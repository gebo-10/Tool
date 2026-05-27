using Backend.Data;
using Backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static Backend.Data.AppDbContext;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]  // 继承全局 JWT 认证要求
public class PipelinesController : ControllerBase
{
    private readonly AppDbContext _db;

    public PipelinesController(AppDbContext db)
    {
        _db = db;
    }

    // POST /api/pipelines
    [HttpPost]
    public async Task<ActionResult<PipelineResponse>> CreatePipeline([FromBody] CreatePipelineRequest request)
    {
        // 获取当前用户名（从 JWT 或自定义头部）
        var currentUser = User.Identity?.Name ?? "anonymous";

        var pipeline = new Pipeline
        {
            Name = request.Name,
            Description = request.Description,
            DagJson = request.DagJson,
            Creator = currentUser,
            CreatedAt = DateTime.UtcNow
        };

        _db.Pipelines.Add(pipeline);
        await _db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPipelineById), new { id = pipeline.Id }, ToResponse(pipeline));
    }

    // GET /api/pipelines?page=1&size=10&search=xxx
    [HttpGet]
    public async Task<ActionResult<PagedResult<PipelineResponse>>> GetPipelines(
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

        var result = new PagedResult<PipelineResponse>
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
    public async Task<ActionResult<PipelineResponse>> GetPipelineById(int id)
    {
        var pipeline = await _db.Pipelines.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id);
        if (pipeline == null)
            return NotFound();

        return Ok(ToResponse(pipeline));
    }

    // PUT /api/pipelines/{id}
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdatePipeline(int id, [FromBody] UpdatePipelineRequest request)
    {
        var pipeline = await _db.Pipelines.FindAsync(id);
        if (pipeline == null)
            return NotFound();

        pipeline.Name = request.Name;
        pipeline.Description = request.Description;
        pipeline.DagJson = request.DagJson;
        pipeline.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/pipelines/{id}
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeletePipeline(int id)
    {
        var pipeline = await _db.Pipelines.FindAsync(id);
        if (pipeline == null)
            return NotFound();

        _db.Pipelines.Remove(pipeline);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static PipelineResponse ToResponse(Pipeline p)
    {
        return new PipelineResponse
        {
            Id = p.Id,
            Name = p.Name,
            Description = p.Description,
            DagJson = p.DagJson,
            Creator = p.Creator,
            CreatedAt = p.CreatedAt,
            UpdatedAt = p.UpdatedAt
        };
    }
}