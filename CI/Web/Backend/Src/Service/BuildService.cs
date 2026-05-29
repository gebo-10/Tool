using Backend.Data;
using BuildSystem;
using Microsoft.EntityFrameworkCore;
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

            _hmiCi.PipelineCompleted += UpdateStatus;
            _hmiCi.PipelineFailed += UpdateStatus;
            _hmiCi.PipelineCancelled += UpdateStatus;

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

            entity.Status = "Running";
            await db.SaveChangesAsync(token);

            // 创建 Pipeline 并提交到 HmiCi
            var pipeline = new Pipeline(new Dictionary<string, object> { ["Name"] = entity.Name });
            pipeline.Id = entity.PipelineId.ToString(); // 使用数据库 ID
            _hmiCi.EnqueuePipeline(pipeline);
            return true;
        }

        private void UpdateStatus(Pipeline pipeline, Workspace workspace) => UpdatePipelineStatus(pipeline, "Completed");
        private void UpdateStatus(Pipeline pipeline, Exception ex) => UpdatePipelineStatus(pipeline, "Failed");
        private void UpdateStatus(Pipeline pipeline) => UpdatePipelineStatus(pipeline, "Cancelled");

        private async void UpdatePipelineStatus(BuildSystem.Pipeline pipeline, string status)
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            // 根据 PipelineId（字符串）查找实体
            var entity = await db.Pipelines.FirstOrDefaultAsync(p => p.PipelineId == pipeline.Id);
            if (entity != null)
            {
                entity.Status = status;
                entity.CompletedAt = DateTime.UtcNow;
                await db.SaveChangesAsync();
            }
            else
            {
                _logger.LogWarning($"未找到 PipelineId 为 {pipeline.Id} 的记录");
            }
        }
    }
}