using Backend.Data;
using BuildSystem;
using DagEngine;
using System.Threading.Channels;
namespace Backend.Service
{
    public class BuildService : BackgroundService
    {
        private readonly PipelineDbContext _db;
        private readonly ILogger<BuildService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly HmiCi _hmiCi;
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);
        private readonly CancellationTokenSource _stopCts = new();


        public BuildService(PipelineDbContext db, ILogger<BuildService> logger, IServiceScopeFactory scopeFactory)
        {
            _db = db;
            _logger = logger;
            _scopeFactory = scopeFactory;
            //_hmiCi = new HmiCi(
            //    "H:\\Work\\Tool\\CI\\Web\\Backend\\wwwroot\\Artifact",
            //    new[] { "H:\\Work\\Tool\\CI\\Workspaces\\Workspace1", "H:\\Work\\Tool\\CI\\Workspaces\\Workspace2" }
            //    );
            _hmiCi = new HmiCi(
                "D:\\work3d\\Tool\\CI\\Web\\Backend\\wwwroot\\Artifact",
                new[] { "D:\\work3d\\Tool\\CI\\Workspaces\\Workspace1", "D:\\work3d\\Tool\\CI\\Workspaces\\Workspace2" }
                );
        }

        public void NotifyNewTask() => _signal.Release();

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // 注册停止事件
            stoppingToken.Register(() => _signal.Release());

            _hmiCi.PipelineStarted += (pipeline, workspace) => UpdatePipelineStatus(pipeline, "Started");
            _hmiCi.PipelineCompleted += (pipeline, workspace) => UpdatePipelineStatus(pipeline, "Completed");
            _hmiCi.PipelineFailed += (pipeline, ex) => UpdatePipelineStatus(pipeline, "Failed", null,ex);
            _hmiCi.PipelineCancelled += (pipeline) => UpdatePipelineStatus(pipeline, "Cancelled");
            _hmiCi.PipelineProgress +=(pipeline, progress) => UpdatePipelineStatus(pipeline, "Progress", progress);

            NotifyNewTask();//启动的时候清理数据库 running的变failed  pending的执行 TODO

            while (!stoppingToken.IsCancellationRequested)
            {
                await _signal.WaitAsync(stoppingToken);
                while (await TryProcessOneTaskAsync(stoppingToken)) { } //无法并行 TODO
            }

            await _hmiCi.StopAsync();
        }

        private async Task<bool> TryProcessOneTaskAsync(CancellationToken token)
        {
            //using var scope = _scopeFactory.CreateScope();
            //var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            //// 原子性取一个 Pending 任务
            //var entity = await db.Pipelines
            //    .Where(p => p.Status == "Pending")
            //    .OrderBy(p => p.CreatedAt)
            //    .FirstOrDefaultAsync(token);

            var col = _db.Pipelines;
            var entity = col.Query()
               .Where(p => p.Status == "Pending")
               .OrderBy(p => p.CreatedAt)
               .Limit(1)
               .FirstOrDefault();

            if (entity == null) return false;

           

            // 创建 PipelineEntity 并提交到 HmiCi
            var pipeline = new BuildSystem.Pipeline(new Dictionary<string, object> { ["Name"] = entity.Name });
            pipeline.Id = entity.Id;
            pipeline.Guid=entity.PipelineGuid;

            entity.Status = "Running";
            entity.DagJson = pipeline.ToJson();

            //await db.SaveChangesAsync(token);
            col.Update(entity);

            _hmiCi.EnqueuePipeline(pipeline);
            return true;
        }

        private async void UpdatePipelineStatus(BuildSystem.Pipeline pipeline,string eventType, NodeProgressEventArgs? progress=null, Exception? ex=null)
        {
            if(eventType == "Progress")
            {
                //Console.WriteLine($"节点 {e.NodeId} {e.NodeType} {e.NodeName} 进度: {e.Percentage}%");
                //var json = JsonSerializer.Serialize(progress.Node.Serialize(), new JsonSerializerOptions { WriteIndented = true });
                //Console.WriteLine(json);
                _logger.LogInformation($"PipelineEntity {pipeline.Guid} Progress: {progress?.Percentage}% - Node: {progress?.NodeName} ({progress?.NodeType})");

                await PublishEventAsync(new BuildEvent("NodeInfo", pipeline.Id, progress.Node.Serialize()));
            }
            else
            {
                //using var scope = _scopeFactory.CreateScope();
                //var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // 根据 id查找实体
                //var entity = await db.Pipelines.FirstOrDefaultAsync(p => p.Id == pipeline.Id);

                var col = _db.Pipelines;
                var entity = col.FindById(pipeline.Id);
                if (entity != null)
                {
                    entity.Status = pipeline.status.ToString();
                    //entity.CompletedAt = DateTime.Now;
                    switch(eventType)
                    {
                        case "Started":
                            
                            break;
                        case "Completed":
                            entity.CompletedAt = DateTime.Now;
                            break;
                        case "Failed":
                            entity.CompletedAt = DateTime.Now;
                            entity.Description = ex?.Message;
                            break;
                        case "Cancelled":
                            entity.CompletedAt = DateTime.Now;
                            break;
                    }
                    entity.DagJson = pipeline.ToJson();
                    //await db.SaveChangesAsync();
                    col.Update(entity);

                    await PublishEventAsync(new BuildEvent("PipelineInfo", pipeline.Id, entity));

                    await PublishEventAsync(new BuildEvent("DagInfo", pipeline.Id, pipeline.ToJson()));
                }
                else
                {
                    _logger.LogWarning($"未找到 Id 为 {pipeline.Id} 的记录");
                }
            }


        }


        public BuildSystem.Pipeline? GetPipeline(string guid)
        {
            return _hmiCi.GetPipeline(guid);
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="eventType">pipeline,node</param>
        /// pipeline: 状态 进度 开始时间  结束时间 
        /// node: 状态 进度 
        /// <param name="PipelineId"></param>
        /// <param name="Status"></param>
        /// <param name="Progress"></param>
        /// <param name="Message"></param>
        public record BuildEvent(string eventType, int PipelineId, object info); //string Status, int Progress, string? Message
        

        // 全局事件通道
        private readonly Channel<BuildEvent> _eventChannel = Channel.CreateBounded<BuildEvent>(new BoundedChannelOptions(100)
        {
            FullMode = BoundedChannelFullMode.DropOldest  // 队列满时丢弃最旧的事件
        });

        public async Task PublishEventAsync(BuildEvent evt)
        {
            // 在产生事件的地方检查
            if (Volatile.Read(ref _subscriberCount) > 0)
            {
                await _eventChannel.Writer.WriteAsync(evt);
            }
        }

        private int _subscriberCount = 0;
        public IAsyncEnumerable<BuildEvent> SubscribePipeline(CancellationToken ct)
        {
            Interlocked.Increment(ref _subscriberCount);
            ct.Register(() => Interlocked.Decrement(ref _subscriberCount));

            return _eventChannel.Reader.ReadAllAsync(ct);
        }
    }
}