using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using QP11.Services;
using QP11.Wpf.Converters;
using QP11.Wpf.Utilities;

namespace QP11.Wpf.Views;

/// <summary>聊天消息模型</summary>
public class VinChatMessage : INotifyPropertyChanged
{
    public bool IsUser { get; set; }
    public string? Text { get; set; }
    public VinDecodeResult? VehicleInfo { get; set; }
    public List<VinPartCategoryGroup>? PartCategories { get; set; }
    public VinPartCard? ExpandedCard { get; set; }
    public string? Vin { get; set; }

    public int PartCount => PartCategories?.SelectMany(c => c.Products).Count() ?? 0;
    public int MatchedCount => PartCategories?.SelectMany(c => c.Products).Count(p => p.IsLocalMatched) ?? 0;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>数据源登录项（用于登录面板绑定）</summary>
public class VinSourceLoginItem : INotifyPropertyChanged
{
    public string SourceName { get; set; } = "";
    public bool IsSourceLoggedIn { get; set; }
    public string Phone { get; set; } = "";
    public string SmsCode { get; set; } = "";
    public string LoginStatus { get; set; } = "";
    public bool IsSendingSms { get; set; }
    public int SmsCountdown { get; set; }
    public string SendSmsButtonText => SmsCountdown > 0 ? $"重发({SmsCountdown}s)" : "发送验证码";
    public string TokenExpiryText { get; set; } = "";

    public event PropertyChangedEventHandler? PropertyChanged;
    public void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>配件分类导航项</summary>
public class VinCategoryNavItem
{
    public string DisplayName { get; set; } = "";
    public List<VinPartCard> Products { get; set; } = [];
}

public partial class VinQueryWindow : Window, INotifyPropertyChanged
{
    private readonly IVinQueryService _vinService;
    private readonly IVinLocalMatchService _localMatchService;
    private readonly MainWindow _mainWindow;

    // 配件列表视图状态
    private bool _isShowingPartList;
    private List<VinPartCategoryGroup>? _currentPartCategories;

    // 登录相关
    private bool _isLoggedIn;
    public bool IsLoggedIn
    {
        get => _isLoggedIn;
        set { _isLoggedIn = value; OnPropertyChanged(); }
    }

    private ObservableCollection<VinSourceLoginItem> _sourceLoginItems = [];
    public ObservableCollection<VinSourceLoginItem> SourceLoginItems => _sourceLoginItems;

    // 查询相关
    private string _vinInput = "";
    public string VinInput
    {
        get => _vinInput;
        set { _vinInput = value; OnPropertyChanged(); }
    }

    public ObservableCollection<VinChatMessage> Messages { get; } = [];

    private bool _isQuerying;
    public bool IsQuerying
    {
        get => _isQuerying;
        set { _isQuerying = value; OnPropertyChanged(); }
    }

    // 当前查询状态
    private string? _currentVin;
    private VinDecodeResult? _currentVehicleInfo;

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    public VinQueryWindow(IVinQueryService vinService, IVinLocalMatchService localMatchService, MainWindow mainWindow)
    {
        InitializeComponent();
        _vinService = vinService;
        _localMatchService = localMatchService;
        _mainWindow = mainWindow;

        InitializeSourceLoginItems();
        _vinService.SourceStatusChanged += OnSourceStatusChanged;

        DataContext = this;
        Loaded += VinQueryWindow_Loaded;
    }

    private async void VinQueryWindow_Loaded(object sender, RoutedEventArgs e)
    {
        await _vinService.StartupRefreshAsync();
        RefreshSourceLoginStatus();

        if (IsLoggedIn)
            txtVinInput.Focus();
    }

    #region 登录

    private void OnSourceStatusChanged(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() => RefreshSourceLoginStatus());
    }

