using System.ComponentModel.DataAnnotations;

namespace Backend.Models;

public class WeatherRecord
{
    [Key]
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public int TemperatureC { get; set; }
    public string? Summary { get; set; }
    
    // 计算属性不需要存进数据库
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}