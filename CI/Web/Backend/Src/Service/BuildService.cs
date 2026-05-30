using Backend.Data;
using BuildSystem;
using DagEngine;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Threading.Channels;
namespace Backend.Service
{
    public class BuildService : BackgroundService
    {
        private readonly ILogger<BuildService> _logger;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly HmiCi _hmiCi;
        private readonly SemaphoreSlim _signal = new SemaphoreSlim(0);
        private readonly CancellationTokenSource _stopCts = new();


        public BuildService(ILogger<BuildService> logger, IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _scopeFactory = scopeFactory;
            _hmiCi = new HmiCi(new[] { "D:\\work3d\\Tool\\CI\\Workspaces\\Workspace1", "D:\\work3d\\Tool\\CI\\Workspaces\\Workspace2" });
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

            while (!stoppingToken.IsCancellationRequested)
            {
                await _signal.WaitAsync(stoppingToken);
                while (await TryProcessOneTaskAsync(stoppingToken)) { }
            }

            await _hmiCi.StopAsync();
        }

        private async Task<bool> TryProcessOneTaskAsync(CancellationToken token)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // 原子性取一个 Pending 任务
            var entity = await db.Pipelines
                .Where(p => p.Status == "Pending")
                .OrderBy(p => p.CreatedAt)
                .FirstOrDefaultAsync(token);

            if (entity == null) return false;

           

            // 创建 Pipeline 并提交到 HmiCi
            var pipeline = new Pipeline(new Dictionary<string, object> { ["Name"] = entity.Name });
            pipeline.Id = entity.PipelineId.ToString(); // 使用数据库 ID

            entity.Status = "Running";
            entity.DagJson = pipeline.ToJson();
            await db.SaveChangesAsync(token);

            _hmiCi.EnqueuePipeline(pipeline);
            return true;
        }

        private async void UpdatePipelineStatus(BuildSystem.Pipeline pipeline,string eventType, NodeProgressEventArgs? progress=null, Exception? ex=null)
        {
            if(eventType == "Progress")
            {
                //Console.WriteLine($"节点 {e.NodeId} {e.NodeType} {e.NodeName} 进度: {e.Percentage}%");
                var json = JsonSerializer.Serialize(progress.Node.Serialize(), new JsonSerializerOptions { WriteIndented = true });
                //Console.WriteLine(json);
                _logger.LogInformation($"Pipeline {pipeline.Id} Progress: {progress?.Percentage}% - Node: {progress?.NodeName} ({progress?.NodeType})");

                // 在产生事件的地方检查
                if (Volatile.Read(ref _subscriberCount) > 0)
                {
                    //await _eventChannel.Writer.WriteAsync(new BuildEvent());
                }

            }
            else
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                // 根据 PipelineId（字符串）查找实体
                var entity = await db.Pipelines.FirstOrDefaultAsync(p => p.PipelineId == pipeline.Id);
                if (entity != null)
                {
                    entity.Status = pipeline.status.ToString();
                    entity.CompletedAt = DateTime.Now;
                    entity.DagJson = pipeline.ToJson();
                    await db.SaveChangesAsync();
                }
                else
                {
                    _logger.LogWarning($"未找到 PipelineId 为 {pipeline.Id} 的记录");
                }
            }


        }


        public record BuildEvent(int PipelineId, string Status, int Progress, string? Message);
        // 全局事件通道

        private readonly Channel<BuildEvent> _eventChannel = Channel.CreateBounded<BuildEvent>(new BoundedChannelOptions(1000)
        {
            FullMode = BoundedChannelFullMode.DropOldest  // 队列满时丢弃最旧的事件
        });


        private int _subscriberCount = 0;
        public IAsyncEnumerable<BuildEvent> SubscribePipeline(int pipelineId, CancellationToken ct)
        {
            Interlocked.Increment(ref _subscriberCount);
            ct.Register(() => Interlocked.Decrement(ref _subscriberCount));

            return _eventChannel.Reader.ReadAllAsync(ct)
                .Where(e => e.PipelineId == pipelineId);
        }
    }
}