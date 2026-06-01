using Backend.Data;
using Backend.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text;
using System.Text.Json;
using static Backend.Service.BuildService;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]  // 继承全局 JWT 认证要求
public class PipelinesController : ControllerBase
{
    private readonly PipelineDbContext _db;
    private readonly BuildService _buildService;  // 直接使用具体类型
    private readonly IWebHostEnvironment _env;
    public PipelinesController(PipelineDbContext db, BuildService buildService, IWebHostEnvironment env)
    {
        _db = db;

        _buildService = buildService;
        _env = env;
    }

    // POST /api/pipelines
    [HttpPost]
    public async Task<ActionResult<PipelineEntity>> CreatePipeline([FromBody] CreatePipelineRequest request)
    {
        // 获取当前用户名（从 JWT 或自定义头部）
        var currentUser = User.Identity?.Name ?? "anonymous";

        var pipeline = new PipelineEntity
        {
            Name = request.Name,
            Description = request.Description,
            //Dag = request.DagJson,
            Creator = currentUser,
            CreatedAt = DateTime.Now
        };

        //_db.Pipelines.Add(pipeline);
        //await _db.SaveChangesAsync();
        _db.Pipelines.Insert(pipeline);


        var response = ToResponse(pipeline);
        await _buildService.PublishEventAsync(new BuildEvent(
           "PipelineCreated",
           pipeline.Id,
           response  // 直接传递响应对象，前端可直接用来插入表格
       ));

        // 2. 可选：立即触发后台拉取（发送信号，如使用 Channel 或 SemaphoreSlim）
        _buildService.NotifyNewTask(); // 或依赖轮询

        return CreatedAtAction(nameof(GetPipelineById), new { id = pipeline.Id }, ToResponse(pipeline));
    }

    // GET /api/pipelines?page=1&size=10&search=xxx
    [HttpGet]
    public async Task<ActionResult<PagedResult<PipelineEntity>>> GetPipelines(
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
        [FromQuery] string? search = null)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 10;
        if (size > 100) size = 100;


        var total = _db.Pipelines.Count();
        var items = _db.Pipelines.Query()
            .OrderByDescending(p => p.CreatedAt)
            .Offset((page - 1) * size)
            .Limit(size)
            .ToList();


        var result = new PagedResult<PipelineEntity>
        {
            PageIndex = page,
            PageSize = size,
            TotalCount = total,
            Items = items
        };

        return Ok(result);
    }

    // GET /api/pipelines/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PipelineEntity>> GetPipelineById(int id)
    {
        //var entity = await _db.Pipelines.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        var entity = _db.Pipelines.FindById(id);
        if (entity == null)
            return NotFound();

        var pipeline=_buildService.GetPipeline(entity.PipelineGuid);
        if(pipeline != null)
        {
            entity.Dag = pipeline?.Serialize();
        }
        return Ok(ToResponse(entity));
    }

    private static PipelineEntity ToResponse(PipelineEntity p)
    {
        return p;
    }

    //private static PipelineResponse ToResponse(PipelineEntity p)
    //{
    //    return new PipelineResponse
    //    {
    //        Id = p.Id,
    //        Name = p.Name,
    //        Description = p.Description,
    //        Dag = p.Dag,
    //        Creator = p.Creator,
    //        CreatedAt = p.CreatedAt,
    //        CompletedAt = p.CompletedAt,
    //    };
    //}


    [HttpGet("status-stream")]
    [AllowAnonymous]
    public async Task StreamPipelineStatus(CancellationToken cancellationToken)
    {
        // SSE 响应头
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no");

        try
        {
            // 首次连接时发送一次当前状态（可选）
            //var initialStatus = await GetPipelineStatusAsync(cancellationToken);
            //await WriteSseDataAsync(initialStatus, cancellationToken);

            // 订阅 BuildService 的事件流
            await foreach (var buildEvent in _buildService.SubscribePipeline(cancellationToken))
            {
                var json = System.Text.Json.JsonSerializer.Serialize(buildEvent, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });
                await Response.WriteAsync($"data: {json}\n\n", cancellationToken);
                await Response.Body.FlushAsync(cancellationToken);
            }
        }
        catch (OperationCanceledException)
        {
            // 客户端主动断开，正常结束
        }
    }

    //// 辅助方法：写一条 SSE 消息
    //private async Task WriteSseDataAsync(object data, CancellationToken ct)
    //{
    //    var json = JsonSerializer.Serialize(data, new JsonSerializerOptions
    //    {
    //        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    //    });
    //    await Response.WriteAsync($"data: {json}\n\n", ct);
    //    await Response.Body.FlushAsync(ct);
    //}

    [HttpGet("{id:int}/tasks/{taskId}/logs")]
    public async Task GetTaskLogs(int id, string taskId, CancellationToken cancellationToken)
    {
        // SSE 响应头
        Response.Headers.Append("Content-Type", "text/event-stream");
        Response.Headers.Append("Cache-Control", "no-cache");
        Response.Headers.Append("Connection", "keep-alive");
        Response.Headers.Append("X-Accel-Buffering", "no");

        var workDir = Path.Combine(_env.ContentRootPath, "wwwroot", "Artifact");
        var entity=_db.Pipelines.FindById(id);
        if (entity == null) 
        {
            return;
        }
        var filePath = Path.Combine(workDir, entity.PipelineGuid, $"{taskId}.log");

        // 文件不存在则直接返回错误事件并断开
        if (!System.IO.File.Exists(filePath))
        {
            var errorPayload = JsonSerializer.Serialize(new { error = "Log file not found" },
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            await Response.WriteAsync($"data: {errorPayload}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
            return;
        }

        // 1. 发送已有全部日志（按行组织成一个事件）
        var existingContent = await System.IO.File.ReadAllTextAsync(filePath, cancellationToken);
        if (!string.IsNullOrEmpty(existingContent))
        {
            var lines = existingContent.Split('\n', StringSplitOptions.None);
            var payload = JsonSerializer.Serialize(new { lines = lines },
                new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
            await Response.WriteAsync($"data: {payload}\n\n", cancellationToken);
            await Response.Body.FlushAsync(cancellationToken);
        }

        //// 2. 持续监控文件新增内容，逐行推送
        //using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        //fs.Seek(0, SeekOrigin.End); // 定位到当前末尾

        //var buffer = new byte[4096];
        //var leftover = string.Empty; // 上次读取不完整的行

        //while (!cancellationToken.IsCancellationRequested)
        //{
        //    if (fs.Position < fs.Length)
        //    {
        //        int bytesRead = await fs.ReadAsync(buffer, 0, buffer.Length, cancellationToken);
        //        if (bytesRead > 0)
        //        {
        //            var chunk = Encoding.UTF8.GetString(buffer, 0, bytesRead);
        //            chunk = leftover + chunk;
        //            var parts = chunk.Split('\n');
        //            leftover = parts[^1]; // 最后一个元素可能是不完整行

        //            for (int i = 0; i < parts.Length - 1; i++)
        //            {
        //                // 发送每一完整行（空行也发送，方便客户端区分段落）
        //                var linePayload = JsonSerializer.Serialize(new { line = parts[i] },
        //                    new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        //                await Response.WriteAsync($"data: {linePayload}\n\n", cancellationToken);
        //            }
        //            await Response.Body.FlushAsync(cancellationToken);
        //        }
        //    }
        //    else
        //    {
        //        await Task.Delay(500, cancellationToken);
        //    }
        //}
    }

}