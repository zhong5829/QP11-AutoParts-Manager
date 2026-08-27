using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using QP11.Services;

namespace QP11.Wpf.Views;

public partial class DesktopControl : UserControl, ITabContent
{
    private readonly ISellRepository _sellRepo;
    private readonly IBuyRepository _buyRepo;
    private readonly IPartRepository _partRepo;
    private DispatcherTimer? _dashboardTimer;

    private static readonly Dictionary<string, string> TagTitleMap = new()
    {
        ["11"] = "采购进货", ["12"] = "计划订货", ["118"] = "采购退货",
        ["13"] = "销售开单", ["136"] = "快捷开单", ["133"] = "销售查询", ["138"] = "销售退货",
        ["15"] = "查看库存", ["151"] = "仓库盘点", ["156"] = "库存预警", ["16"] = "单据打印",
        ["31"] = "采购明细", ["33"] = "销售明细", ["36"] = "进销存报表", ["37"] = "营业报表",
        ["21"] = "应付款", ["22"] = "应收款", ["24"] = "现金账", ["25"] = "银行账",
    };

    public string TabTitle => "桌面导航";
    public bool HasUnsavedChanges => false;
    public event EventHandler? RequestClose;

    public DesktopControl(
        ISellRepository sellRepo,
        IBuyRepository buyRepo,
        IPartRepository partRepo)
    {
        _sellRepo = sellRepo;
        _buyRepo = buyRepo;
        _partRepo = partRepo;
        InitializeComponent();
    }

    private void UserControl_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyButtonPermissions();
        RefreshWebStatus();

        // 异步加载数据，不阻塞页面渲染（3 个 DB 查询并行，SQL Server 2000 较慢）
        _ = LoadDashboardAsync();

