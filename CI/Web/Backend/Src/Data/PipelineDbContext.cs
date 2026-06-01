using LiteDB;

namespace Backend.Data;

public class PipelineDbContext
{
    public LiteDatabase Database { get; }

    public PipelineDbContext(string connectionString)
    {
        Database = new LiteDatabase(connectionString);
    }

    // 可选：直接暴露 User 集合的快捷方法
    public ILiteCollection<PipelineEntity> Pipelines => Database.GetCollection<PipelineEntity>("pipelines");
}

public class PipelineParam
{
    public bool IsRelease { get; set; } = false;
    public bool IsBuildApk { get; set; } = false;
}

public class PipelineEntity
{
    public int Id { get; set; }

    public string PipelineGuid { get; set; } = System.Guid.NewGuid().ToString(); // 方便前端使用字符串 ID

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public PipelineParam Params { get; set; } = new PipelineParam();

    public string Status { get; set; } = "Pending";  // Pending, Running, Completed, Failed, Cancelled    

    /// <summary>
    /// 例如 "DAG" 结构的 JSON 配置（节点 + 边）
    /// </summary>
    public string DagJson { get; set; } = string.Empty;

    public string? Creator { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public DateTime? StartdAt { get; set; }

    public DateTime? CompletedAt { get; set; }
}

// ---------- 请求 DTO ----------
public class CreatePipelineRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string DagJson { get; set; } = string.Empty;
}

public class PagedResult<T>
{
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public List<T> Items { get; set; } = new();
}