using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Constants;
using QP11.Core.Entities;
using QP11.Core.Models;
using QP11.Core.Interfaces;
using QP11.Wpf.ViewModels;
using QP11.Wpf.Helpers;
using QP11.Wpf.Services.LabelPrint;

namespace QP11.Wpf.Views;

public class SellControlItem : INotifyPropertyChanged
{
    public long? Partid { get; set; }
    public string? PartNo { get; set; }
    public string? PartName { get; set; }
    public string? Cartype { get; set; }
    public string? CarMark { get; set; }
    public string? Place { get; set; }
    public string? Memo { get; set; }

    private decimal _price;
    public decimal Price
    {
        get => _price;
        set { _price = value; OnPropertyChanged(nameof(Price)); OnPropertyChanged(nameof(SubTotal)); }
    }

    private decimal _billPrice;
    public decimal BillPrice
    {
        get => _billPrice;
        set { _billPrice = value; OnPropertyChanged(nameof(BillPrice)); OnPropertyChanged(nameof(SubTotal)); }
    }

    private decimal _amount = 1;
    public decimal Amount
    {
        get => _amount;
        set { _amount = value; OnPropertyChanged(nameof(Amount)); OnPropertyChanged(nameof(SubTotal)); }
    }

    public decimal SubTotal => Math.Round(Price * Amount, 2);

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public class BillSellDisplay : INotifyPropertyChanged
{
    private string? _memo;
    public string? Sn { get; set; }
    public DateTime? Datetime { get; set; }
    public string? Client { get; set; }
    public string? Worker { get; set; }
    public decimal? Total { get; set; }
    public decimal? BillTotal { get; set; }
    public int? Flag { get; set; }

    public string? Memo
    {
        get => _memo;
        set { _memo = value; OnPropertyChanged(nameof(Memo)); }
    }

    public string? FlagText => BusinessConstants.GetFlagText(Flag ?? 0);

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class SellControl : UserControl, ITabContent
{
    private readonly SellViewModel _viewModel;
    private readonly ISellRepository _sellRepo;
    private readonly IBuyRepository _buyRepo;
    private readonly IClientRepository _clientRepo;
    private readonly IDbConnectionFactory _dbFactory;
    private List<ClientInfor> _allClients = new();

    private bool _isQueryMode;
    private bool _isReturnMode;
    private bool _hideScrapPlace = true; // 隐藏废品仓（默认勾选）
    private int _lastQueryBoxIndex; // 记录上次聚焦的查询输入框索引
    private BillSell? _selectedBill;

    public bool HideScrapPlace
    {
        get => _hideScrapPlace;
        set => _hideScrapPlace = value;
    }
    public string TabTitle => "销售开单";
    public bool HasUnsavedChanges => _viewModel.Details.Count > 0;

    public event EventHandler? RequestClose;

    private readonly DispatcherTimer _searchTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };

    public SellControl(SellViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _sellRepo = App.ServiceProvider.GetRequiredService<ISellRepository>();
        _buyRepo = App.ServiceProvider.GetRequiredService<IBuyRepository>();
        _clientRepo = App.ServiceProvider.GetRequiredService<IClientRepository>();
        _dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
        _queryTextBoxes = new[] { txtPartNo, txtPartName, txtCartype, txtClass };
        dgDetails.ItemsSource = _viewModel.Details;
        dtBillDate.SelectedDate = DateTime.Now;
        dtQStart.SelectedDate = DateTime.Today;
        dtQEnd.SelectedDate = DateTime.Today;
        Loaded += SellControl_Loaded;
        IsVisibleChanged += SellControl_IsVisibleChanged;
        _searchTimer.Tick += (s, e) => { _searchTimer.Stop(); _ = LoadPartList(); };
        LoadDropdowns();
    }

    private void SellControl_Loaded(object sender, RoutedEventArgs e)
    {
        // 开单模式：配件列表后台加载，不阻塞窗口显示；查询模式：查单据列表
        if (_isQueryMode)
            BtnSearchBills_Click(this, new RoutedEventArgs());
        else
            _ = LoadPartList();

        txtPartName.Focus();
        txtPartName.SelectAll();
    }

    private void SellControl_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (IsVisible)
        {
            BtnSearchBills_Click(this, new RoutedEventArgs());
        }
    }

    /// <summary>从VIN查询窗口添加配件明细</summary>
    public void AddDetailFromVin(long partId, string partNo, string partName, decimal price, decimal billPrice, decimal amount, string? carMark, string? cartype, string? memo)
    {
        _viewModel.Details.Add(new SellControlItem
        {
            Partid = partId,
            PartNo = partNo,
            PartName = partName,
            Price = price,
            BillPrice = billPrice,
            Amount = amount,
            CarMark = carMark,
            Cartype = cartype,
            Memo = memo
        });
    }

    /// <summary>获取当前客户名称（供VIN窗口使用）</summary>
    public string? GetCurrentClientName()
    {
        return cboClient.SearchText;
    }

