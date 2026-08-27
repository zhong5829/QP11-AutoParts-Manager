using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Entities;
using QP11.Core.AI;
using QP11.Core.Interfaces;
using QP11.Data.Infrastructure;
using QP11.Data.Repositories;
using QP11.Services;
using QP11.Services.AI;
using QP11.Services.AI.Abstractions;
using QP11.Services.AI.Tools;
using QP11.Services.Update;
using QP11.Wpf.ViewModels;
using QP11.Wpf.Views;
using Serilog;

namespace QP11.Wpf;

public partial class App : Application
{
    public static IServiceProvider ServiceProvider { get; private set; } = null!;
    public static UserInfor? CurrentUser { get; set; }
    public static PermissionService? PermissionService { get; private set; }
    public static UpdateService? UpdateService { get; private set; }

    /// <summary>Web 服务是否正在运行</summary>
    public static bool WebServiceIsRunning => QP11.WebApi.Services.WebServerManager.IsRunning;

    /// <summary>显示原生错误对话框（不依赖 WPF 窗口状态）</summary>
    private static void ShowError(string title, string message)
    {
        // 使用 Win32 MessageBox，在 WPF 启动前后都能正常显示
        var result = Interop.NativeMethods.MessageBoxW(
            IntPtr.Zero, message, title,
            0x10 /* MB_ICONERROR */ | 0x0 /* MB_OK */);
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // 最先设置：防止异常时静默退出
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        try
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.Debug(outputTemplate: "[{Level:u3}] {Message:lj}{NewLine}")
                .WriteTo.File(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs", "qp11-.log"),
                    rollingInterval: RollingInterval.Day,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
                .CreateLogger();
        }
        catch (Exception ex)
        {
            ShowError("QP11 启动错误", $"日志系统初始化失败:\n{ex.Message}");
            Shutdown();
            return;
        }

        Log.Information("QP11 应用启动");

        try
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            DapperTypeMapper.Register<UserInfor>();
            DapperTypeMapper.Register<PartData>();
            DapperTypeMapper.Register<ClientInfor>();
            DapperTypeMapper.Register<SupplierInfor>();
            DapperTypeMapper.Register<BillSell>();
            DapperTypeMapper.Register<DetailSell>();
            DapperTypeMapper.Register<BillBuy>();
            DapperTypeMapper.Register<DetailBuy>();
            DapperTypeMapper.Register<Account>();
            DapperTypeMapper.Register<Pays>();
            DapperTypeMapper.Register<Arrearage>();
            DapperTypeMapper.Register<PartStock>();
            DapperTypeMapper.Register<CarMark>();
            DapperTypeMapper.Register<SysLog>();
            DapperTypeMapper.Register<PartClass>();
            DapperTypeMapper.Register<Desktop>();
            DapperTypeMapper.Register<BillJhdh>();
            DapperTypeMapper.Register<DetailJhdh>();
            DapperTypeMapper.Register<BillBaosun>();
            DapperTypeMapper.Register<DetailBaosun>();

            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);

            // 数据库连接工厂
            services.AddSingleton<IDbConnectionFactory, DbConnectionFactory>();
            services.AddSingleton<IUnitOfWorkFactory, UnitOfWorkFactory>();
            services.AddSingleton<IDatabaseInfoService, DatabaseInfoService>();

            // 仓储 - 接口映射
            services.AddTransient<ISellRepository, SellRepository>();
            services.AddTransient<IBuyRepository, BuyRepository>();
            services.AddTransient<IPartRepository, PartRepository>();
            services.AddTransient<IClientRepository, ClientRepository>();
            services.AddTransient<ISupplierRepository, SupplierRepository>();
            services.AddTransient<IAccountRepository, AccountRepository>();
            services.AddTransient<IPaysRepository, PaysRepository>();
            services.AddTransient<IArrearageRepository, ArrearageRepository>();
            services.AddTransient<IMemberCardRepository, MemberCardRepository>();
            services.AddTransient<IBorrowRepository, BorrowRepository>();
            services.AddTransient<IUserRepository, UserRepository>();
            services.AddTransient<ISysLogRepository, SysLogRepository>();
            services.AddTransient<IBaosunRepository, BaosunRepository>();
            services.AddTransient<IPartBatchRepository, PartBatchRepository>();
            services.AddTransient<IRegionRepository, RegionRepository>();
            services.AddTransient<ILogisticsRepository, LogisticsRepository>();
            services.AddTransient<ICodeRuleRepository, CodeRuleRepository>();
            services.AddTransient<IPartLocationRepository, PartLocationRepository>();
            services.AddTransient<IDesktopRepository, DesktopRepository>();
            services.AddTransient<IJhdhRepository, JhdhRepository>();