        // 60 秒自动刷新工作台数据
        _dashboardTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(60) };
        _dashboardTimer.Tick += async (_, _) => await LoadDashboardAsync();
        _dashboardTimer.Start();
    }

    private void UserControl_Unloaded(object sender, RoutedEventArgs e)
    {
        _dashboardTimer?.Stop();
        _dashboardTimer = null;
    }

    private void ApplyButtonPermissions()
    {
        var perm = App.PermissionService;
        if (perm == null || perm.IsSuperAdmin) return;

        ApplyButtonPermissionsRecursive(this, perm);
    }

    private static void ApplyButtonPermissionsRecursive(DependencyObject parent, PermissionService perm)
    {
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is Button btn)
            {
                var tag = btn.Tag?.ToString();
                if (!string.IsNullOrEmpty(tag) && tag.Length > 0 && char.IsDigit(tag[0]))
                {
                    if (!perm.HasPermission(tag))
                    {
                        btn.IsEnabled = false;
                    }
                }
            }
            ApplyButtonPermissionsRecursive(child, perm);
        }
    }

    /// <summary>加载工作台仪表板数据（KPI + 列表），在后台线程执行数据库查询避免阻塞 UI</summary>
    private async Task LoadDashboardAsync()
    {
        try
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            // 在线程池线程上执行所有数据库查询（ODBC 的 OpenAsync 实际是同步阻塞，不能在 UI 线程上启动）
            var data = await Task.Run(async () =>
            {
                var sellTask = _sellRepo.GetListAsync(today, tomorrow);
                var buyTask = _buyRepo.GetListAsync(today, tomorrow);
                var rankingTask = _sellRepo.GetTodayPartsRankingAsync(today, 10);

                await Task.WhenAll(sellTask, buyTask, rankingTask);

                var sells = sellTask.Result.ToList();
                var buys = buyTask.Result.ToList();
                var ranking = rankingTask.Result.ToList();

                var validBuys = buys.Where(b => b.Flag != -1).ToList();

                // 给排行数据加上排名序号（SQL Server 2000 不支持 ROW_NUMBER）
                var ranked = ranking.Select((r, i) =>
                {
                    var dict = (IDictionary<string, object>)r;
                    dict["Rank"] = i + 1;
                    return r;
                }).ToList();

                return new
                {
                    Sells = sells,
                    ValidBuys = validBuys,
                    PartsRanking = ranked
                };
            });

            // 回到 UI 线程更新控件
            txtTodaySell.Text = $"¥{data.Sells.Sum(s => s.Total ?? 0):N2}";
            txtTodaySellCount.Text = $"{data.Sells.Count} 笔";
            txtTodayBuy.Text = $"¥{data.ValidBuys.Sum(b => b.Total ?? 0):N2}";
            txtTodayBuyCount.Text = $"{data.ValidBuys.Count} 笔";
            dgRecentSells.ItemsSource = data.Sells
                .OrderByDescending(s => s.Datetime)
                .Take(10)
                .ToList();
            dgPartsRanking.ItemsSource = data.PartsRanking;
            txtLastRefresh.Text = $"更新于 {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "加载工作台数据失败");
            Dispatcher.Invoke(() => txtLastRefresh.Text = "加载失败");
        }
    }

    // ========== 左侧导航按钮点击 ==========
    private void DesktopButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            OnDesktopButtonClick(tag);
        }
    }

    private void OnDesktopButtonClick(string tag)
    {
        var title = TagTitleMap.TryGetValue(tag, out var t) ? t : tag;
        var mainWin = Window.GetWindow(this) as MainWindow;
        mainWin?.OpenFunctionTab(tag, title);
    }

    // ========== KPI 卡片点击跳转 ==========
    private void KpiCard_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            OnDesktopButtonClick(tag);
        }
    }

    // ========== 双栏列表双击跳转 ==========
    private void DgRecentSells_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        OnDesktopButtonClick("133");
    }

    private async void BtnRefreshDashboard_Click(object sender, RoutedEventArgs e)
    {
        await LoadDashboardAsync();
    }

    // ========== Web 服务管理 ==========
    private void BtnWebStart_Click(object sender, RoutedEventArgs e)
    {
        if (App.WebServiceIsRunning)
        {
            MessageBox.Show("Web 服务已在运行中", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        App.StartWebServer();
        RefreshWebStatus();
    }

    private void BtnWebStop_Click(object sender, RoutedEventArgs e)
    {
        if (!App.WebServiceIsRunning)
        {
            MessageBox.Show("Web 服务未运行", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (MessageBox.Show("确定停止 Web 服务？停止后手机/平板将无法访问开单页面。",
            "确认停止", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        App.StopWebServer();
        RefreshWebStatus(); // 立即显示"正在停止"
        // 延迟1秒后再次刷新，确保异步停止完成
        DispatcherTimer? timer = null;
        timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => { timer.Stop(); RefreshWebStatus(); };
        timer.Start();
    }

    private void BtnWebStatus_Click(object sender, RoutedEventArgs e) => RefreshWebStatus();

    /// <summary>刷新 Web 服务状态显示</summary>
    private void RefreshWebStatus()
    {
        var running = App.WebServiceIsRunning;
        var connections = QP11.WebApi.Services.ConnectionCounter.ActiveCount;

        txtWebConnections.Text = connections.ToString();

        if (running)
        {
            txtWebStatus.Text = "● 运行中\n   访问地址: http://本机IP:5000";
            txtWebStatus.Foreground = System.Windows.Media.Brushes.Green;
            btnWebStart.IsEnabled = false;
            btnWebStop.IsEnabled = true;
        }
        else
        {
            txtWebStatus.Text = "○ 未运行\n   点击「启动服务」启动 Web 服务";
            txtWebStatus.Foreground = System.Windows.Media.Brushes.Gray;
            btnWebStart.IsEnabled = true;
            btnWebStop.IsEnabled = false;
        }
    }

    #region ITabContent

    public void OnAdd() { }
    public void OnEdit() { }

    public async void OnQuery()
    {
        await LoadDashboardAsync();
    }

    public void OnDelete() { }
    public void OnSave() { }
    public void OnSettle() { }
    public void OnPrint() { }
    public void OnReturn() { }
    public void OnCancel() { }
    public void OnHistory() { }
    public void OnClose() => RequestClose?.Invoke(this, EventArgs.Empty);

    #endregion
}
