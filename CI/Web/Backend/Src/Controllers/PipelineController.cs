using Backend.Data;
using Backend.Models;
using Backend.Service;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using static Backend.Data.AppDbContext;
using static Backend.Service.BuildService;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]  // 继承全局 JWT 认证要求
public class PipelinesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly BuildService _buildService;  // 直接使用具体类型

    public PipelinesController(AppDbContext db, BuildService buildService)
    {
        _db = db;
        _buildService = buildService;
    }

    // POST /api/pipelines
    [HttpPost]
    public async Task<ActionResult<Pipeline>> CreatePipeline([FromBody] CreatePipelineRequest request)
    {
        // 获取当前用户名（从 JWT 或自定义头部）
        var currentUser = User.Identity?.Name ?? "anonymous";

        var pipeline = new Pipeline
        {
            Name = request.Name,
            Description = request.Description,
            DagJson = request.DagJson,
            Creator = currentUser,
            CreatedAt = DateTime.Now
        };

        _db.Pipelines.Add(pipeline);
        await _db.SaveChangesAsync();

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
    public async Task<ActionResult<PagedResult<Pipeline>>> GetPipelines(
        [FromQuery] int page = 1,
        [FromQuery] int size = 10,
        [FromQuery] string? search = null)
    {
        if (page < 1) page = 1;
        if (size < 1) size = 10;
        if (size > 100) size = 100;

        var query = _db.Pipelines.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(p => p.Name.Contains(search) || (p.Description != null && p.Description.Contains(search)));
        }

        var totalCount = await query.CountAsync();
        var items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(p => ToResponse(p))
            .ToListAsync();

        var result = new PagedResult<Pipeline>
        {
            PageIndex = page,
            PageSize = size,
            TotalCount = totalCount,
            Items = items
        };

        return Ok(result);
    }

    // GET /api/pipelines/{id}
    [HttpGet("{id:int}")]
    public async Task<ActionResult<Pipeline>> GetPipelineById(int id)
    {
        var pipeline = await _db.Pipelines.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (pipeline == null)
            return NotFound();

        //_buildService.


        return Ok(ToResponse(pipeline));
    }

    //// PUT /api/pipelines/{id}
    //[HttpPut("{id:int}")]
    //public async Task<IActionResult> UpdatePipeline(int id, [FromBody] UpdatePipelineRequest request)
    //{
    //    var pipeline = await _db.Pipelines.FindAsync(id);
    //    if (pipeline == null)
    //        return NotFound();

    //    pipeline.Name = request.Name;
    //    pipeline.Description = request.Description;
    //    pipeline.DagJson = request.DagJson;
    //    //pipeline.CompletedAt = DateTime.UtcNow;

    //    await _db.SaveChangesAsync();
    //    return NoContent();
    //}

    //// DELETE /api/pipelines/{id}
    //[HttpDelete("{id:int}")]
    //public async Task<IActionResult> DeletePipeline(int id)
    //{
    //    var pipeline = await _db.Pipelines.FindAsync(id);
    //    if (pipeline == null)
    //        return NotFound();

    //    _db.Pipelines.Remove(pipeline);
    //    await _db.SaveChangesAsync();
    //    return NoContent();
    //}

    private static Pipeline ToResponse(Pipeline p)
    {
        return p;
    }

    //private static PipelineResponse ToResponse(Pipeline p)
    //{
    //    return new PipelineResponse
    //    {
    //        Id = p.Id,
    //        Name = p.Name,
    //        Description = p.Description,
    //        DagJson = p.DagJson,
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
                var json = JsonSerializer.Serialize(buildEvent, new JsonSerializerOptions
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

}