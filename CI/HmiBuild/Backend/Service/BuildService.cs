using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Backend.Data;

namespace Backend.Services;

public class BuildService : BackgroundService
{
    private readonly ILogger<BuildService> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public BuildService(ILogger<BuildService> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;   // 用于获取 Scoped 服务（如 DbContext）
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("常驻服务已启动。");

        // 示例：每秒执行一次任务
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                // 如果需要使用 DbContext 或其他 Scoped 服务，必须创建独立的作用域
                using (var scope = _scopeFactory.CreateScope())
                {
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    // 执行你的数据操作...
                    _logger.LogInformation("后台服务正在工作，当前时间: {Time}", DateTime.Now);
                }

                // 等待一段时间，避免空转
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // 正常停止，不做处理
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "后台服务发生未处理异常。");
                // 可根据需要增加重试或延迟逻辑
                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }

        _logger.LogInformation("常驻服务已停止。");
    }
}