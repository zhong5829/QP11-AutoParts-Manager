using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using QP11.Services;
using QP11.Wpf.ViewModels;

namespace QP11.Wpf.Views;

public partial class MainWindow : Window
{
    private UserInfor? _currentUser;
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<string, TabItem> _openTabs = new();
    private VinQueryWindow? _vinQueryWindow;

    public UserInfor? CurrentUser => _currentUser;

    public MainWindow(UserInfor user)
    {
        InitializeComponent();
        LoadWindowIcon();
        _currentUser = user;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (s, e) => txtTime.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _timer.Start();

        KeyDown += MainWindow_KeyDown;
    }

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F1) { ExecuteOnActiveTab(c => c.OnAdd()); e.Handled = true; }
        else if (e.Key == Key.F2) { ExecuteOnActiveTab(c => c.OnEdit()); e.Handled = true; }
        else if (e.Key == Key.F3) { ExecuteOnActiveTab(c => c.OnQuery()); e.Handled = true; }
        else if (e.Key == Key.F4) { ExecuteOnActiveTab(c => c.OnDelete()); e.Handled = true; }
        else if (e.Key == Key.F5) { ExecuteOnActiveTab(c => c.OnSave()); e.Handled = true; }
        else if (e.Key == Key.F6) { ExecuteOnActiveTab(c => c.OnSettle()); e.Handled = true; }
        else if (e.Key == Key.F7) { ExecuteOnActiveTab(c => c.OnPrint()); e.Handled = true; }
        else if (e.Key == Key.F8) { ExecuteOnActiveTab(c => c.OnReturn()); e.Handled = true; }
        else if (e.Key == Key.F9) { ExecuteOnActiveTab(c => c.OnCancel()); e.Handled = true; }
        else if (e.Key == Key.F11) { ExecuteOnActiveTab(c => c.OnHistory()); e.Handled = true; }
        else if (e.Key == Key.F12) { ExecuteOnActiveTab(c => c.OnClose()); e.Handled = true; }
    }

    private void LoadWindowIcon()
    {
        try
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(path))
            {
                var uri = new Uri(path, UriKind.Absolute);
                var decoder = new IconBitmapDecoder(uri, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
                if (decoder.Frames.Count > 0)
                    Icon = decoder.Frames[0];
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "加载窗口图标失败");
        }
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        txtOperator.Text = $"操作员：{_currentUser?.Name ?? _currentUser?.Username}";
        txtPermission.Text = $"权限：{GetPermissionText(_currentUser?.Groups)}";
        txtTime.Text = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        ApplyMenuPermissions();
        // 桌面导航延迟加载，让主窗口先完成渲染
        Dispatcher.BeginInvoke(() =>
        {
            OpenTab("desktop", "桌面导航", new DesktopControl(
                App.ServiceProvider.GetRequiredService<ISellRepository>(),
                App.ServiceProvider.GetRequiredService<IBuyRepository>(),
                App.ServiceProvider.GetRequiredService<IPartRepository>()));
        }, DispatcherPriority.Loaded);
    }

    /// <summary>
    /// 返回新系统菜单权限树（按主窗口菜单结构映射），供权限管理窗口使用：
    /// 顶层组按 Header 映射旧系统父码（进销存=1、财务=2、业务查询=3、基础数据=4、系统维护=7），
    /// 叶子节点权限码取数字开头的菜单 Tag。
    /// </summary>
    public List<MenuNode> GetMenuPermissionTree()
    {
        var roots = new List<MenuNode>();
        foreach (var item in mainMenu.Items)
        {
            if (item is MenuItem mi && BuildMenuPermNode(mi) is { } node)
                roots.Add(node);
        }
        return roots;
    }

    private static MenuNode? BuildMenuPermNode(MenuItem mi)
    {
        var tag = mi.Tag?.ToString();
        var code = !string.IsNullOrEmpty(tag) && char.IsDigit(tag[0]) ? tag : null;

        var children = mi.Items.OfType<MenuItem>()
            .Select(BuildMenuPermNode)
            .Where(n => n != null)
            .Cast<MenuNode>()
            .ToList();

        // 顶层组无 Tag，按标题映射旧系统父码，便于授权整组
        var header = (string)mi.Header;
        code ??= header switch
        {
            _ when header.StartsWith("进销存管理") => "1",
            _ when header.StartsWith("财务管理") => "2",
            _ when header.StartsWith("业务查询") => "3",
            _ when header.StartsWith("基础数据") => "4",
            _ when header.StartsWith("系统管理") => "7",
            _ => null
        };

        if (code == null && children.Count == 0) return null; // 无码项（退出/会员/AI 等不参与权限）
        var node = new MenuNode { Code = code, Name = header };
        node.Children.AddRange(children);
        return node;
    }

    private static string GetPermissionText(int? groups)
    {
        return groups switch
        {
            1 => "超级管理员",
            2 => "管理员",
            3 => "操作员",
            4 => "只读",
            _ => groups?.ToString() ?? "未知"
        };
    }

    /// <summary>
    /// 权限变更后刷新菜单与工作台按钮权限状态（无需重新登录）。
    /// 先恢复全部权限菜单为启用，再按最新权限重新禁用。
    /// </summary>
    public void RefreshAllPermissionUi()
    {
        EnableAllMenuItems(mainMenu.Items);
        ApplyMenuPermissions();

        foreach (var item in mainTab.Items)
        {
            if (item is TabItem ti && ti.Content is DesktopControl dc)
                dc.RefreshButtonPermissions();
        }
    }

    private static void EnableAllMenuItems(ItemCollection items)
    {
        foreach (var item in items)
        {
            if (item is MenuItem mi)
            {
                var tag = mi.Tag?.ToString();
                if (!string.IsNullOrEmpty(tag) && tag.Length > 0 && char.IsDigit(tag[0]))
                    mi.IsEnabled = true;
                if (mi.Items.Count > 0)
                    EnableAllMenuItems(mi.Items);
            }
        }
    }

    private void ApplyMenuPermissions()
    {
        var perm = App.PermissionService;
        if (perm == null || perm.IsSuperAdmin) return;

        ApplyMenuPermissionsRecursive(mainMenu.Items, perm);
    }

    private void ApplyMenuPermissionsRecursive(ItemCollection items, PermissionService perm)
    {
        foreach (var item in items)
        {
            if (item is MenuItem menuItem)
            {
                var tag = menuItem.Tag?.ToString();
                if (!string.IsNullOrEmpty(tag) && tag.Length > 0 && char.IsDigit(tag[0]))
                {
                    if (!perm.HasPermission(tag))
                    {
                        menuItem.IsEnabled = false;
                    }
                }

                if (menuItem.Items.Count > 0)
                {
                    ApplyMenuPermissionsRecursive(menuItem.Items, perm);
                }
            }
        }
    }

    #region Tab Management

    public void OpenTab(string tag, string title, UserControl content)
    {
        if (_openTabs.TryGetValue(tag, out var existingTab))
        {
            mainTab.SelectedItem = existingTab;
            return;
        }

        var tabItem = new TabItem
        {
            Header = title,
            Content = content,
            Tag = tag,
            Style = (Style)mainTab.Resources["ClosableTabItemStyle"]
        };

        if (content is ITabContent tabContent)
        {
            tabContent.RequestClose += (s, e) => CloseTab(tabItem);
        }

        _openTabs[tag] = tabItem;
        mainTab.Items.Add(tabItem);
        mainTab.SelectedItem = tabItem;
    }

    /// <summary>
    /// 打开销售退货编辑Tab（从销售查询页面编辑退货单时调用）
    /// </summary>
    public void OpenReturnEditTab(string sn)
    {
        var tag = "sellReturn_edit_" + sn;
        var ctrl = new SellReturnControl(App.ServiceProvider.GetRequiredService<SellReturnViewModel>());
        OpenTab(tag, "编辑退货-" + sn, ctrl);
        ctrl.LoadBillForEdit(sn);
    }

    public void OpenFunctionTab(string tag, string title)
    {
        // VIN查询作为独立非模态窗口打开（单例），不走Tab
        if (tag == "vin1")
        {
            OpenVinQueryWindow();
            return;
        }

        UserControl content = tag switch
        {
            "11" => new BuyControl(App.ServiceProvider.GetRequiredService<BuyViewModel>()),
            "118" => new BuyReturnWindow(
                App.ServiceProvider.GetRequiredService<IBuyService>(),
                App.ServiceProvider.GetRequiredService<ISupplierRepository>()),
            "13" => new SellControl(App.ServiceProvider.GetRequiredService<SellViewModel>()),
            "12" => new WindowHostControl("计划订货", () => new PurchaseOrderWindow(_currentUser,
                App.ServiceProvider.GetRequiredService<IJhdhRepository>(),
                App.ServiceProvider.GetRequiredService<IJhdhService>(),
                App.ServiceProvider.GetRequiredService<IPartRepository>(),
                App.ServiceProvider.GetRequiredService<ISupplierRepository>(),
                App.ServiceProvider.GetRequiredService<IUserRepository>(),
                App.ServiceProvider.GetRequiredService<IDbConnectionFactory>())),
            "15" => new WindowHostControl("查看库存", () => new InventoryWindow(
                App.ServiceProvider.GetRequiredService<IPartRepository>(),
                App.ServiceProvider.GetRequiredService<ExportService>())),
            "151" => new WindowHostControl("仓库盘点", () => new StockCheckWindow(
                App.ServiceProvider.GetRequiredService<IPartRepository>(),
                App.ServiceProvider.GetRequiredService<IDbConnectionFactory>(),
                App.ServiceProvider.GetRequiredService<IUnitOfWorkFactory>(),
                App.ServiceProvider.GetRequiredService<ExportService>())),
            "156" => new WindowHostControl("库存预警", () => new StockAlertWindow(
                App.ServiceProvider.GetRequiredService<IPartRepository>(),
                App.ServiceProvider.GetRequiredService<IPartQueryService>())),
            "133" => new SellControl(App.ServiceProvider.GetRequiredService<SellViewModel>()),
            "138" => new SellReturnControl(App.ServiceProvider.GetRequiredService<SellReturnViewModel>()),
            "17" => new BaosunControl(App.ServiceProvider.GetRequiredService<BaosunViewModel>()),
            "18" => new BorrowControl(App.ServiceProvider.GetRequiredService<BorrowViewModel>()),
            "21" => new ArrearageControl(App.ServiceProvider.GetRequiredService<IArrearageRepository>(), App.ServiceProvider.GetRequiredService<IFinanceService>(), 1),
            "22" => new ArrearageControl(App.ServiceProvider.GetRequiredService<IArrearageRepository>(), App.ServiceProvider.GetRequiredService<IFinanceService>(), 2),
            "24" => new AccountControl(App.ServiceProvider.GetRequiredService<AccountViewModel>(), "现金账"),
            "25" => new AccountControl(App.ServiceProvider.GetRequiredService<AccountViewModel>(), "银行账"),
            "26" => new AccountControl(App.ServiceProvider.GetRequiredService<AccountViewModel>(), "支付宝账"),
            "27" => new AccountControl(App.ServiceProvider.GetRequiredService<AccountViewModel>(), "微信账"),
            "28" => new AccountControl(App.ServiceProvider.GetRequiredService<AccountViewModel>(), "运费账"),
            "29" => new AccountControl(App.ServiceProvider.GetRequiredService<AccountViewModel>(), "日结账"),
            "31" => new BuyQueryControl(App.ServiceProvider.GetRequiredService<BuyQueryViewModel>()),
            "32" => new BuyQueryControl(App.ServiceProvider.GetRequiredService<BuyQueryViewModel>()),
            "33" => new SellQueryControl(App.ServiceProvider.GetRequiredService<SellQueryViewModel>()),
            "35" => new WindowHostControl("销售汇总", () => new ReportCenterWindow()),
            "36" => new WindowHostControl("进销存报表", () => new ReportCenterWindow()),
            "37" => new WindowHostControl("营业报表", () => new ReportCenterWindow()),
            "39" => new WindowHostControl("报表中心", () => new ReportCenterWindow()),
            "38" => new WindowHostControl("排行榜", () => new RankingWindow()),
            "41" => new WindowHostControl("客户管理", () => new ClientManagerWindow(
                App.ServiceProvider.GetRequiredService<IClientRepository>())),
            "42" => new WindowHostControl("供应商管理", () => new SupplierManagerWindow(
                App.ServiceProvider.GetRequiredService<ISupplierRepository>())),
            "43" => new WindowHostControl("员工管理", () => new UserManagerWindow(
                App.ServiceProvider.GetRequiredService<IUserRepository>())),
            "44" => new BasicDataControl(),
            "45" => new WindowHostControl("品牌管理", () => new PartClassWindow()),
            "m1" => new WindowHostControl("会员管理", () => new MemberCardWindow(
                App.ServiceProvider.GetRequiredService<IMemberCardRepository>())),
            "m2" => new WindowHostControl("会员卡充值", () => new MemberCardWindow(
                App.ServiceProvider.GetRequiredService<IMemberCardRepository>())),
            "m3" => new WindowHostControl("往来账管理", () => new MemberTransactionWindow(
                App.ServiceProvider.GetRequiredService<IArrearageRepository>())),
            "46" => new WindowHostControl("物流公司", () => new LogisticsWindow(
                App.ServiceProvider.GetRequiredService<ILogisticsRepository>())),
            "47" => new WindowHostControl("库位管理", () => new LocationWindow(
                App.ServiceProvider.GetRequiredService<IPartLocationRepository>())),
            "48" => new WindowHostControl("拼音修复", () => new PinyinFixWindow(
                App.ServiceProvider.GetRequiredService<IPartRepository>())),
            "71" => new WindowHostControl("数据备份", () => new SettingsWindow(App.ServiceProvider.GetRequiredService<IDatabaseInfoService>())),
            "7d" => new WindowHostControl("数据恢复", () => new SettingsWindow(App.ServiceProvider.GetRequiredService<IDatabaseInfoService>())),
            "72" => new WindowHostControl("操作员管理", () => new UserManagerWindow(
                App.ServiceProvider.GetRequiredService<IUserRepository>())),
            "73" => new WindowHostControl("系统日志", () => new SysLogWindow(
                App.ServiceProvider.GetRequiredService<ISysLogRepository>(),
                App.ServiceProvider.GetRequiredService<IUserRepository>())),
            "78" => new WindowHostControl("权限管理", () => new RolePermissionWindow()),
            "7f" => new WindowHostControl("系统参数设置", () => new SettingsWindow(App.ServiceProvider.GetRequiredService<IDatabaseInfoService>())),
            "r5" => new WindowHostControl("单据打印设置", () => new BillPrintSettingsWindow()),
            "7e" => new WindowHostControl("打印设置", () => new PrintSetupWindow()),
            "r6" => new WindowHostControl("标签打印", () => new LabelPrintWindow(
                App.ServiceProvider.GetRequiredService<IPartRepository>())),
            "7g" => new WindowHostControl("数据迁移", () => new MigrationWindow(
                App.ServiceProvider.GetRequiredService<MigrationService>())),
            "agnes" => new AgnesAssistantTab(App.ServiceProvider.GetRequiredService<AgnesChatViewModel>()),
            _ => CreateTabContent(title)
        };
        var tabTitle = tag switch
        {
            "11" => "采购进货",
            "118" => "采购退货",
            "13" => "销售开单",
            "133" => "销售查询",
            "138" => "销售退货",
            "17" => "报损出库",
            "18" => "借还管理",
            "21" => "应付款",
            "22" => "应收款",
            "24" => "现金账",
            "25" => "银行账",
            "26" => "支付宝账",
            "27" => "微信账",
            "28" => "运费账",
            "29" => "日结账",
            "12" => "计划订货",
            "15" => "查看库存",
            "151" => "仓库盘点",
            "156" => "库存预警",
            "31" => "采购明细",
            "32" => "计划明细",
            "33" => "销售明细",
            "35" => "销售汇总",
            "36" => "进销存报表",
            "37" => "营业报表",
            "39" => "报表中心",
            "38" => "排行榜",
            "41" => "客户管理",
            "42" => "供应商管理",
            "43" => "员工管理",
            "44" => "配件管理",
            "45" => "品牌管理",
            "m1" => "会员管理",
            "m2" => "会员卡充值",
            "m3" => "往来账管理",
            "46" => "物流公司",
            "47" => "库位管理",
            "48" => "拼音修复",
            "71" => "数据备份",
            "7d" => "数据恢复",
            "72" => "操作员管理",
            "73" => "系统日志",
            "78" => "权限管理",
            "7f" => "系统参数设置",
            "r5" => "单据打印设置",
            "7e" => "打印设置",
            "7g" => "数据迁移",
            "agnes" => "Agnes AI 助手",
            _ => title
        };
        OpenTab(tag, tabTitle, content);

        // 查询类标签页：自动进入查询模式
        if (content is ITabContent tc && tag == "133")
            Dispatcher.BeginInvoke(() => tc.OnQuery(), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    public void CloseTab(TabItem tab)
    {
        if (tab == null) return;

        if (tab.Content is ITabContent tabContent && tabContent.HasUnsavedChanges)
        {
            var result = MessageBox.Show("当前有未保存的更改，确定关闭？", "确认关闭",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;
        }

        var tag = tab.Tag as string;
        if (tag != null)
            _openTabs.Remove(tag);

        mainTab.Items.Remove(tab);
    }

    public TabItem? FindTabByContent(UserControl content)
    {
        foreach (TabItem tab in mainTab.Items)
        {
            if (ReferenceEquals(tab.Content, content))
                return tab;
        }
        return null;
    }

    private void CloseTabButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is TabItem tabItem)
        {
            CloseTab(tabItem);
        }
    }

    private void MainTab_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
    }

    private ITabContent? GetActiveTabContent()
    {
        if (mainTab.SelectedItem is TabItem tab && tab.Content is ITabContent content)
            return content;
        return null;
    }

    private void OpenVinQueryWindow()
    {
        // VIN查询作为独立非模态窗口打开（单例）
        if (_vinQueryWindow != null && _vinQueryWindow.IsLoaded)
        {
            _vinQueryWindow.Activate();
            return;
        }

        var vinService = App.ServiceProvider.GetRequiredService<IVinQueryService>();
        var localMatchService = App.ServiceProvider.GetRequiredService<IVinLocalMatchService>();
        _vinQueryWindow = new VinQueryWindow(vinService, localMatchService, this);
        _vinQueryWindow.Title = "VIN查询";
        // 不设置Owner，使VIN窗口与主窗口平级，可自由切换焦点
        _vinQueryWindow.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        _vinQueryWindow.Closed += (s, e) => _vinQueryWindow = null;
        _vinQueryWindow.Show(); // 非模态！不阻塞主窗口
    }

    /// <summary>工具栏VIN查询按钮</summary>
    private void ToolbarVinQuery_Click(object sender, RoutedEventArgs e)
    {
        SendVinFromToolbar();
    }

    /// <summary>工具栏VIN输入框回车</summary>
    private void TxtVinToolbar_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            SendVinFromToolbar();
        }
    }

    /// <summary>从工具栏发送VIN到查询窗口</summary>
    private void SendVinFromToolbar()
    {
        var vin = txtVinToolbar.Text.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(vin)) return;

        if (_vinQueryWindow == null || !_vinQueryWindow.IsLoaded)
        {
            MessageBox.Show("请先打开VIN查询窗口（菜单 → VIN查询）", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        txtVinToolbar.Clear();
        _vinQueryWindow.QueryFromExternal(vin);
    }

    /// <summary>获取当前活跃的销售开单控件（供VinQueryWindow调用）</summary>
    public SellControl? GetActiveSellControl()
    {
        // 优先找当前选中Tab中的SellControl
        if (mainTab.SelectedItem is TabItem { Content: SellControl selectedSell })
            return selectedSell;

        // 其次查找任意打开的SellControl Tab
        if (_openTabs.TryGetValue("13", out var sellTab) && sellTab.Content is SellControl sellControl)
            return sellControl;

        return null;
    }

    private void ExecuteOnActiveTab(Action<ITabContent> action)
    {
        var content = GetActiveTabContent();
        if (content != null)
            action(content);
    }

    #endregion

    #region Menu Handlers

    private void MenuGeneric_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem mi) return;
        var tag = mi.Tag as string;
        var title = mi.Header as string;

        if (string.IsNullOrEmpty(tag) || string.IsNullOrEmpty(title))
            return;

        if (tag == "r4")
        {
            OpenTab("desktop", "桌面导航", new DesktopControl(
                App.ServiceProvider.GetRequiredService<ISellRepository>(),
                App.ServiceProvider.GetRequiredService<IBuyRepository>(),
                App.ServiceProvider.GetRequiredService<IPartRepository>()));
            return;
        }

        OpenFunctionTab(tag, title);
    }

    private UserControl CreateTabContent(string title)
    {
        var placeholder = new UserControl();
        var grid = new Grid();
        var textBlock = new TextBlock
        {
            Text = title,
            FontSize = 24,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = SystemColors.GrayTextBrush
        };
        grid.Children.Add(textBlock);
        placeholder.Content = grid;
        return placeholder;
    }

    private void MenuPassword_Click(object sender, RoutedEventArgs e)
    {
        var win = new PasswordWindow(_currentUser!, App.ServiceProvider.GetRequiredService<IUserRepository>()) { Owner = this };
        win.ShowDialog();
    }

    private void MenuExit_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("确定退出系统?", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
        {
            Application.Current.Shutdown();
        }
    }

    #endregion

    #region Toolbar Handlers

    private void ToolbarAdd_Click(object sender, RoutedEventArgs e) => ExecuteOnActiveTab(c => c.OnAdd());
    private void ToolbarEdit_Click(object sender, RoutedEventArgs e) => ExecuteOnActiveTab(c => c.OnEdit());
    private void ToolbarQuery_Click(object sender, RoutedEventArgs e) => ExecuteOnActiveTab(c => c.OnQuery());
    private void ToolbarDelete_Click(object sender, RoutedEventArgs e) => ExecuteOnActiveTab(c => c.OnDelete());
    private void ToolbarSave_Click(object sender, RoutedEventArgs e) => ExecuteOnActiveTab(c => c.OnSave());
    private void ToolbarSettle_Click(object sender, RoutedEventArgs e) => ExecuteOnActiveTab(c => c.OnSettle());
    private void ToolbarPrint_Click(object sender, RoutedEventArgs e) => ExecuteOnActiveTab(c => c.OnPrint());
    private void ToolbarReturn_Click(object sender, RoutedEventArgs e) => ExecuteOnActiveTab(c => c.OnReturn());
    private void ToolbarCancel_Click(object sender, RoutedEventArgs e) => ExecuteOnActiveTab(c => c.OnCancel());
    private void ToolbarHistory_Click(object sender, RoutedEventArgs e) => ExecuteOnActiveTab(c => c.OnHistory());
    private void ToolbarCloseTab_Click(object sender, RoutedEventArgs e)
    {
        if (mainTab.SelectedItem is TabItem tab)
            CloseTab(tab);
    }

    #endregion

    protected override void OnClosed(EventArgs e)
    {
        _timer.Stop();
        base.OnClosed(e);
    }
}