            // 服务 - 接口映射
            services.AddSingleton<PermissionService>();
            services.AddTransient<IValidationService, ValidationService>();
            services.AddTransient<ICalcService, CalcService>();
            services.AddTransient<IAuthService, AuthService>();
            services.AddTransient<ISerialNumberService, SerialNumberService>();
            services.AddTransient<ISellService, SellService>();
            services.AddTransient<IBuyService, BuyService>();
            services.AddTransient<IJhdhService, JhdhService>();
            services.AddTransient<IFinanceService, FinanceService>();
            services.AddSingleton<IVinLocalMatchService, VinLocalMatchService>();

            // VIN查询服务 — 多数据源组合
            var useVinMock = bool.TryParse(configuration["VinQuery:UseMock"], out var mock) && mock;
            var pinxiuEnabled = !useVinMock && bool.TryParse(configuration["Pinxiu:Enabled"], out var pe) && pe;

            // 共享 HttpClient 实例，避免 Socket 耗尽
            var vinHttpClient = new HttpClient();

            if (useVinMock)
            {
                services.AddSingleton<IVinQueryService, MockVinQueryService>();
            }
            else
            {
                // 注册318car数据源
                services.AddSingleton<IVinDataSource>(_ => new VinQueryService(vinHttpClient, configuration));

                // 注册品秀数据源（受Enabled开关控制）
                if (pinxiuEnabled)
                {
                    services.AddSingleton<IVinDataSource>(_ => new PinxiuDataSource(vinHttpClient, configuration));
                }

                // 注册组合服务
                services.AddSingleton<IVinQueryService>(sp =>
                {
                    var sources = sp.GetServices<IVinDataSource>().ToList();
                    return new CompositeVinQueryService(sources);
                });
            }

            // 无接口的服务 - 直接注册
            services.AddTransient<ExportService>();
            services.AddSingleton<MigrationService>();

            // ViewModel
            services.AddTransient<SellViewModel>();
            services.AddTransient<BuyViewModel>();
            services.AddTransient<SellReturnViewModel>();
            services.AddTransient<SellQueryViewModel>();
            services.AddTransient<BuyQueryViewModel>();
            services.AddTransient<AccountViewModel>();
            services.AddTransient<BaosunViewModel>();
            services.AddTransient<BorrowViewModel>();

            // Agnes AI 助手
            var agnesSection = configuration.GetSection("Agnes");
            var agnesOptions = new AgnesOptions
            {
                Provider = agnesSection["Provider"] ?? "DeepSeek",
                BaseUrl = agnesSection["BaseUrl"] ?? "https://api.deepseek.com/v1",
                ApiKey = agnesSection["ApiKey"] ?? "YOUR_DEEPSEEK_API_KEY",
                Model = agnesSection["Model"] ?? "deepseek-chat",
                EnableStreaming = bool.TryParse(agnesSection["EnableStreaming"], out var es) ? es : true,
                MaxHistoryMessages = int.TryParse(agnesSection["MaxHistoryMessages"], out var mhm) ? mhm : 20,
                MaxToolRounds = int.TryParse(agnesSection["MaxToolRounds"], out var mtr) ? mtr : 5,
                RequestTimeoutSeconds = int.TryParse(agnesSection["RequestTimeoutSeconds"], out var rts) ? rts : 120,
                OfflineFallback = bool.TryParse(agnesSection["OfflineFallback"], out var of) ? of : true,
                Temperature = double.TryParse(agnesSection["Temperature"], out var temp) ? temp : 0.3,
                MaxTokens = int.TryParse(agnesSection["MaxTokens"], out var mt) ? mt : 2048
            };
            services.AddSingleton(agnesOptions);
            services.AddSingleton<HttpClient>(sp =>
            {
                var opt = sp.GetRequiredService<AgnesOptions>();
                return new HttpClient { Timeout = TimeSpan.FromSeconds(opt.RequestTimeoutSeconds) };
            });
            services.AddSingleton<AgnesAuditor>();
            services.AddSingleton<IChatClient, DeepSeekChatClient>();
            services.AddSingleton<IToolRegistry, ToolRegistry>();
            services.AddTransient<AgnesOrchestrator>();
            services.AddTransient<IPartQueryService, PartQueryService>();
            services.AddTransient<IChatTool, SearchPartsTool>();
            services.AddTransient<IChatTool, GetStockTool>();
            services.AddTransient<IChatTool, GetStockAdvancedTool>();
            services.AddTransient<IChatTool, GetPartPriceTool>();
            services.AddTransient<IChatTool, GetSellHistoryTool>();
            services.AddTransient<IChatTool, GetBuyHistoryTool>();
            services.AddTransient<AgnesChatViewModel>();

