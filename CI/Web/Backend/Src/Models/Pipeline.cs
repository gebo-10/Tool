using System.ComponentModel.DataAnnotations;

namespace Backend.Models;

public class Pipeline
{
    [Key]
    public int Id { get; set; }

    public string PipelineId { get; set; } = Guid.NewGuid().ToString(); // 方便前端使用字符串 ID

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(250)]
    public string? Description { get; set; }

    public string Params { get; set; } = "{}";

    public string Status { get; set; } = "Pending";  // Pending, Running, Completed, Failed, Cancelled    

    /// <summary>
    /// 例如 "DAG" 结构的 JSON 配置（节点 + 边）
    /// </summary>
    public string DagJson { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string? Creator { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    //public DateTime? UpdatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    
}