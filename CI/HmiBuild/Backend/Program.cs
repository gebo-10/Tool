using Backend.Data;
using Backend.Models;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddControllers();


// 注册 UserDbContext（用户数据）
builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("UserConnection")));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));



// JWT 认证配置
var jwtKey = builder.Configuration["Jwt:Key"]!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();



var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}



using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    var userDb = scope.ServiceProvider.GetRequiredService<UserDbContext>();
    userDb.Database.EnsureCreated();
}





//app.UseHttpsRedirection();


// 1. 启用静态文件服务（会自动去 wwwroot 找 index.html、JS、CSS 等）
app.UseDefaultFiles();       // 自动寻找默认页（如 index.html）
app.UseStaticFiles();

// 2. 配置路由
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// 3. 最关键：所有不是 /api 开头的请求，且找不到对应文件的，都返回 index.html
app.MapFallbackToFile("index.html");


var summaries = new[]
{
    "1Freezing", "2Bracing", "3Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
};

//app.MapGet("/api/weatherforecast", () =>
//{
//    var forecast =  Enumerable.Range(1, 3).Select(index =>
//        new WeatherForecast
//        (
//            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
//            Random.Shared.Next(-20, 55),
//            summaries[Random.Shared.Next(summaries.Length)]
//        ))
//        .ToArray();
//    return forecast;
//})
//.WithName("GetWeatherForecast");


app.MapGet("/api/weatherforecast", async (AppDbContext db) =>
{
    var records = await db.WeatherRecords.ToListAsync();
    if (!records.Any())
    {
        var seedData = new List<WeatherRecord>
        {
            new() { Date = DateOnly.FromDateTime(DateTime.Now), TemperatureC = 10, Summary = "Cool" },
            new() { Date = DateOnly.FromDateTime(DateTime.Now.AddDays(1)), TemperatureC = 25, Summary = "Warm" },
            new() { Date = DateOnly.FromDateTime(DateTime.Now.AddDays(2)), TemperatureC = -5, Summary = "Freezing" }
        };
        db.WeatherRecords.AddRange(seedData);
        await db.SaveChangesAsync();
        records = seedData;
    }
    return records;
}).RequireAuthorization();


app.MapGet("/api/logs", async (HttpContext context, CancellationToken ct) =>
{
    context.Response.ContentType = "text/event-stream";
    context.Response.Headers["Cache-Control"] = "no-cache";
    context.Response.Headers["Connection"] = "keep-alive";

    try
    {
        int i = 0;
        while (!ct.IsCancellationRequested)
        {
            var logEntry = new
            {
                time = DateTime.Now.ToString("HH:mm:ss"),
                level = i % 5 == 0 ? "ERROR" : "INFO",
                message = $"日志消息 #{i}"
            };

            string json = System.Text.Json.JsonSerializer.Serialize(logEntry);
            await context.Response.WriteAsync($"data: {json}\n\n", ct);
            await context.Response.Body.FlushAsync(ct);

            i++;
            await Task.Delay(1000, ct);
        }
    }
    catch (OperationCanceledException)
    {
        // 客户端断开连接（刷新页面、关闭标签页等），正常结束，无需记录
    }
}).RequireAuthorization();



app.Run();

record WeatherForecast(DateOnly Date, int TemperatureC, string? Summary)
{
    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);
}