    private async void LoadDropdowns()
    {
        try
        {
            // 3个查询并行执行，而非串行等待
            var clientsTask = _viewModel.LoadClientsAsync();
            var usersTask = _viewModel.LoadUsersAsync();
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            var workersTask = db.QueryAsync("SELECT workid, name FROM work_infor ORDER BY workid");

            await Task.WhenAll(clientsTask, usersTask, workersTask);

            var clients = await clientsTask;
            _allClients = clients;
            cboClient.SetClients(_allClients);
            txtQClient.SetClients(_allClients);

            var users = await usersTask;
            cboWorker.ItemsSource = users;
            cboWorker.DisplayMemberPath = "Name";
            cboWorker.SelectedValuePath = "Username";

            var rows = await workersTask;
            var qWorkers = rows.Select(r => new WorkerItem { Workid = (string)r.workid, Name = (string)r.name }).ToList();
            txtQWorker.ItemsSource = qWorkers;

            var currentUsername = App.CurrentUser?.Username;
            if (!string.IsNullOrEmpty(currentUsername))
            {
                var currentUser = users.FirstOrDefault(u => u.Username == currentUsername);
                if (currentUser != null)
                    cboWorker.SelectedItem = currentUser;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "LoadDropdowns 失败");
        }
    }




    private async Task LoadPartList()
    {
        try
        {
            var partNo = string.IsNullOrWhiteSpace(txtPartNo.Text) ? null : txtPartNo.Text;
            var partName = string.IsNullOrWhiteSpace(txtPartName.Text) ? null : txtPartName.Text.Trim();
            var cartype = string.IsNullOrWhiteSpace(txtCartype.Text) ? null : txtCartype.Text.Trim();
            var className = string.IsNullOrWhiteSpace(txtClass.Text) ? null : txtClass.Text.Trim();
            var queryMode = cmbQueryMode.SelectedIndex;

            var data = await _viewModel.LoadPartListAsync(partNo, partName, cartype, className, queryMode, _hideScrapPlace);
            dgParts.ItemsSource = data;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "加载配件列表失败");
        }
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T result) return result;
            var descendant = FindVisualChild<T>(child);
            if (descendant != null) return descendant;
        }
        return null;
    }

    private void TxtPartQuery_TextChanged(object sender, TextChangedEventArgs e)
    {
        _searchTimer.Stop();
        _searchTimer.Start();
    }

    private readonly TextBox[] _queryTextBoxes;

    private void QueryTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox current) return;

        int index = Array.IndexOf(_queryTextBoxes, current);
        if (index < 0) return;

        if (e.Key == Key.Right)
        {
            e.Handled = true;
            int next = (index + 1) % 4;
            _queryTextBoxes[next].Focus();
            _queryTextBoxes[next].SelectAll();
        }
        else if (e.Key == Key.Left)
        {
            e.Handled = true;
            int prev = (index - 1 + 4) % 4;
            _queryTextBoxes[prev].Focus();
            _queryTextBoxes[prev].SelectAll();
        }
        else if (e.Key == Key.Down && dgParts.Items.Count > 0)
        {
            e.Handled = true;
            dgParts.SelectedIndex = 0;
            dgParts.ScrollIntoView(dgParts.Items[0]);
            dgParts.CurrentCell = new DataGridCellInfo(dgParts.Items[0], dgParts.Columns[0]);
            Dispatcher.BeginInvoke(() =>
            {
                dgParts.Focus();
                var row = (DataGridRow)dgParts.ItemContainerGenerator.ContainerFromIndex(0);
                if (row != null)
                {
                    var cell = FindVisualChild<DataGridCell>(row);
                    cell?.Focus();
                }
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    /// <summary>查询输入框获得焦点时全选文字，并记录索引</summary>
    private void QueryTextBox_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            int idx = Array.IndexOf(_queryTextBoxes, tb);
            if (idx >= 0) _lastQueryBoxIndex = idx;
            Dispatcher.BeginInvoke(() => tb.SelectAll(), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private void NavButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            var title = btn.Content?.ToString() ?? tag;
            var mainWin = Window.GetWindow(this) as MainWindow;
            mainWin?.OpenFunctionTab(tag, title);
        }
    }

    private void NavCalc_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Process.Start("calc.exe");
    }

    private void NavNotepad_Click(object sender, RoutedEventArgs e)
    {
        System.Diagnostics.Process.Start("notepad.exe");
    }

    private void DgParts_MouseDoubleClick(object sender, MouseButtonEventArgs? e)
    {
        if (dgParts.SelectedItem == null) return;
        try
        {
            if (dgParts.SelectedItem is not PartStockDisplay row) return;
            long partid = row.PartId;
            string partno = row.PartNo ?? "";
            string name = row.Name ?? "";
            string cartype = row.CarType ?? "";
            decimal lsprice = row.LsPrice ?? 0m;
            decimal pfprice = row.PfPrice ?? 0m;
            int stockAmount = row.Amount == null ? 0 : (int)row.Amount.Value;

            // 库存为0时，打开只读模式查看销售/进货历史
            if (stockAmount <= 0)
            {
                var historyDlg = new SellEditDialog(partid, partno, name, lsprice, pfprice, stockAmount, _sellRepo, _buyRepo, _clientRepo, _dbFactory, cboClient.SearchText, cartype, readOnly: true);
                historyDlg.Owner = Window.GetWindow(this);
                historyDlg.ShowDialog();
                return;
            }

            // BUG2: 废品仓配件需二次确认
            if (!string.IsNullOrEmpty(row.Place) && row.Place.Trim() == "废品仓")
            {
                var confirm = MessageBox.Show(
                    $"配件 [{partno} {name}] 为废品仓库存，是否确定出售？",
                    "废品仓提示", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (confirm != MessageBoxResult.Yes) return;
            }

            // 先检查该配件是否已在明细中，避免重复添加导致超开库存
            var existing = _viewModel.Details.FirstOrDefault(d => d.Partid == partid);
            if (existing != null)
            {
                var confirm = MessageBox.Show(
                    $"配件 [{partno} {name}] 已在明细中，是否修改？",
                    "提示", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (confirm == MessageBoxResult.Yes)
                {
                    // 弹出编辑窗口，用新数据替换原明细
                    var editDlg = new SellEditDialog(partid, partno, name, lsprice, pfprice, stockAmount, _sellRepo, _buyRepo, _clientRepo, _dbFactory, cboClient.SearchText, cartype);
                    editDlg.Owner = Window.GetWindow(this);
                    if (editDlg.ShowDialog() == true && editDlg.IsConfirmed)
                    {
                        existing.Price = editDlg.Price;
                        existing.BillPrice = editDlg.BillPrice;
                        existing.Amount = editDlg.Amount;
                        existing.CarMark = editDlg.CarMark;
                        existing.Cartype = editDlg.Cartype;
                        existing.Memo = editDlg.Memo ?? "";
                        UpdateTotals();
                    }
                }
                return; // 无论选择是/否都不再走新增逻辑
            }

            // 不存在则正常弹出编辑窗口新增
            var dlg = new SellEditDialog(partid, partno, name, lsprice, pfprice, stockAmount, _sellRepo, _buyRepo, _clientRepo, _dbFactory, cboClient.SearchText, cartype);
            dlg.Owner = Window.GetWindow(this);
            if (dlg.ShowDialog() == true && dlg.IsConfirmed)
            {
                _viewModel.Details.Add(new SellControlItem
                {
                    Partid = partid,
                    PartNo = partno,
                    PartName = name,
                    Price = dlg.Price,
                    BillPrice = dlg.BillPrice,
                    Amount = dlg.Amount,
                    CarMark = dlg.CarMark,
                    Cartype = dlg.Cartype,
                    Place = row.Place,
                    Memo = dlg.Memo ?? ""
                });

                UpdateTotals();
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "添加配件到明细失败");
        }
    }

    private void MiHideScrap_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi)
            _hideScrapPlace = mi.IsChecked;
        _ = LoadPartList();
    }

    private void DgParts_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && dgParts.SelectedItem != null)
        {
            e.Handled = true;
            DgParts_MouseDoubleClick(sender, null);
        }
        else if (e.Key == Key.F4 && dgParts.SelectedItem != null)
        {
            e.Handled = true;
            DgParts_MouseDoubleClick(sender, null);
        }
        else if (e.Key == Key.Right && dgParts.SelectedItem != null)
        {
            // 右键跳到下一个查询输入框
            e.Handled = true;
            _lastQueryBoxIndex = (_lastQueryBoxIndex + 1) % _queryTextBoxes.Length;
            _queryTextBoxes[_lastQueryBoxIndex].Focus();
            _queryTextBoxes[_lastQueryBoxIndex].SelectAll();
        }
        else if (e.Key == Key.Left && dgParts.SelectedItem != null)
        {
            // 左键跳到上一个查询输入框
            e.Handled = true;
            _lastQueryBoxIndex = (_lastQueryBoxIndex - 1 + _queryTextBoxes.Length) % _queryTextBoxes.Length;
            _queryTextBoxes[_lastQueryBoxIndex].Focus();
            _queryTextBoxes[_lastQueryBoxIndex].SelectAll();
        }
    }

    private async void DgParts_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (dgParts.SelectedItem == null) return;
        if (dgParts.SelectedItem is not PartStockDisplay row) return;

        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            var picture = await db.QueryFirstOrDefaultAsync<byte[]>(
                "SELECT picture FROM part_data WHERE partid=@PartId",
                new { PartId = row.PartId });
            if (picture != null && picture.Length > 0)
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.StreamSource = new MemoryStream(picture);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                bitmap.Freeze();
                imgPart.Source = bitmap;
            }
            else
            {
                imgPart.Source = null;
            }
        }
        catch { imgPart.Source = null; }
    }

    private void ImgPart_Click(object sender, MouseButtonEventArgs e)
    {
        if (imgPart.Source == null) return;
        OpenImagePreview();
    }

    private void ImgPart_Preview_Click(object sender, RoutedEventArgs e)
    {
        if (imgPart.Source == null) return;
        OpenImagePreview();
    }

    private void OpenImagePreview()
    {
        if (imgPart.Source is not BitmapSource source) return;

        var dlg = new Window
        {
            Title = "配件图片预览",
            Width = Math.Min(source.PixelWidth + 40, SystemParameters.PrimaryScreenWidth - 80),
            Height = Math.Min(source.PixelHeight + 60, SystemParameters.PrimaryScreenHeight - 80),
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            ResizeMode = ResizeMode.CanResizeWithGrip,
            Background = SystemColors.ControlBrush
        };

        var toolbar = new ToolBar();
        var btnCopy = new Button { Content = "复制图片", Width = 80, Height = 26 };
        btnCopy.Click += (_, _) => CopyPartImageToClipboard();
        var btnClose = new Button { Content = "关闭", Width = 55, Height = 24, Margin = new Thickness(8, 0, 0, 0) };
        btnClose.Click += (_, _) => dlg.Close();
        toolbar.Items.Add(btnCopy);
        toolbar.Items.Add(btnClose);

        var imgView = new Image { Source = source, Stretch = Stretch.Uniform };

        var sp = new StackPanel();
        sp.Children.Add(toolbar);
        sp.Children.Add(imgView);
        dlg.Content = sp;

        dlg.ShowDialog();
    }

    private void ImgPart_Copy_Click(object sender, RoutedEventArgs e)
    {
        CopyPartImageToClipboard();
    }

    private void CopyPartImageToClipboard()
    {
        if (imgPart.Source is not BitmapSource source)
        {
            MessageBox.Show("没有可复制的图片", "提示");
            return;
        }
        try
        {
            // 将 BitmapSource 编码为 PNG 流后写入剪贴板
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var ms = new MemoryStream();
            encoder.Save(ms);
            ms.Position = 0;

            Clipboard.Clear();
            var dataObj = new DataObject();
            dataObj.SetData("PNG", ms);
            dataObj.SetImage(source);
            Clipboard.SetDataObject(dataObj, true);

            MessageBox.Show("已复制到剪贴板", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"复制失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateTotals()
    {
        if (!IsLoaded) return;

        var total = _viewModel.Details.Sum(d => d.Price * d.Amount);
        txtTotal.Text = total.ToString("N2");

        var discountRate = decimal.TryParse(txtDiscountRate.Text, out var dr) ? dr : 0m;
        var billTotal = discountRate > 0 ? Math.Round(total * discountRate, 2) : total;
        txtBillTotal.Text = billTotal.ToString("N2");

        txtSumTotal.Text = total.ToString("N2");
        txtSumBillTotal.Text = billTotal.ToString("N2");
        txtSumAmount.Text = _viewModel.Details.Sum(d => d.Amount).ToString();

        UpdateArrear();
    }

    private void UpdateArrear()
    {
        if (!IsLoaded) return;

        var billTotal = decimal.TryParse(txtBillTotal.Text, out var bt) ? bt : 0;
        var cash = decimal.TryParse(txtCash.Text, out var c) ? c : 0;
        var checks = decimal.TryParse(txtChecks.Text, out var ck) ? ck : 0;
        var zhifubao = decimal.TryParse(txtZhifubao.Text, out var z) ? z : 0;
        var weixin = decimal.TryParse(txtWeixin.Text, out var w) ? w : 0;
        var paid = cash + checks + zhifubao + weixin;
        txtArrear.Text = Math.Max(0, billTotal - paid).ToString("N2");
    }

    private void TxtDiscountRate_TextChanged(object sender, TextChangedEventArgs e) => UpdateTotals();
    private void Payment_TextChanged(object sender, TextChangedEventArgs e) => UpdateArrear();

    private void BtnSwitchMode_Click(object sender, RoutedEventArgs e) => ToggleMode();

    private async void BtnMultiCodeQuery_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new MultiCodeQueryDialog
        {
            Owner = Window.GetWindow(this)
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var data = await _viewModel.LoadPartListByCodesAsync(dlg.Codes, _hideScrapPlace);
            dgParts.ItemsSource = data;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "多条件查询失败");
            MessageBox.Show($"多条件查询失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ToggleMode()
    {
        _isQueryMode = !_isQueryMode;
        if (_isQueryMode)
        {
            panelOrderMode.Visibility = Visibility.Collapsed;
            panelQueryMode.Visibility = Visibility.Visible;
            BtnSearchBills_Click(this, new RoutedEventArgs());
        }
        else
        {
            panelOrderMode.Visibility = Visibility.Visible;
            panelQueryMode.Visibility = Visibility.Collapsed;
            // 切回开单模式时重置表单
            ClearBill();
            // 异步刷新配件列表（不阻塞UI切换，同时确保库存数据是最新的）
            _ = LoadPartList();
        }
    }

    public async void LoadBillForEdit(string sn)
    {
        try
        {
            var bill = await _viewModel.LoadBillForEditAsync(sn);
            if (bill == null)
            {
                MessageBox.Show($"单据 {sn} 不存在", "错误");
                return;
            }

            txtBillNo.Text = bill.Sn;
            dtBillDate.SelectedDate = bill.Datetime;

            // 通过客户ID查找客户名称
            if (!string.IsNullOrEmpty(bill.Client))
            {
                var client = _allClients.FirstOrDefault(c => c.Cid == bill.Client);
                cboClient.SetClient(client);
            }
            else
            {
                cboClient.ClearSelection();
            }

            // 业务员：按workid查名字显示
            if (!string.IsNullOrEmpty(bill.Worker))
            {
                try
                {
                    var wName = await _viewModel.GetWorkerNameAsync(bill.Worker);
                    if (!string.IsNullOrEmpty(wName))
                    {
                        var match = cboWorker.Items.Cast<UserInfor>()
                            .FirstOrDefault(u => u.Name == wName);
                        if (match != null)
                            cboWorker.SelectedItem = match;
                        else
                            cboWorker.Text = wName;
                    }
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "匹配业务员失败");
                }
            }
            txtCheckno.Text = bill.Checkno ?? "";
            var dbRate = bill.DiscountRate ?? 0m;
            txtDiscountRate.Text = (dbRate == 0m || dbRate >= 1m) ? "0" : dbRate.ToString();
            txtCash.Text = bill.Cash?.ToString() ?? "0";
            txtChecks.Text = bill.Checks?.ToString() ?? "0";
            txtZhifubao.Text = bill.Zhifubao?.ToString() ?? "0";
            txtWeixin.Text = bill.Weixin?.ToString() ?? "0";
            txtMemo.Text = bill.Memo ?? "";

            var details = await _viewModel.LoadDetailsAsync(sn);
            _viewModel.Details.Clear();
            foreach (var d in details)
            {
                _viewModel.Details.Add(new SellControlItem
                {
                    Partid = d.Partid,
                    PartNo = d.Partno,
                    PartName = d.Name,
                    Cartype = d.Cartype,
                    CarMark = d.CarMark,
                    Place = d.Place,
                    Price = d.Price ?? 0,
                    BillPrice = d.BillPrice ?? 0,
                    Amount = d.Amount ?? 0,
                    Memo = d.Memo
                });
            }
            UpdateTotals();
            // ClearBill() 已在 ToggleMode 中执行过，需重新设置编辑标志
            _viewModel.IsEditMode = true;
            _viewModel.EditSn = sn;

            if (_isQueryMode) ToggleMode();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载单据失败: {ex.Message}", "错误");
        }
    }

    #region 查询模式

    /// <summary>查询模式明细区：打印选中明细行的商品标签（替代原打印预览文档中的标签打印按钮）</summary>
    private void BtnQueryLabelPrint_Click(object sender, RoutedEventArgs e)
    {
        if (dgQueryDetails.SelectedItem is not DetailSell d || string.IsNullOrWhiteSpace(d.Partno))
        {
            MessageBox.Show("请先在下方明细中选择一行配件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var dlg = new LabelPrintDialog(new LabelPrintItem
        {
            PartNo = d.Partno,
            Name = d.Name ?? "",
            CarType = d.Cartype ?? ""
        });
        if (Application.Current.MainWindow is { IsVisible: true } mainWin)
            dlg.Owner = mainWin;
        dlg.ShowDialog();
    }

    private async void BtnSearchBills_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            // DatePicker 手动输入日期时 SelectedDate 不会自动同步，需优先从 Text 解析
            var qStart = dtQStart.SelectedDate;
            if (DateTime.TryParse(dtQStart.Text, out var ps)) qStart = ps;
            var qEnd = dtQEnd.SelectedDate;
            if (DateTime.TryParse(dtQEnd.Text, out var pe)) qEnd = pe;

            var display = await _viewModel.SearchBillsAsync(qStart, qEnd, txtQClient.SearchText);
            dgBills.ItemsSource = display;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"查询失败: {ex.Message}", "错误");
        }
    }

    private void DgBills_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        try
        {
            if (e.Row.Item is BillSellDisplay row)
            {
                int flag = row.Flag ?? 0;
                if (flag == 2)
                {
                    e.Row.Foreground = Brushes.Red;
                    e.Row.Background = new SolidColorBrush(Color.FromArgb(30, 255, 200, 200));
                }
                else
                {
                    e.Row.Foreground = Brushes.Black;
                    e.Row.Background = null;
                }
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "DgBills_LoadingRow 失败");
        }
    }

    private async void DgBills_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (dgBills.SelectedItem == null) return;
        try
        {
            if (dgBills.SelectedItem is not BillSellDisplay row) return;
            string sn = row.Sn ?? "";
            if (string.IsNullOrEmpty(sn)) return;

            _selectedBill = await _viewModel.LoadBillForEditAsync(sn);
            if (_selectedBill == null) return;

            txtQBillNo.Text = _selectedBill.Sn;
            txtQDate.Text = _selectedBill.Datetime?.ToString("yyyy-MM-dd") ?? "";

            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db2 = await dbFactory.CreateAsync();
            var clientName = await db2.QueryFirstOrDefaultAsync<string>(
                "SELECT name FROM client_infor WHERE cid=@Cid", new { Cid = _selectedBill.Client });
            txtQClientName.Text = clientName ?? _selectedBill.Client ?? "";

            var workerName = await _viewModel.GetWorkerNameAsync(_selectedBill.Worker ?? "");
            txtQWorkerName.Text = workerName ?? _selectedBill.Worker ?? "";
            txtQStatus.Text = BusinessConstants.GetFlagText(_selectedBill.Flag ?? 0);

            var details = await _viewModel.LoadDetailsAsync(sn);
            dgQueryDetails.ItemsSource = details;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "DgBills_SelectionChanged 失败");
        }
    }

    private async void DgBills_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.Column.Header?.ToString() != "备注") return;
        if (e.EditAction == DataGridEditAction.Cancel) return;

        if (e.Row.Item is not BillSellDisplay bill) return;

        var textBox = e.EditingElement as TextBox;
        var newMemo = textBox?.Text?.Trim() ?? "";

        if (newMemo == (bill.Memo ?? "")) return;

        if (bill.Flag == (int)BusinessConstants.BillFlag.Returned)
        {
            if (App.CurrentUser == null)
            {
                e.Cancel = true;
                return;
            }
            var pwdDlg = new MemoConfirmDialog(App.CurrentUser)
            {
                Owner = Window.GetWindow(this)
            };
            if (pwdDlg.ShowDialog() != true)
            {
                e.Cancel = true;
                return;
            }
        }

        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            await db.ExecuteAsync("UPDATE bill_sell SET memo=@Memo WHERE sn=@Sn", new { Memo = newMemo, Sn = bill.Sn });
            bill.Memo = newMemo;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"修改备注失败: {ex.Message}", "错误");
            e.Cancel = true;
        }
    }

    #endregion

    private async void BtnPrintBill_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedBill == null)
        {
            MessageBox.Show("请先选择要打印的单据", "提示");
            return;
        }
        try
        {
            var billData = new BillPrintData
            {
                BillType = "销售",
                Sn = _selectedBill.Sn,
                DateText = _selectedBill.Datetime?.ToString("yyyy-MM-dd") ?? "",
                PartnerName = txtQClientName.Text,
                PartnerPhone = _allClients.FirstOrDefault(c => c.Cid == _selectedBill.Client)?.Mobile
                    ?? _allClients.FirstOrDefault(c => c.Cid == _selectedBill.Client)?.Tel ?? "",
                PartnerContact = _allClients.FirstOrDefault(c => c.Cid == _selectedBill.Client)?.Linkman ?? "",
                PartnerAddress = _allClients.FirstOrDefault(c => c.Cid == _selectedBill.Client)?.Address ?? "",
                WorkerName = txtQWorkerName.Text,
                TotalAmount = _selectedBill.Total ?? 0,
                Cash = _selectedBill.Cash ?? 0,
                Weixin = _selectedBill.Weixin ?? 0,
                Zhifubao = _selectedBill.Zhifubao ?? 0,
                Arrearage = _selectedBill.Arrear ?? 0,
                Memo = _selectedBill.Memo ?? "",
                DeliveryMethod = "自提"
            };
            await billData.LoadCompanyInfoAsync();

            var idx = 1;

            if (_isQueryMode && dgQueryDetails.ItemsSource is System.Collections.IEnumerable queryDetails)
            {
                foreach (var d in queryDetails.OfType<DetailSell>())
                {
                    billData.Items.Add(new BillPrintItem
                    {
                        Index = idx++,
                        PartNo = d.Partno,
                        PartName = d.Name,
                        Cartype = d.Cartype,
                        Unit = d.Unit,
                        Price = d.Price ?? 0,
                        PfPrice = 0,
                        BillPrice = d.BillPrice ?? 0,
                        Amount = (int)(d.Amount ?? 0),
                        Subtotal = d.Stotal ?? 0,
                        Place = d.Place,
                        Area = d.Area ?? "",
                        Brand = "",
                        DiscountRate = d.DiscountRate ?? 0,
                        Memo = d.Memo
                    });
                }
            }
            else
            {
                foreach (var d in _viewModel.Details)
                {
                    billData.Items.Add(new BillPrintItem
                    {
                        Index = idx++,
                        PartNo = d.PartNo,
                        PartName = d.PartName,
                        Cartype = d.Cartype,
                        Unit = "",
                        Price = d.Price,
                        PfPrice = 0,
                        BillPrice = d.BillPrice,
                        Amount = (int)d.Amount,
                        Subtotal = d.SubTotal,
                        Place = d.Place ?? "",
                        Area = "",
                        Brand = "",
                        DiscountRate = 0,
                        Memo = d.Memo
                    });
                }
            }

            var dlg = new PrintPreviewWindow(billData, $"销售单-{_selectedBill.Sn}")
            {
                Owner = Window.GetWindow(this)
            };
            dlg.ShowDialog();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打印预览失败: {ex.Message}", "错误");
        }
    }

    #region ITabContent

    public void OnAdd()
    {
        if (_isQueryMode)
        {
            ToggleMode();
            return;
        }
        txtPartName.Focus();
        txtPartName.SelectAll();
    }

    public async void OnEdit()
    {
        if (_isQueryMode)
        {
            // 查询模式：从单据列表获取选中的单号
            if (_selectedBill == null)
            {
                MessageBox.Show("请先在单据列表中选择要编辑的单据", "提示");
                return;
            }

            // 退货单需要密码验证后跳转到销售退货页面
            if (_selectedBill.Flag == (int)BusinessConstants.BillFlag.Returned)
            {
                var dlg = new MemoConfirmDialog(App.CurrentUser!);
                dlg.Title = "退货单编辑验证";
                if (dlg.ShowDialog() != true) return;

                var mainWin = Window.GetWindow(this) as MainWindow;
                mainWin?.OpenReturnEditTab(_selectedBill.Sn!);
                return;
            }

            // 普通销售单：设置编辑标志后切回开单模式并加载
            _viewModel.IsEditMode = true;
            _viewModel.EditSn = _selectedBill.Sn;
            var sn = _selectedBill.Sn;
            ToggleMode();
            LoadBillForEdit(sn!);
            return;
        }

        if (dgDetails.SelectedItem is not SellControlItem item) return;

        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            var part = db.QueryFirstOrDefault<dynamic>(
                "SELECT partid, partno, name, lsprice, pfprice, amount, place FROM part_stock WHERE partid=@PartId",
                new { PartId = item.Partid });

            if (part == null) return;

            long partId = Convert.ToInt64(part.partid);
            string partNo = part.partno ?? "";
            string partName = part.name ?? "";
            decimal lsprice = part.lsprice == null ? 0m : Convert.ToDecimal(part.lsprice);
            decimal pfprice = part.pfprice == null ? 0m : Convert.ToDecimal(part.pfprice);
            int stockAmount = part.amount == null ? 0 : Convert.ToInt32(part.amount);

            var dlg = new SellEditDialog(partId, partNo, partName, lsprice, pfprice, stockAmount, _sellRepo, _buyRepo, _clientRepo, _dbFactory, cboClient.SearchText, item.Cartype ?? "");
            dlg.SetEditValues(item.Amount, item.Price, item.BillPrice, item.CarMark, item.Cartype, item.Memo);
            dlg.Owner = Window.GetWindow(this);
            if (dlg.ShowDialog() == true && dlg.IsConfirmed)
            {
                item.Price = dlg.Price;
                item.BillPrice = dlg.BillPrice;
                item.Amount = dlg.Amount;
                item.CarMark = dlg.CarMark;
                item.Cartype = dlg.Cartype;
                UpdateTotals();
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "编辑明细失败");
        }
    }

    public void OnQuery() => ToggleMode();

    public void OnDelete()
    {
        if (_isQueryMode)
        {
            VoidSelectedBill();
            return;
        }
        if (dgDetails.SelectedItem is SellControlItem item)
        {
            _viewModel.Details.Remove(item);
            UpdateTotals();
        }
    }

    private void DgDetails_Delete_Click(object sender, RoutedEventArgs e)
    {
        if (dgDetails.SelectedItem is not SellControlItem item) return;
        if (MessageBox.Show($"确定删除 [{item.PartName}]？", "确认删除", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        _viewModel.Details.Remove(item);
        UpdateTotals();
    }

    private void DgDetails_Price_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox tb || dgDetails.SelectedItem is not SellControlItem item) return;
        if (decimal.TryParse(tb.Text, out var price) && price != item.BillPrice)
            item.BillPrice = price;
        UpdateTotals();
    }

    private void DgDetails_PreparingCellForEdit(object sender, DataGridPreparingCellForEditEventArgs e)
    {
        if (e.EditingElement is TextBox tb)
            Dispatcher.BeginInvoke(new Action(() => tb.SelectAll()), System.Windows.Threading.DispatcherPriority.Input);
    }

    public void OnSave() => SaveBill();

    public void OnSettle() => SettleBill();

    public void OnPrint() => BtnPrintBill_Click(this, new RoutedEventArgs());

    public void OnReturn()
    {
        // 跳转到销售退货页面
        var mainWin = Window.GetWindow(this) as MainWindow;
        mainWin?.OpenFunctionTab("138", "销售退货");
    }

    public void OnCancel() => ClearBill();

    public void OnHistory() { }

    public void OnClose() => RequestClose?.Invoke(this, EventArgs.Empty);

    #endregion


    private async void SaveBill()
    {
        if (_isQueryMode) return;
        if (_viewModel.Details.Count == 0)
        {
            MessageBox.Show("请添加销售明细", "提示");
            return;
        }

        // 提交DataGrid中正在编辑的单元格，确保绑定值已更新到数据源
        dgDetails.CommitEdit(DataGridEditingUnit.Row, true);

        var clientId = cboClient.SelectedClientId;
        var clientText = cboClient.SearchText.Trim();
        if (string.IsNullOrEmpty(clientId))
        {
            if (string.IsNullOrEmpty(clientText))
            {
                MessageBox.Show("请选择客户", "提示");
                cboClient.Focus();
                return;
            }
            clientId = await _viewModel.ResolveClientIdAsync(clientText);
            if (string.IsNullOrEmpty(clientId))
            {
                MessageBox.Show($"客户 \"{clientText}\" 不存在，请从列表中选择有效客户", "提示");
                cboClient.Focus();
                return;
            }
        }

        try
        {
            IsEnabled = false;

            var discountRate = decimal.TryParse(txtDiscountRate.Text, out var dr) ? dr : 0m;
            var cash = decimal.TryParse(txtCash.Text, out var c) ? c : 0;
            var weixin = decimal.TryParse(txtWeixin.Text, out var w) ? w : 0;
            var zhifubao = decimal.TryParse(txtZhifubao.Text, out var z) ? z : 0;
            var checks = decimal.TryParse(txtChecks.Text, out var ck) ? ck : 0;

            var workerName = (cboWorker.SelectedItem as UserInfor)?.Name ?? cboWorker.Text.Trim();
            var workerId = !string.IsNullOrEmpty(workerName)
                ? await _viewModel.ResolveWorkerIdAsync(workerName)
                : workerName;

            // DatePicker 手动输入日期时 SelectedDate 不会自动同步，需优先从 Text 解析
            // 合并当前时间部分，避免同日单据无法按录入时间排序
            var billDate = dtBillDate.SelectedDate;
            if (DateTime.TryParse(dtBillDate.Text, out var parsedDate))
                billDate = parsedDate;
            billDate = (billDate?.Date ?? DateTime.Now.Date) + DateTime.Now.TimeOfDay;

            var result = await _viewModel.SaveBillAsync(
                _viewModel.EditSn,
                _viewModel.IsEditMode,
                _viewModel.IsReturnMode,
                clientId,
                billDate,
                workerId,
                App.CurrentUser?.Username,
                discountRate,
                cash, weixin, zhifubao, checks,
                txtMemo.Text,
                txtCheckno.Text);

            if (result.Success)
            {
                txtBillNo.Text = result.BillNo;

                // 构建打印数据（在清空明细之前）
                var printData = await BuildPrintDataAsync(result.BillNo);

                var dlg = new SettlementResultDialog(result.BillNo, result.BillTotal, result.TotalPaid, result.Arrear)
                {
                    Owner = Window.GetWindow(this),
                    PrintData = printData
                };
                dlg.ShowDialog();

                ClearBill();
                if (!dlg.PrintNow)
                {
                    // 未勾选打印 → 跳转查询模式
                    if (!_isQueryMode) ToggleMode();
                    BtnSearchBills_Click(this, new RoutedEventArgs());
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败:\n{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private async void SettleBill()
    {
        if (_isQueryMode) return;
        if (_viewModel.Details.Count == 0) return;
        if (string.IsNullOrEmpty(txtBillNo.Text))
        {
            MessageBox.Show("请先保存单据", "提示");
            return;
        }

        try
        {
            await _viewModel.SettleBillAsync(txtBillNo.Text);
            MessageBox.Show("结算成功", "提示");
            ClearBill();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"结算失败: {ex.Message}", "错误");
        }
    }

    private async void VoidSelectedBill()
    {
        if (_selectedBill == null)
        {
            MessageBox.Show("请选择要作废的单据", "提示");
            return;
        }

        if (MessageBox.Show($"确定删除单据 [{_selectedBill.Sn}]? 删除后不可恢复，库存将回补", "确认",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

        try
        {
            await _viewModel.VoidBillAsync(_selectedBill.Sn!);
            _selectedBill = null;
            txtQBillNo.Text = "";
            txtQDate.Text = "";
            txtQClientName.Text = "";
            txtQWorkerName.Text = "";
            txtQStatus.Text = "";
            dgQueryDetails.ItemsSource = null;
            BtnSearchBills_Click(this, new RoutedEventArgs());
            MessageBox.Show("单据已删除，库存已回补", "提示");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"删除失败: {ex.Message}", "错误");
        }
    }

    /// <summary>
    /// 根据当前表单数据构建打印数据（必须在 ClearBill 之前调用）
    /// </summary>
    private async Task<BillPrintData> BuildPrintDataAsync(string billNo)
    {
        var clientObj = _allClients.FirstOrDefault(c => c.Cid == cboClient.SelectedClientId);
        var workerName = (cboWorker.SelectedItem as UserInfor)?.Name ?? cboWorker.Text.Trim();

        // DatePicker 手动输入日期时 SelectedDate 不会自动同步
        var printDate = dtBillDate.SelectedDate;
        if (DateTime.TryParse(dtBillDate.Text, out var ppd)) printDate = ppd;

        var billData = new BillPrintData
        {
            BillType = _isReturnMode ? "退货" : "销售",
            Sn = billNo,
            DateText = printDate?.ToString("yyyy-MM-dd") ?? "",
            PartnerName = cboClient.SearchText,
            PartnerPhone = clientObj?.Mobile ?? clientObj?.Tel ?? "",
            PartnerContact = clientObj?.Linkman ?? "",
            PartnerAddress = clientObj?.Address ?? "",
            WorkerName = workerName,
            TotalAmount = _viewModel.Details.Sum(d => d.Price * d.Amount),
            Cash = decimal.TryParse(txtCash.Text, out var c) ? c : 0,
            Weixin = decimal.TryParse(txtWeixin.Text, out var w) ? w : 0,
            Zhifubao = decimal.TryParse(txtZhifubao.Text, out var z) ? z : 0,
            Arrearage = decimal.TryParse(txtArrear.Text, out var a) ? a : 0,
            Memo = txtMemo.Text?.Trim() ?? "",
            DeliveryMethod = "自提"
        };
        await billData.LoadCompanyInfoAsync();

        var idx = 1;
        foreach (var d in _viewModel.Details)
        {
            billData.Items.Add(new BillPrintItem
            {
                Index = idx++,
                PartNo = d.PartNo,
                PartName = d.PartName,
                Cartype = d.Cartype,
                Unit = "",
                Price = d.Price,
                PfPrice = 0,
                BillPrice = d.BillPrice,
                Amount = (int)d.Amount,
                Subtotal = d.SubTotal,
                Place = d.Place ?? "",
                Area = "",
                Brand = "",
                DiscountRate = 0,
                Memo = d.Memo
            });
        }

        return billData;
    }

    private void ClearBill()
    {
        _selectedBill = null;
        _viewModel.IsEditMode = false;  // 重置编辑模式标志
        _viewModel.EditSn = null;       // 重置编辑单号
        _viewModel.Details.Clear();
        // 清空条件查询区输入框
        txtPartNo.Text = "";
        txtPartName.Text = "";
        txtCartype.Text = "";
        txtClass.Text = "";
        txtBillNo.Text = "";
        dtBillDate.SelectedDate = DateTime.Now;
        cboClient.ClearSelection();
        // 注意：业务员(cboWorker)不在 ClearBill 中重置，避免查询/开单模式切换时丢失选择
        // 仅在新建(F1)和取消(F9)时才重置业务员
        txtCheckno.Text = "";
        txtDiscountRate.Text = "0";
        txtTotal.Text = "";
        txtBillTotal.Text = "";
        txtCash.Text = "0";
        txtChecks.Text = "0";
        txtZhifubao.Text = "0";
        txtWeixin.Text = "0";
        txtArrear.Text = "0";
        txtMemo.Text = "";
        txtSumTotal.Text = "0.00";
        txtSumBillTotal.Text = "0.00";
        txtSumAmount.Text = "0";

        if (_isReturnMode)
        {
            _isReturnMode = false;
            _viewModel.IsReturnMode = false;
            txtModeIndicator.Visibility = Visibility.Collapsed;
            dgDetails.Columns[2].Header = "数量";
        }
    }
}
