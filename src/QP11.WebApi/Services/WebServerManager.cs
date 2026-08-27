using System.Threading;
using System.Threading.Tasks;

namespace QP11.WebApi.Services;

/// <summary>
/// Web 服务管理器 - 封装 Kestrel 启动/停止，供 WPF 端调用
/// 所有 ASP.NET Core 类型依赖都在此项目内解决
/// </summary>
public static class WebServerManager
{
    private static WebApplication? _app;
    private static Task? _runTask;
    private static CancellationTokenSource? _cts;

    /// <summary>服务是否正在运行</summary>
    public static bool IsRunning => _app != null && _runTask != null && !_runTask.IsCompleted;

    /// <summary>启动 Web 服务</summary>
    public static void Start(string[]? args = null)
    {
        if (IsRunning) return;
        try
        {
            _cts = new CancellationTokenSource();
            _app = Program.CreateWebHost(args ?? new[] { "--urls", "http://0.0.0.0:5000" });
            _runTask = Task.Run(async () =>
            {
                try { await _app.RunAsync(_cts.Token); }
                catch (OperationCanceledException) { }
                catch (System.Exception ex) { Serilog.Log.Error(ex, "Web 服务异常退出"); }
            }, _cts.Token);
            Serilog.Log.Information("Web 服务已启动，访问地址: http://0.0.0.0:5000");
        }
        catch (System.Exception ex)
        {
            Serilog.Log.Warning(ex, "Web 服务启动失败");
        }
    }

    /// <summary>停止 Web 服务（异步不阻塞）</summary>
    public static void Stop()
    {
        if (!IsRunning) return;
        Task.Run(async () =>
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _app!.StopAsync(cts.Token);
            }
            catch { }
            if (_cts != null && !_cts.IsCancellationRequested)
                _cts.Cancel();
        });
        Serilog.Log.Information("Web 服务正在停止...");
    }
}
