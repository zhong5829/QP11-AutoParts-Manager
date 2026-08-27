using QP11.Core.Interfaces;
using QP11.Data.Infrastructure;
using QP11.WebApi.Controllers;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace QP11.WebApi;

public class Program
{
    public static WebApplication CreateWebHost(string[]? args = null)
    {
        // 查找 wwwroot 所在目录（支持从 WPF 内嵌启动）
        var contentRoot = FindContentRoot();

        var options = new WebApplicationOptions
        {
            Args = args ?? Array.Empty<string>(),
            ContentRootPath = contentRoot,
            WebRootPath = Path.Combine(contentRoot, "wwwroot")
        };
        var builder = WebApplication.CreateBuilder(options);

        // 配置日志：输出到 Console（在 WPF 窗口中可见）和 Debug
        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(opts =>
        {
            opts.IncludeScopes = true;
            opts.TimestampFormat = "HH:mm:ss.fff ";
        });
        builder.Logging.SetMinimumLevel(LogLevel.Debug);

        // 响应压缩（10M FRP 带宽优化：JSON 压缩率 70%~85%）
        builder.Services.AddResponseCompression(opts =>
        {
            opts.EnableForHttps = true;
            opts.MimeTypes = new[]
            {
                "application/json",
                "text/plain",
                "text/html",
                "application/javascript",
                "text/css"
            };
            opts.Providers.Add<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProvider>();
        });
        builder.Services.Configure<Microsoft.AspNetCore.ResponseCompression.GzipCompressionProviderOptions>(opts =>
        {
            opts.Level = System.IO.Compression.CompressionLevel.Optimal;
        });

        // 配置服务（必须显式注册 WebApi 程序集中的 Controllers）
        builder.Services.AddControllers()
            .AddApplicationPart(typeof(SellController).Assembly)
            .AddJsonOptions(opts =>
        {
            opts.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
            opts.JsonSerializerOptions.PropertyNamingPolicy = null;
        });

        // Swagger/OpenAPI 文档（仅 Development 环境启用）
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(c =>
        {
            c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
            {
                Title = "QP11 汽配管理系统 API",
                Version = "v1"
            });
        });
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
            {
                var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                    ?? new[] { "http://localhost:5000" };
                policy.WithOrigins(allowedOrigins)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            });
        });

        // 注册现有服务（复用 WPF 项目的 Service 层）
        builder.Services.AddSingleton<QP11.Core.Interfaces.IDbConnectionFactory, QP11.Data.Infrastructure.DbConnectionFactory>();
        builder.Services.AddTransient<ISellService, QP11.Services.SellService>();
        builder.Services.AddTransient<IBuyService, QP11.Services.BuyService>();
        builder.Services.AddTransient<ISellRepository, QP11.Data.Repositories.SellRepository>();
        builder.Services.AddTransient<IPartRepository, QP11.Data.Repositories.PartRepository>();
        builder.Services.AddTransient<IClientRepository, QP11.Data.Repositories.ClientRepository>();
        builder.Services.AddTransient<IUserRepository, QP11.Data.Repositories.UserRepository>();
        builder.Services.AddTransient<IArrearageRepository, QP11.Data.Repositories.ArrearageRepository>();
        builder.Services.AddTransient<IMemberCardRepository, QP11.Data.Repositories.MemberCardRepository>();
        builder.Services.AddTransient<IValidationService, QP11.Services.ValidationService>();
        builder.Services.AddTransient<ICalcService, QP11.Services.CalcService>();
        builder.Services.AddTransient<ISerialNumberService, QP11.Services.SerialNumberService>();
        builder.Services.AddTransient<IAuthService, QP11.Services.AuthService>();
        builder.Services.AddTransient<IPartQueryService, QP11.Services.PartQueryService>();

        var app = builder.Build();

        // 初始化数据库连接工厂
        DatabaseFactory.Initialize(app.Configuration);

        app.UseRouting();

        // Swagger（仅 Development 环境）
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "QP11 API v1"));
        }

        app.UseResponseCompression(); // 响应压缩（必须在 UseCors/MapControllers 之前）
        app.UseCors();

        // 全局异常处理中间件：捕获未处理的异常，返回统一格式
        app.UseExceptionHandler(app =>
        {
            app.Run(async context =>
            {
                var exceptionHandlerFeature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
                var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError(exceptionHandlerFeature?.Error, "[GlobalException] 未处理异常: {Path}", context.Request.Path);

                context.Response.StatusCode = 500;
                context.Response.ContentType = "application/json";
                var response = System.Text.Json.JsonSerializer.Serialize(new { error = "服务器内部错误，请稍后重试" });
                await context.Response.WriteAsync(response);
            });
        });

        // Web端登录认证中间件：所有 /api/ 请求（除 /api/auth/* 外）需携带有效 Token
        app.Use(async (context, next) =>
        {
            var path = context.Request.Path.Value ?? "";
            if (path.StartsWith("/api/", StringComparison.OrdinalIgnoreCase) &&
                !path.StartsWith("/api/auth/", StringComparison.OrdinalIgnoreCase))
            {
                var token = context.Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
                if (string.IsNullOrEmpty(token) || TokenStore.Get(token) == null)
                {
                    context.Response.StatusCode = 401;
                    context.Response.ContentType = "application/json";
                    await context.Response.WriteAsync(System.Text.Json.JsonSerializer.Serialize(new { error = "未登录或登录已过期" }));
                    return;
                }
            }
            await next();
        });

        app.MapControllers();  // API 路由必须在 Fallback 之前
        app.UseStaticFiles(); // 静态文件（wwwroot）
        app.MapFallbackToFile("index.html");

        return app;
    }

    /// <summary>
    /// 查找 wwwroot 目录所在位置，按优先级依次检查：
    /// 1. 程序运行目录/wwwroot（安装目录，最高优先级）
    /// 2. WebApi 程序集目录/wwwroot（开发调试备用）
    /// </summary>
    private static string FindContentRoot()
    {
        var candidates = new[]
        {
            AppContext.BaseDirectory,
            Path.GetDirectoryName(typeof(Program).Assembly.Location),
        };

        foreach (var dir in candidates)
        {
            if (string.IsNullOrEmpty(dir)) continue;
            var wwwroot = Path.Combine(dir, "wwwroot");
            if (Directory.Exists(wwwroot) && File.Exists(Path.Combine(wwwroot, "index.html")))
            {
                Serilog.Log.Information("Web 内容根目录: {Dir}", dir);
                return dir;
            }
        }

        return AppContext.BaseDirectory;
    }

    public static void Main(string[] args)
    {
        CreateWebHost(args).Run();
    }
}
