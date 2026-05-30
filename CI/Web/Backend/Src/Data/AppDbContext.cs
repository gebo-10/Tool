using Microsoft.EntityFrameworkCore;
using Backend.Models;

namespace Backend.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<WeatherRecord> WeatherRecords { get; set; }

    public DbSet<Pipeline> Pipelines { get; set; }   // 新增

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 可以配置唯一索引等
        modelBuilder.Entity<Pipeline>()
            .HasIndex(p => p.Name)
            .IsUnique();
    }


    // ---------- 请求 DTO ----------
    public class CreatePipelineRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string DagJson { get; set; } = string.Empty;
    }

    public class UpdatePipelineRequest
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string DagJson { get; set; } = string.Empty;
    }

    // ---------- 响应 DTO ----------
    public class PipelineResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string DagJson { get; set; } = string.Empty;
        public string? Creator { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }

    public class PagedResult<T>
    {
        public int PageIndex { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public List<T> Items { get; set; } = new();
    }
}