using System.ComponentModel.DataAnnotations;

namespace Backend.Models;

public class Pipeline
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// 例如 "DAG" 结构的 JSON 配置（节点 + 边）
    /// </summary>
    public string DagJson { get; set; } = string.Empty;

    public string? Creator { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}