    private void RefreshSourceLoginStatus()
    {
        var allSources = _vinService.GetAllSources();
        var loggedInSources = _vinService.GetLoggedInSources();
        var loggedInNames = loggedInSources.Select(s => s.SourceName).ToHashSet();

        foreach (var item in _sourceLoginItems)
        {
            var wasLoggedIn = item.IsSourceLoggedIn;
            item.IsSourceLoggedIn = loggedInNames.Contains(item.SourceName);
            item.OnPropertyChanged(nameof(item.IsSourceLoggedIn));

            var source = allSources.FirstOrDefault(s => s.SourceName == item.SourceName);
            var expiry = source?.GetTokenExpiryTime();
            item.TokenExpiryText = expiry.HasValue
                ? $"到期: {expiry.Value:HH:mm}" + (expiry.Value.Date != DateTime.Today ? $" ({expiry.Value:MM/dd})" : "")
                : (item.IsSourceLoggedIn ? "已登录" : "");
            item.OnPropertyChanged(nameof(item.TokenExpiryText));

            if (wasLoggedIn && !item.IsSourceLoggedIn)
            {
                item.LoginStatus = "登录已过期，请重新登录";
                item.OnPropertyChanged(nameof(item.LoginStatus));
                panelSourceLogin.Visibility = Visibility.Visible;
            }
        }

        IsLoggedIn = _sourceLoginItems.Any(s => s.IsSourceLoggedIn);
    }

    /// <summary>从持久化文件加载上次使用的手机号</summary>
    private static string LoadLastPhone()
    {
        try
        {
            var path = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "vin_last_phone.json");
            if (!System.IO.File.Exists(path)) return "";
            var json = System.IO.File.ReadAllText(path);
            var doc = System.Text.Json.JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("phone", out var p) ? p.GetString() ?? "" : "";
        }
        catch { return ""; }
    }

    /// <summary>保存手机号到持久化文件</summary>
    private static void SaveLastPhone(string phone)
    {
        try
        {
            var dir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            System.IO.Directory.CreateDirectory(dir);
            var path = System.IO.Path.Combine(dir, "vin_last_phone.json");
            var json = System.Text.Json.JsonSerializer.Serialize(new { phone });
            System.IO.File.WriteAllText(path, json);
        }
        catch { /* 保存失败不影响主流程 */ }
    }

    private void InitializeSourceLoginItems()
    {
        _sourceLoginItems.Clear();
        var loggedInSources = _vinService.GetLoggedInSources();
        var loggedInNames = loggedInSources.Select(s => s.SourceName).ToHashSet();
        var lastPhone = LoadLastPhone();

        _sourceLoginItems.Add(new VinSourceLoginItem
        {
            SourceName = "318car",
            Phone = lastPhone,
            IsSourceLoggedIn = loggedInNames.Contains("318car")
        });

        if (_vinService is CompositeVinQueryService composite)
        {
            _sourceLoginItems.Add(new VinSourceLoginItem
            {
                SourceName = "品秀",
                Phone = lastPhone,
                IsSourceLoggedIn = loggedInNames.Contains("品秀")
            });
        }

        icSourceLogins.ItemsSource = _sourceLoginItems;
        IsLoggedIn = _sourceLoginItems.Any(s => s.IsSourceLoggedIn);
    }