            ServiceProvider = services.BuildServiceProvider();

            // 初始化静态服务
            PermissionService = ServiceProvider.GetRequiredService<PermissionService>();

            // 初始化更新服务
            InitializeUpdateService(configuration);

            Log.Information("DI容器初始化完成，显示登录窗口");

            var login = new LoginWindow(
                ServiceProvider.GetRequiredService<IAuthService>(),
                ServiceProvider.GetRequiredService<IUserRepository>(),
                ServiceProvider.GetRequiredService<IDatabaseInfoService>());
            bool? loginResult = false;
            try
            {
                loginResult = login.ShowDialog();
            }
            catch (Exception ex)
            {
                Log.Error(ex, "登录窗口异常");
                ShowError("QP11 错误", $"登录窗口异常:\n{ex.Message}");
                Shutdown();
                return;
            }

            Log.Information("登录窗口关闭，结果: {Result}", loginResult);

            if (loginResult == true && login.CurrentUser != null)
            {
                CurrentUser = login.CurrentUser;
                Log.Information("用户 {Username} 登录成功，创建主窗口", CurrentUser.Username);

                try
                {
                    var main = new MainWindow(login.CurrentUser);
                    MainWindow = main;
                    ShutdownMode = ShutdownMode.OnMainWindowClose;
                    main.Show();
                    Log.Information("主窗口已显示");

                    // 异步检查更新，不阻塞启动
                    _ = CheckUpdateAsync();
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "创建主窗口失败");
                    ShowError("QP11 错误", $"创建主窗口失败:\n{ex.Message}\n\n{ex.InnerException?.Message}");
                    Shutdown();
                }
            }
            else
            {
                Log.Information("用户取消登录，退出应用");
                Shutdown();
            }
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "应用启动失败");
            ShowError("QP11 错误", $"启动失败:\n{ex.Message}\n\n{ex.InnerException?.Message}");
            Shutdown();
        }
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "UI线程未处理异常");
        MessageBox.Show($"发生错误:\n{e.Exception.Message}\n\n{e.Exception.InnerException?.Message}",
            "QP11 错误", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            Log.Fatal(ex, "未处理异常(非UI线程)");
            var msg = ex is System.IO.IOException && ex.HResult == -2147024864
                ? $"文件被占用，无法读取。\n\n请关闭正在使用该文件的程序（如 Excel）后重试。"
                : $"发生严重错误:\n{ex.Message}";
            MessageBox.Show(msg, "QP11 错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "后台任务未处理异常");
        e.SetObserved();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // 停止内嵌 Web 服务
        StopWebServer();

        // 释放更新服务资源（HttpClient）
        (UpdateService as IDisposable)?.Dispose();

        Log.Information("QP11 应用退出，退出码: {Code}", e.ApplicationExitCode);
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    /// <summary>
    /// 在后台线程启动内嵌 ASP.NET Core Kestrel 服务器
    /// 用户通过浏览器访问 http://本机IP:5000 即可使用销售开单网页版
    /// </summary>
    public static void StartWebServer()
    {
        QP11.WebApi.Services.WebServerManager.Start(new[] { "--urls", "http://0.0.0.0:5000" });
    }

    public static void StopWebServer()
    {
        QP11.WebApi.Services.WebServerManager.Stop();
    }

    /// <summary>初始化更新服务（读取 Gitee 配置）</summary>
    private void InitializeUpdateService(IConfiguration configuration)
    {
        var updateSection = configuration.GetSection("UpdateSettings");
        var client = new GiteeReleaseClient
        {
            Owner = updateSection["GiteeOwner"] ?? string.Empty,
            Repo = updateSection["GiteeRepo"] ?? string.Empty,
            AccessToken = updateSection["GiteeAccessToken"] ?? string.Empty
        };
        UpdateService = new UpdateService(client)
        {
            ShutdownApp = () => Dispatcher.Invoke(() => Shutdown()),
            AccessToken = client.AccessToken
        };
    }

    /// <summary>异步检查更新</summary>
    private async Task CheckUpdateAsync()
    {
        try
        {
            if (UpdateService == null) return;
            var update = await UpdateService.CheckUpdateAsync();
            if (update == null) return;

            // 检查用户是否已跳过此版本
            if (!update.Mandatory && UpdateWindow.IsVersionSkipped(update.Version))
                return;

            await Dispatcher.InvokeAsync(() =>
            {
                var dialog = new UpdateWindow(update, UpdateService);
                dialog.Owner = MainWindow;
                dialog.ShowDialog();
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "检查更新失败");
        }
    }
}
