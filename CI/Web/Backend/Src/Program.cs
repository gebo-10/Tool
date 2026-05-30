using Backend.Data;
using Backend.Models;
using Backend.Service;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddControllers();




// 注册 UserDbContext（用户数据）
builder.Services.AddDbContext<UserDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("UserConnection")));

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

// 先注册为单例（用于控制器注入）
builder.Services.AddSingleton<BuildService>();

// 再注册为托管服务（使用同一个实例）
builder.Services.AddSingleton<IHostedService>(sp => sp.GetRequiredService<BuildService>());


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
    app.UseSwagger();
    app.UseSwaggerUI();
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



app.Use((context, next) =>
{
    if (string.IsNullOrEmpty(context.Request.Headers["Authorization"]) &&
        context.Request.Query.TryGetValue("token", out var token))
    {
        context.Request.Headers["Authorization"] = $"Bearer {token}";
    }
    return next();
});


app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// 3. 最关键：所有不是 /api 开头的请求，且找不到对应文件的，都返回 index.html
app.MapFallbackToFile("index.html");


//app.MapGet("/api/logs", async (HttpContext context, CancellationToken ct) =>
//{
//    context.Response.ContentType = "text/event-stream";
//    context.Response.Headers["Cache-Control"] = "no-cache";
//    context.Response.Headers["Connection"] = "keep-alive";

//    try
//    {
//        int i = 0;
//        while (!ct.IsCancellationRequested)
//        {
//            var logEntry = new
//            {
//                time = DateTime.Now.ToString("HH:mm:ss"),
//                level = i % 5 == 0 ? "ERROR" : "INFO",
//                message = $"日志消息 #{i}"
//            };

//            string json = System.Text.Json.JsonSerializer.Serialize(logEntry);
//            await context.Response.WriteAsync($"data: {json}\n\n", ct);
//            await context.Response.Body.FlushAsync(ct);

//            i++;
//            await Task.Delay(1000, ct);
//        }
//    }
//    catch (OperationCanceledException)
//    {
//        // 客户端断开连接（刷新页面、关闭标签页等），正常结束，无需记录
//    }
//}).RequireAuthorization();




//app.MapGet("/api/pipelines/{pipelineId}/dag-updates", async(string pipelineId, HttpContext context, ...) =>
//{
//    context.Response.ContentType = "text/event-stream";
//    var channel = _pipelineHub.Subscribe(pipelineId);
//    try
//    {
//        await foreach (var msg in channel.Reader.ReadAllAsync(context.RequestAborted))
//        {
//            await context.Response.WriteAsync($"data: {msg}\n\n", context.RequestAborted);
//            await context.Response.Body.FlushAsync();
//        }
//    }
//    catch (OperationCanceledException) { }
//});



//// ====== 获取 Demo DAG 结构 ======
//app.MapGet("/api/pipeline/demo/dag", () =>
//{
//    var nodes = new[]
//    {
//        new { id = "build-front",  label = "编译前端", status = "pending", progress = 0 },
//        new { id = "build-back",   label = "编译后端", status = "pending", progress = 0 },
//        new { id = "integration",  label = "集成测试", status = "pending", progress = 0 },
//        new { id = "deploy",       label = "部署上线", status = "pending", progress = 0 }
//    };

//    var edges = new[]
//    {
//        new { source = "build-front", target = "integration" },
//        new { source = "build-back",  target = "integration" },
//        new { source = "integration", target = "deploy" }
//    };

//    return Results.Ok(new { nodes, edges });
//});

// ====== Demo SSE：模拟任务进度推送 ======
//app.MapGet("/api/pipeline/demo/dag-updates", async (HttpContext context, CancellationToken ct) =>
//{
//    context.Response.ContentType = "text/event-stream";
//    context.Response.Headers["Cache-Control"] = "no-cache";

//    var steps = new[] { "build-front", "build-back", "integration", "deploy" };
//    foreach (var nodeId in steps)
//    {
//        for (int p = 0; p <= 100; p += 20)
//        {
//            if (ct.IsCancellationRequested) break;

//            var update = new
//            {
//                nodeId,
//                status = p == 100 ? "completed" : "running",
//                progress = p
//            };
//            string json = System.Text.Json.JsonSerializer.Serialize(update);
//            await context.Response.WriteAsync($"data: {json}\n\n", ct);
//            await context.Response.Body.FlushAsync(ct);
//            await Task.Delay(300, ct);   // 模拟实际耗时
//        }
//    }
//});




app.Run();