    private async void BtnSourceSendSms_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is VinSourceLoginItem item)
        {
            if (string.IsNullOrWhiteSpace(item.Phone) || item.Phone.Length != 11)
            {
                item.LoginStatus = "请输入11位手机号";
                item.OnPropertyChanged(nameof(item.LoginStatus));
                return;
            }

            item.IsSendingSms = true;
            item.LoginStatus = "发送中...";
            item.OnPropertyChanged(nameof(item.IsSendingSms));
            item.OnPropertyChanged(nameof(item.LoginStatus));

            try
            {
                var ok = await _vinService.SendSourceSmsAsync(item.SourceName, item.Phone);
                item.LoginStatus = ok ? "验证码已发送" : "发送失败";
                if (ok)
                {
                    SaveLastPhone(item.Phone);
                    item.SmsCountdown = 60;
                    item.OnPropertyChanged(nameof(item.SendSmsButtonText));
                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                    timer.Tick += (s, ev) =>
                    {
                        item.SmsCountdown--;
                        item.OnPropertyChanged(nameof(item.SendSmsButtonText));
                        item.IsSendingSms = item.SmsCountdown > 0;
                        item.OnPropertyChanged(nameof(item.IsSendingSms));
                        if (item.SmsCountdown <= 0) timer.Stop();
                    };
                    timer.Start();
                }
            }
            catch (Exception ex)
            {
                item.LoginStatus = $"发送失败: {ex.Message}";
            }
            finally
            {
                item.IsSendingSms = false;
                item.OnPropertyChanged(nameof(item.IsSendingSms));
                item.OnPropertyChanged(nameof(item.LoginStatus));
            }
        }
    }

    private async void BtnSourceLogin_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is VinSourceLoginItem item)
        {
            if (string.IsNullOrWhiteSpace(item.SmsCode))
            {
                item.LoginStatus = "请输入验证码";
                item.OnPropertyChanged(nameof(item.LoginStatus));
                return;
            }

            item.LoginStatus = "登录中...";
            item.OnPropertyChanged(nameof(item.LoginStatus));

            try
            {
                var ok = await _vinService.LoginSourceAsync(item.SourceName, item.Phone, item.SmsCode);
                if (ok)
                {
                    item.IsSourceLoggedIn = true;
                    item.LoginStatus = "";
                    item.OnPropertyChanged(nameof(item.IsSourceLoggedIn));
                    item.OnPropertyChanged(nameof(item.LoginStatus));
                    IsLoggedIn = _sourceLoginItems.Any(s => s.IsSourceLoggedIn);

                    SaveLastPhone(item.Phone);
                    RefreshSourceLoginStatus();

                    // 自动填充手机号到其他未登录数据源
                    foreach (var other in _sourceLoginItems.Where(s => s != item && !s.IsSourceLoggedIn))
                    {
                        if (string.IsNullOrEmpty(other.Phone))
                        {
                            other.Phone = item.Phone;
                            other.OnPropertyChanged(nameof(other.Phone));
                        }
                    }

                    if (_sourceLoginItems.All(s => s.IsSourceLoggedIn))
                        panelSourceLogin.Visibility = Visibility.Collapsed;

                    txtVinInput.Focus();
                }
                else
                {
                    item.LoginStatus = "登录失败，请检查验证码";
                }
            }
            catch (Exception ex)
            {
                item.LoginStatus = $"登录失败: {ex.Message}";
            }
            item.OnPropertyChanged(nameof(item.LoginStatus));
        }
    }

    private void SourceTag_Click(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount > 1) return;
        if (sender is FrameworkElement fe && fe.DataContext is VinSourceLoginItem item)
        {
            if (!item.IsSourceLoggedIn)
                panelSourceLogin.Visibility = Visibility.Visible;
        }
    }

    private void VehicleCard_CopyInfo(object sender, RoutedEventArgs e)
    {
        if (_currentVehicleInfo is not VinDecodeResult v || string.IsNullOrEmpty(v.Brand)) return;
        var lines = new List<string>();
        if (!string.IsNullOrEmpty(v.Brand) || !string.IsNullOrEmpty(v.Series))
            lines.Add($"{v.Brand} {v.Series}".Trim());
        if (!string.IsNullOrEmpty(v.Models)) lines.Add($"车型: {v.Models}");
        if (!string.IsNullOrEmpty(v.YearRange)) lines.Add($"年款: {v.YearRange}");
        if (!string.IsNullOrEmpty(v.EngineModel)) lines.Add($"发动机: {v.EngineModel}");
        if (!string.IsNullOrEmpty(v.DisplacementWithT)) lines.Add($"排量: {v.DisplacementWithT}");
        if (!string.IsNullOrEmpty(v.ChassisCode4)) lines.Add($"底盘号: {v.ChassisCode4}");
        if (!string.IsNullOrEmpty(v.DriveMode)) lines.Add($"驱动: {v.DriveMode}");
        if (!string.IsNullOrEmpty(v.GearboxType)) lines.Add($"变速箱: {v.GearboxType}");
        if (lines.Count > 0)
            WinClipboard.SetText(string.Join(Environment.NewLine, lines));
    }

    private void SourceTag_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount >= 2 && sender is FrameworkElement fe && fe.DataContext is VinSourceLoginItem item)
        {
            e.Handled = true;
            item.IsSourceLoggedIn = false;
            item.LoginStatus = "请重新登录";
            item.TokenExpiryText = "";
            item.SmsCode = "";
            item.OnPropertyChanged(nameof(item.IsSourceLoggedIn));
            item.OnPropertyChanged(nameof(item.LoginStatus));
            item.OnPropertyChanged(nameof(item.TokenExpiryText));
            item.OnPropertyChanged(nameof(item.SmsCode));
            panelSourceLogin.Visibility = Visibility.Visible;
            IsLoggedIn = _sourceLoginItems.Any(s => s.IsSourceLoggedIn);
        }
    }

    private void BtnCollapseSourcePanel_Click(object sender, RoutedEventArgs e)
    {
        panelSourceLogin.Visibility = Visibility.Collapsed;
    }

    #endregion

    #region VIN查询

    private async void BtnQuery_Click(object sender, RoutedEventArgs e)
    {
        await QueryVinAsync();
    }

    private async void TxtVinInput_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !IsQuerying)
        {
            e.Handled = true;
            await QueryVinAsync();
        }
    }

    private async Task QueryVinAsync()
    {
        var vin = VinInput.Trim().ToUpperInvariant();
        if (vin.Length != 17)
        {
            Messages.Add(new VinChatMessage { IsUser = false, Text = "VIN码应为17位，请检查输入" });
            ScrollToBottom();
            return;
        }

        Messages.Add(new VinChatMessage { IsUser = true, Text = vin });
        VinInput = "";
        ScrollToBottom();

        IsQuerying = true;
        try
        {
            var loggedInSources = _vinService.GetLoggedInSources();
            if (loggedInSources.Count == 0)
            {
                Messages.Add(new VinChatMessage { IsUser = false, Text = "数据源Token已失效，请重新登录后再查询" });
                RefreshSourceLoginStatus();
                ScrollToBottom();
                return;
            }

            // Step 1: VIN解码
            var vehicleInfo = await _vinService.DecodeVinAsync(vin);
            if (vehicleInfo == null)
            {
                var stillLoggedIn = _vinService.GetLoggedInSources();
                if (stillLoggedIn.Count == 0)
                {
                    Messages.Add(new VinChatMessage { IsUser = false, Text = "数据源Token已失效，请重新登录后再查询" });
                    RefreshSourceLoginStatus();
                }
                else
                {
                    Messages.Add(new VinChatMessage { IsUser = false, Text = "VIN解码失败，未找到车型信息" });
                }
                ScrollToBottom();
                return;
            }

            // Step 2: 获取适配配件（自动加载所有页）
            var firstPage = await _vinService.GetPartCardsAsync(vin, vehicleInfo, 1);

            if (firstPage == null && _vinService.GetLoggedInSources().Count == 0)
            {
                Messages.Add(new VinChatMessage { IsUser = false, Text = "数据源Token已失效，请重新登录后再查询" });
                RefreshSourceLoginStatus();
                ScrollToBottom();
                return;
            }

            var allCategories = firstPage?.Categories ?? [];

            if (firstPage != null && firstPage.Current < firstPage.Pages)
            {
                for (int page = 2; page <= firstPage.Pages; page++)
                {
                    var nextPage = await _vinService.GetPartCardsAsync(vin, vehicleInfo, page);
                    if (nextPage == null) break;
                    foreach (var cat in nextPage.Categories)
                    {
                        var existing = allCategories.FirstOrDefault(c => c.TenantCategoryId == cat.TenantCategoryId);
                        if (existing != null)
                            existing.Products.AddRange(cat.Products);
                        else
                            allCategories.Add(cat);
                    }
                }
            }

            // Step 3: 匹配本地库存（委托给VinLocalMatchService）
            if (allCategories.Count > 0)
            {
                await _localMatchService.EnrichWithLocalDataAsync(allCategories.SelectMany(c => c.Products), vehicleInfo);
            }

            // Step 4: 添加系统消息气泡
            var msg = new VinChatMessage
            {
                IsUser = false,
                VehicleInfo = vehicleInfo,
                PartCategories = allCategories,
                Vin = vin
            };
            Messages.Add(msg);

            // Step 5: 检查各数据源错误
            if (_vinService is CompositeVinQueryService composite && composite.LastQueryErrors.Count > 0)
            {
                var errorParts = composite.LastQueryErrors.Select(kv => $"【{kv.Key}】{kv.Value}");
                Messages.Add(new VinChatMessage { IsUser = false, Text = "部分数据源查询异常: " + string.Join("; ", errorParts) });
                RefreshSourceLoginStatus();
            }
            _currentVin = vin;
            _currentVehicleInfo = vehicleInfo;
        }
        catch (Exception ex)
        {
            Messages.Add(new VinChatMessage { IsUser = false, Text = $"查询失败: {ex.Message}" });
        }
        finally
        {
            IsQuerying = false;
            ScrollToBottom();
        }
    }

    /// <summary>添加配件到销售明细</summary>
    public void AddPartToSellDetail(VinPartCard card)
    {
        if (!card.IsLocalMatched || card.LocalPartId == null)
        {
            MessageBox.Show("该配件未匹配到本地库存，无法添加", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var sellControl = _mainWindow.GetActiveSellControl();
        if (sellControl == null)
        {
            MessageBox.Show("请先打开销售开单页面后再添加配件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        VinLocalMatch selected;
        if (card.LocalCandidates.Count > 1)
        {
            var selectDlg = new VinCandidateSelectWindow(card.LocalCandidates, card.Model ?? "");
            selectDlg.Owner = this;
            if (selectDlg.ShowDialog() != true || selectDlg.SelectedItem == null)
                return;
            selected = selectDlg.SelectedItem;
        }
        else
        {
            selected = card.LocalCandidates[0];
        }

        var clientName = sellControl.GetCurrentClientName();
        var sellRepo = App.ServiceProvider.GetRequiredService<ISellRepository>();
        var buyRepo = App.ServiceProvider.GetRequiredService<IBuyRepository>();
        var clientRepo = App.ServiceProvider.GetRequiredService<IClientRepository>();
        var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        var dlg = new SellEditDialog(
            selected.PartId,
            selected.PartNo ?? "",
            selected.Name ?? "",
            selected.LsPrice,
            selected.PfPrice,
            selected.StockAmount,
            sellRepo, buyRepo, clientRepo, dbFactory,
            clientName,
            selected.CarType ?? "",
            selected.StockAmount == 0);
        dlg.Owner = this;

        if (dlg.ShowDialog() == true && dlg.IsConfirmed)
        {
            sellControl.AddDetailFromVin(
                selected.PartId,
                selected.PartNo ?? "",
                selected.Name ?? "",
                dlg.Price,
                dlg.BillPrice,
                dlg.Amount,
                dlg.CarMark,
                dlg.Cartype,
                dlg.Memo);
        }
    }

    private void BtnAddPart_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is VinPartCard card)
            AddPartToSellDetail(card);
    }

    private void BtnAddPartItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is VinPartCard card)
            AddPartToSellDetail(card);
    }

    private void PartImg_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is VinPartCard card && card.ImgUrlList.Count > 0)
        {
            e.Handled = true;
            var viewer = new VinImageViewerWindow(card.ImgUrlList);
            viewer.Owner = this;
            viewer.Show();
        }
    }

    private void BtnPartSummary_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement fe) return;
        var msg = fe.DataContext as VinChatMessage;
        if (msg?.PartCategories == null || msg.PartCategories.Count == 0) return;
        ShowPartListView(msg.PartCategories);
    }

    private void ShowPartListView(List<VinPartCategoryGroup> categories)
    {
        _currentPartCategories = categories;
        _isShowingPartList = true;

        var all = categories.SelectMany(c => c.Products).ToList();
        var allMatched = all.Count(p => p.IsLocalMatched);

        lstPartCategories.Items.Clear();
        lstPartCategories.Items.Add(new VinCategoryNavItem
        {
            DisplayName = $"全部 ({allMatched}/{all.Count})",
            Products = all
        });
        foreach (var cat in categories)
        {
            var matched = cat.Products.Count(p => p.IsLocalMatched);
            lstPartCategories.Items.Add(new VinCategoryNavItem
            {
                DisplayName = $"{cat.CategoryName} ({matched}/{cat.Products.Count})",
                Products = cat.Products
            });
        }
        lstPartCategories.SelectedIndex = 0;

        txtPartListSummary.Text = $"共 {all.Count} 个配件，已匹配 {allMatched} 个本地库存";
        partListPanel.Visibility = Visibility.Visible;
    }

    private void BackToChatView()
    {
        if (!_isShowingPartList) return;
        _isShowingPartList = false;
        partListPanel.Visibility = Visibility.Collapsed;
    }

    private void BtnPartListBack_Click(object sender, RoutedEventArgs e)
    {
        BackToChatView();
    }

    private void LstPartCategories_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isShowingPartList) return;
        if (lstPartCategories.SelectedItem is VinCategoryNavItem item)
        {
            icPartItems.ItemsSource = item.Products
                .OrderByDescending(p => p.IsLocalMatched)
                .ToList();
        }
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape && _isShowingPartList)
        {
            e.Handled = true;
            BackToChatView();
        }
    }

    public async void QueryFromExternal(string vin)
    {
        VinInput = vin;
        Activate();
        await QueryVinAsync();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ScrollToBottom()
    {
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (FindName("scrollViewer") is ScrollViewer sv)
                sv.ScrollToEnd();
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    #endregion
}
