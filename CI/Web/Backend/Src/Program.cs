using Backend.Data;
using Backend.Service;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.FileProviders;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddControllers();

builder.Services.AddDirectoryBrowser();


// 注册用户数据库（单例）
builder.Services.AddSingleton<UserDbContext>(sp =>
{
    return new UserDbContext(@"users.db");   // 单独的文件
});

// 注册用户数据库（单例）
builder.Services.AddSingleton<PipelineDbContext>(sp =>
{
    return new PipelineDbContext(@"pipelines.db");   // 单独的文件
});

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


//app.UseHttpsRedirection();



// 1. 启用静态文件服务（会自动去 wwwroot 找 index.html、JS、CSS 等）
//app.UseDefaultFiles();       // 自动寻找默认页（如 index.html）
//app.UseStaticFiles();    // 先启用静态文件（提供下载）
//app.UseDirectoryBrowser(); // 再启用目录浏览（显示列表）


// 定义要暴露的物理目录和 URL 路径
var outputsPath = Path.Combine(builder.Environment.WebRootPath, "Artifact");
Directory.CreateDirectory(outputsPath); // 确保目录存在

// 1. 静态文件中间件（处理文件下载）
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(outputsPath),
    RequestPath = "/Artifact",               // 对外访问的 URL 路径
    ServeUnknownFileTypes = true,          // 允许未知扩展名（如 .log）
    OnPrepareResponse = ctx =>
    {
        // 强制所有文件以附件形式下载
        ctx.Context.Response.Headers.ContentDisposition =
            $"attachment; filename=\"{ctx.File.Name}\"";
        // 可选：统一为 octet-stream 避免浏览器试图预览
        ctx.Context.Response.ContentType = "application/octet-stream";
    }
});

// 目录浏览（如需）
app.UseDirectoryBrowser(new DirectoryBrowserOptions
{
    FileProvider = new PhysicalFileProvider(
        Path.Combine(builder.Environment.WebRootPath, "Artifact")),
    RequestPath = "/Artifact"
});



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

app.Run();