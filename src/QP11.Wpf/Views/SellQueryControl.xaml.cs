using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Constants;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using QP11.Wpf.Helpers;
using QP11.Wpf.ViewModels;

namespace QP11.Wpf.Views;

public partial class SellQueryControl : UserControl, ITabContent
{
    private readonly SellQueryViewModel _viewModel;
    private List<ClientInfor> _allClients = new();
    private List<WorkerItem> _allWorkers = new();
    private List<ActiveClientItem> _allActiveClients = new();
    private List<ActiveClientItem>? _customOrder = null;
    private bool _isDetailMode = false;
    private List<SellBillItem> _currentBills = new();
    private List<SellQueryDetailItem> _currentDetails = new();
    private bool _syncingSelection = false;
    // Shift+拖拽批量勾选状态
    private bool _isDragSelecting = false;
    private int _dragStartIndex = -1;

    public string TabTitle => "销售明细查询";
    public bool HasUnsavedChanges => false;
    public event EventHandler? RequestClose;

    public SellQueryControl(SellQueryViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        var lastMonth = DateTime.Today.AddMonths(-1);
        dtStart.SelectedDate = new DateTime(lastMonth.Year, lastMonth.Month, 1);
        dtEnd.SelectedDate = new DateTime(lastMonth.Year, lastMonth.Month, DateTime.DaysInMonth(lastMonth.Year, lastMonth.Month));
        Loaded += SellQueryControl_Loaded;
        KeyDown += SellQueryControl_KeyDown;

        // Shift+拖拽批量勾选：用AddHandler强制接收被DataGrid内部标记为已处理的MouseMove/MouseUp事件
        dgBills.AddHandler(UIElement.MouseMoveEvent, (MouseEventHandler)Dg_MouseMove, true);
        dgBills.AddHandler(UIElement.MouseUpEvent, (MouseButtonEventHandler)Dg_MouseUp, true);
        dgDetails.AddHandler(UIElement.MouseMoveEvent, (MouseEventHandler)Dg_MouseMove, true);
        dgDetails.AddHandler(UIElement.MouseUpEvent, (MouseButtonEventHandler)Dg_MouseUp, true);
    }

    private void SellQueryControl_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F3)
        {
            ToggleMode();
            e.Handled = true;
        }
    }

    private void BtnSwitchMode_Click(object sender, RoutedEventArgs e) => ToggleMode();

    private void ToggleMode()
    {
        _isDetailMode = !_isDetailMode;
        if (_isDetailMode)
        {
            panelBills.Visibility = Visibility.Collapsed;
            panelDetails.Visibility = Visibility.Visible;
            btnSwitchMode.Content = "切换销售单据(F3)";
        }
        else
        {
            panelBills.Visibility = Visibility.Visible;
            panelDetails.Visibility = Visibility.Collapsed;
            btnSwitchMode.Content = "切换销售明细(F3)";
        }
    }

    private async void SellQueryControl_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var clientRepo = App.ServiceProvider.GetRequiredService<IClientRepository>();
            _allClients = (await clientRepo.GetAllAsync()).ToList();
            cmbClient.SetClients(_allClients);

            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            var rows = await db.QueryAsync("SELECT workid, name FROM work_infor ORDER BY workid");
            _allWorkers = rows.Select(r => new WorkerItem { Workid = (string)r.workid, Name = (string)r.name }).ToList();
            cmbWorker.ItemsSource = _allWorkers;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "SellQueryControl_Loaded 失败");
        }

        dtStart.SelectedDateChanged += OnDateRangeChanged;
        dtEnd.SelectedDateChanged += OnDateRangeChanged;

        await LoadActiveClients(dtStart.SelectedDate!.Value, dtEnd.SelectedDate!.Value);
    }

    private async System.Threading.Tasks.Task LoadActiveClients(DateTime startDate, DateTime endDate)
    {
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            var sql = @"SELECT c.cid AS Cid, c.name AS Name,
                        ISNULL(s.SellCount, 0) AS SellCount,
                        ISNULL(s.TotalAmount, 0) AS TotalAmount
                        FROM client_infor c
                        LEFT JOIN (
                            SELECT bs.client,
                                   COUNT(DISTINCT bs.sn) AS SellCount,
                                   ISNULL(SUM(ds.stotal), 0) AS TotalAmount
                            FROM bill_sell bs
                            INNER JOIN detail_sell ds ON ds.sn = bs.sn
                            WHERE bs.datetime >= @Start
                              AND bs.datetime < DATEADD(day, 1, @End)
                              AND ISNULL(bs.flag, 0) <> -1
                              AND ds.amount <> 0
                            GROUP BY bs.client
                        ) s ON s.client = c.cid
                        ORDER BY CASE WHEN ISNULL(s.SellCount, 0) > 0 THEN 0 ELSE 1 END,
                                 c.name";

            var clients = (await db.QueryAsync<ActiveClientItem>(sql,
                new { Start = startDate, End = endDate })).ToList();

            _allActiveClients = SortActiveClients(clients);

            if (_customOrder != null)
            {
                // 保留自定义排序：按原顺序从新数据中重新匹配客户，不在自定义排序中的不显示
                var reordered = new List<ActiveClientItem>();
                foreach (var item in _customOrder)
                {
                    var match = _allActiveClients.FirstOrDefault(c =>
                        c.Name?.Trim().Equals(item.Name?.Trim(), StringComparison.OrdinalIgnoreCase) == true);
                    if (match != null)
                        reordered.Add(match);
                }
                _customOrder = reordered;
                lstActiveClients.ItemsSource = _customOrder;
            }
            else
            {
                lstActiveClients.ItemsSource = _allActiveClients;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "加载活跃客户失败");
        }
    }

    private async void OnDateRangeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!dtStart.SelectedDate.HasValue || !dtEnd.SelectedDate.HasValue) return;
        if (dtStart.SelectedDate.Value > dtEnd.SelectedDate.Value) return;
        await LoadActiveClients(dtStart.SelectedDate.Value, dtEnd.SelectedDate.Value);
    }

    private void LstActiveClient_DoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (lstActiveClients.SelectedItem is not ActiveClientItem client) return;
        cmbClient.SearchText = client.Name;
        DoSearch();
    }

    private static List<ActiveClientItem> SortActiveClients(List<ActiveClientItem> clients)
    {
        return clients
            .OrderByDescending(c => IsHmName(c.Name))
            .ThenBy(c => IsHmName(c.Name) ? GetDistrict(c.Name) : 0)
            .ThenBy(c => c.Name ?? "")
            .ToList();
    }

    private static bool IsHmName(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length < 2) return false;
        var py = PinyinHelper.GetPinyinInitials(name[..2]);
        return py.Equals("HM", StringComparison.OrdinalIgnoreCase);
    }

    private static int GetDistrict(string? name)
    {
        if (string.IsNullOrEmpty(name)) return 0;
        var m = System.Text.RegularExpressions.Regex.Match(name, @"(\d+)区");
        return m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }

    private void BtnPasteOrder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new Window
        {
            Title = "粘贴排序",
            Width = 360,
            Height = 380,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            ResizeMode = ResizeMode.NoResize
        };
        var panel = new StackPanel { Margin = new Thickness(10) };
        panel.Children.Add(new TextBlock { Text = "请粘贴客户名称，每行一个：", Margin = new Thickness(0, 0, 0, 5) });
        var tb = new TextBox
        {
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Height = 200
        };
        panel.Children.Add(tb);
        var tip = new TextBlock { Text = "提示: 未匹配的客户将追加在末尾", FontSize = 11, Foreground = Brushes.Gray, Margin = new Thickness(0, 5, 0, 0) };
        panel.Children.Add(tip);
        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 8, 0, 0) };
        var btnOk = new Button { Content = "确定", Width = 70, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var btnCancel = new Button { Content = "取消", Width = 70, IsCancel = true };
        btnPanel.Children.Add(btnOk);
        btnPanel.Children.Add(btnCancel);
        panel.Children.Add(btnPanel);
        dlg.Content = panel;

        btnOk.Click += (_, _) =>
        {
            var text = tb.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(text)) { dlg.DialogResult = false; dlg.Close(); return; }

            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(l => l.Trim()).Where(l => !string.IsNullOrEmpty(l)).ToList();

            var matched = new List<ActiveClientItem>();
            foreach (var name in lines)
            {
                var found = _allActiveClients.FirstOrDefault(c =>
                    c.Name?.Trim().Equals(name.Trim(), StringComparison.OrdinalIgnoreCase) == true);
                if (found != null && !matched.Contains(found))
                    matched.Add(found);
            }

            _customOrder = matched;
            lstActiveClients.ItemsSource = _customOrder;
            dlg.DialogResult = true;
            dlg.Close();
        };
        btnCancel.Click += (_, _) => { dlg.DialogResult = false; dlg.Close(); };

        dlg.ShowDialog();
    }

    private void BtnResetOrder_Click(object sender, RoutedEventArgs e)
    {
        _customOrder = null;
        lstActiveClients.ItemsSource = _allActiveClients;
    }

    private async void DoSearch()
    {
        // 解绑旧事件
        UnbindSelectionEvents();

        var clientText = cmbClient.SearchText?.Trim() ?? "";
        var workerSelected = cmbWorker.SelectedValue?.ToString();
        string? workerText = null;
        if (!string.IsNullOrEmpty(workerSelected))
            workerText = workerSelected;
        else if (!string.IsNullOrWhiteSpace(cmbWorker.Text))
            workerText = cmbWorker.Text.Trim();

        await LoadBills(string.IsNullOrEmpty(clientText) ? null : clientText, workerText);
        await LoadDetailsCore(string.IsNullOrEmpty(clientText) ? null : clientText, workerText);

        // 绑定新事件
        BindSelectionEvents();
    }

    private async System.Threading.Tasks.Task LoadBills(string? client, string? worker)
    {
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            var sql = @"SELECT bs.sn AS Sn, bs.datetime AS Datetime,
                        ISNULL(ci.name, '') AS ClientName,
                        ISNULL(wi.name, ISNULL(bs.worker, '')) AS WorkerName,
                        ISNULL(bs.total, 0) AS Total,
                        ISNULL(bs.bill_total, 0) AS BillTotal,
                        ISNULL(bs.flag, 0) AS Flag,
                        ISNULL(bs.memo, '') AS Memo,
                        ISNULL(bs.arrear, 0) AS Arrear
                        FROM bill_sell bs
                        LEFT JOIN client_infor ci ON ci.cid = bs.client
                        LEFT JOIN work_infor wi ON wi.workid = bs.worker
                        WHERE ISNULL(bs.flag, 0) <> -1";

            if (dtStart.SelectedDate.HasValue) sql += $" AND bs.datetime >= '{dtStart.SelectedDate.Value:yyyy-MM-dd}'";
            if (dtEnd.SelectedDate.HasValue) sql += $" AND bs.datetime < DATEADD(day, 1, '{dtEnd.SelectedDate.Value:yyyy-MM-dd}')";
            if (!string.IsNullOrEmpty(client)) sql += $" AND ci.name LIKE '%{client.Replace("'", "''")}%'";
            if (!string.IsNullOrEmpty(worker))
            {
                var w = worker.Replace("'", "''");
                sql += $" AND (wi.name LIKE '%{w}%' OR bs.worker LIKE '%{w}%')";
            }
            sql += " ORDER BY bs.sn DESC";

            _currentBills = (await db.QueryAsync<SellBillItem>(sql)).ToList();
            dgBills.ItemsSource = _currentBills;

            txtBillCount.Text = $"单据数: {_currentBills.Count}";
            txtBillSumTotal.Text = _currentBills.Sum(r => r.Total).ToString("N2");
            txtBillSumBillTotal.Text = _currentBills.Sum(r => r.BillTotal).ToString("N2");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"查询单据失败: {ex.Message}", "错误");
        }
    }

    private async System.Threading.Tasks.Task LoadDetailsCore(string? client, string? worker)
    {
        try
        {
            var rawData = (await _viewModel.LoadDetailListAsync(
                dtStart.SelectedDate, dtEnd.SelectedDate, client, worker)).ToList();

            _currentDetails = rawData.Select(r => new SellQueryDetailItem
            {
                Sn = Convert.ToString(r.sn),
                Partid = r.partid as long?,
                Partno = Convert.ToString(r.partno),
                Name = Convert.ToString(r.name),
                Amount = Convert.ToDecimal(r.amount),
                Price = r.price as decimal?,
                BillPrice = r.bill_price as decimal?,
                Cartype = Convert.ToString(r.cartype),
                CarMark = Convert.ToString(r.car_mark),
                Memo = Convert.ToString(r.memo),
                Datetime = r.datetime as DateTime?,
                Unit = Convert.ToString(r.unit),
                Stotal = r.stotal as decimal?,
                Btotal = r.btotal as decimal?,
                Id = r.id as long?,
                Tsn = Convert.ToString(r.tsn),
                Type = Convert.ToString(r.type),
                Place = Convert.ToString(r.place),
                Flag = r.flag as int?,
                Cb = r.cb as int?,
                PartTh = Convert.ToString(r.part_th),
                PartGg = Convert.ToString(r.part_gg),
                PartCclb = Convert.ToString(r.part_cclb),
                BillFlag = r.bill_flag as int?,
                Client = Convert.ToString(r.client),
                Worker = Convert.ToString(r.worker)
            }).ToList();

            dgDetails.ItemsSource = _currentDetails;

            txtCount.Text = $"记录数: {_currentDetails.Count}";
            txtSumAmount.Text = _currentDetails.Sum(r => r.Amount ?? 0m).ToString();
            txtSumStotal.Text = _currentDetails.Sum(r => r.Stotal ?? 0m).ToString("N2");
            txtSumBtotal.Text = _currentDetails.Sum(r => r.Btotal ?? 0m).ToString("N2");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"查询明细失败: {ex.Message}", "错误");
        }
    }

    private async void LoadDetails()
    {
        var clientText = cmbClient.SearchText?.Trim() ?? "";
        var workerSelected = cmbWorker.SelectedValue?.ToString();
        string? workerText = null;
        if (!string.IsNullOrEmpty(workerSelected))
            workerText = workerSelected;
        else if (!string.IsNullOrWhiteSpace(cmbWorker.Text))
            workerText = cmbWorker.Text.Trim();

        await LoadBills(string.IsNullOrEmpty(clientText) ? null : clientText, workerText);
        await LoadDetailsCore(string.IsNullOrEmpty(clientText) ? null : clientText, workerText);
    }

    private void BtnSearch_Click(object sender, RoutedEventArgs e) => DoSearch();

    private async void DgBills_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.Column.Header?.ToString() != "备注") return;
        if (e.EditAction == DataGridEditAction.Cancel) return;

        var bill = e.Row.Item as SellBillItem;
        if (bill == null) return;

        var textBox = e.EditingElement as TextBox;
        var newMemo = textBox?.Text?.Trim() ?? "";

        if (newMemo == (bill.Memo ?? "")) return;

        var mainWin = Window.GetWindow(this) as MainWindow;
        if (mainWin?.CurrentUser == null) return;

        var pwdDlg = new MemoConfirmDialog(mainWin.CurrentUser) { Owner = mainWin };
        if (pwdDlg.ShowDialog() != true)
        {
            e.Cancel = true;
            return;
        }

        try
        {
            await _viewModel.UpdateMemoAsync(bill.Sn!, newMemo);
            bill.Memo = newMemo;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"修改备注失败: {ex.Message}", "错误");
            e.Cancel = true;
        }
    }

    private void DgBills_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        try
        {
            if (e.Row.Item is SellBillItem bill)
            {
                if (bill.Flag == (int)BusinessConstants.BillFlag.Returned)
                {
                    e.Row.Foreground = Brushes.Red;
                    e.Row.Background = new SolidColorBrush(Color.FromArgb(30, 255, 200, 200));
                }
                else if (bill.Flag == (int)BusinessConstants.BillFlag.Voided)
                {
                    e.Row.Foreground = Brushes.Gray;
                    e.Row.Background = new SolidColorBrush(Color.FromArgb(20, 200, 200, 200));
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

    private async void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        // 获取要打印的数据（优先选中行，否则全部）
        var hasAnySelection = _currentBills.Any(b => b.IsSelected) || _currentDetails.Any(d => d.IsSelected);
        var detailsData = hasAnySelection ? _currentDetails.Where(d => d.IsSelected).ToList() : _currentDetails;

        if (detailsData.Count == 0)
        {
            MessageBox.Show("没有可打印的数据", "提示");
            return;
        }

        try
        {
            // 按单号分组，每个单号生成一个打印预览
            var grouped = detailsData.GroupBy(d => d.Sn).ToList();
            foreach (var group in grouped)
            {
                var sn = group.Key;
                var firstDetail = group.First();
                var flag = firstDetail.BillFlag ?? firstDetail.Flag ?? 0;
                var isReturn = flag == 2;

                var billData = new BillPrintData
                {
                    BillType = isReturn ? "退货" : "销售",
                    Sn = sn,
                    DateText = firstDetail.Datetime?.ToString("yyyy-MM-dd") ?? "",
                    PartnerName = firstDetail.Client ?? "",
                    PartnerPhone = _allClients.FirstOrDefault(c => c.Cid == firstDetail.Client)?.Mobile
                        ?? _allClients.FirstOrDefault(c => c.Cid == firstDetail.Client)?.Tel ?? "",
                    PartnerContact = _allClients.FirstOrDefault(c => c.Cid == firstDetail.Client)?.Linkman ?? "",
                    PartnerAddress = _allClients.FirstOrDefault(c => c.Cid == firstDetail.Client)?.Address ?? "",
                    WorkerName = firstDetail.Worker ?? "",
                    TotalAmount = group.Sum(d => d.Stotal ?? 0m),
                    Arrearage = isReturn ? 0 : group.Sum(d => d.Stotal ?? 0m),
                    DeliveryMethod = "自提"
                };
                await billData.LoadCompanyInfoAsync();

                var idx = 1;
                foreach (var d in group)
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
                        Area = "",
                        Brand = "",
                        DiscountRate = 0,
                        Memo = d.Memo
                    });
                }

                var dlg = new PrintPreviewWindow(billData, $"{(isReturn ? "退货单" : "销售单")}-{sn}")
                {
                    Owner = Window.GetWindow(this)
                };
                dlg.ShowDialog();
                // 只预览第一个单号，避免弹出多个窗口
                break;
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打印预览失败: {ex.Message}", "错误");
        }
    }

    private void DgDetails_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        try
        {
            if (e.Row.Item is SellQueryDetailItem row)
            {
                var flag = row.BillFlag ?? row.Flag ?? 0;
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
            Serilog.Log.Warning(ex, "DgDetails_LoadingRow 失败");
        }
    }

    // ========== Shift+拖拽批量勾选 ==========

    private int GetRowIndexFromPoint(DataGrid dg, Point point)
    {
        // 使用 HitTest 回调方式查找 DataGridRow，比 InputHitTest 更可靠
        DataGridRow? hitRow = null;
        VisualTreeHelper.HitTest(dg, null,
            new HitTestResultCallback(r =>
            {
                if (r is HitTestResult visualResult)
                {
                    var dep = visualResult.VisualHit;
                    // 向上查找 DataGridRow
                    while (dep != null)
                    {
                        if (dep is DataGridRow row)
                        {
                            hitRow = row;
                            return HitTestResultBehavior.Stop;
                        }
                        if (dep is DataGrid dgParent && dgParent != dg)
                            return HitTestResultBehavior.Continue; // 嵌套DataGrid跳过
                        dep = VisualTreeHelper.GetParent(dep);
                    }
                }
                return HitTestResultBehavior.Continue;
            }),
            new PointHitTestParameters(point));

        return hitRow == null ? -1 : dg.ItemContainerGenerator.IndexFromContainer(hitRow);
    }

    private void Dg_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left) return;
        if (!Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift)) return;

        var dg = (DataGrid)sender;
        var index = GetRowIndexFromPoint(dg, e.GetPosition(dg));
        if (index < 0) return;

        _isDragSelecting = true;
        _dragStartIndex = index;
        // 立即选中起始行
        ApplyRangeSelection(dg, _dragStartIndex, _dragStartIndex);
        e.Handled = true;
    }

    private void Dg_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragSelecting || _dragStartIndex < 0) return;
        if (e.LeftButton != MouseButtonState.Pressed) return; // 确保左键仍按下

        var dg = (DataGrid)sender;
        var currentIndex = GetRowIndexFromPoint(dg, e.GetPosition(dg));
        if (currentIndex < 0) return;

        ApplyRangeSelection(dg, _dragStartIndex, currentIndex);
    }

    private void Dg_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (!_isDragSelecting) return;
        if (e.ChangedButton != MouseButton.Left) return;
        _isDragSelecting = false;
        _dragStartIndex = -1;
    }

    private void ApplyRangeSelection(DataGrid dg, int startIdx, int currentIdx)
    {
        System.Collections.IList sourceItems = dg.Name == "dgBills" ? _currentBills : (System.Collections.IList)_currentDetails;
        var min = Math.Min(startIdx, currentIdx);
        var max = Math.Max(startIdx, currentIdx);

        UnbindSelectionEvents();
        try
        {
            for (int i = 0; i < sourceItems.Count; i++)
            {
                var item = sourceItems[i]!;
                bool shouldSelect = i >= min && i <= max;
                if (dg.Name == "dgBills")
                    ((SellBillItem)item).IsSelected = shouldSelect;
                else
                    ((SellQueryDetailItem)item).IsSelected = shouldSelect;
            }
        }
        finally { BindSelectionEvents(); }

        // 批量勾选后手动同步联动
        if (dg.Name == "dgBills")
        {
            // 单据勾选变化 → 同步明细
            _syncingSelection = true;
            try
            {
                for (int i = min; i <= max && i < _currentBills.Count; i++)
                {
                    var bill = _currentBills[i];
                    foreach (var detail in _currentDetails)
                    {
                        if (detail.Sn == bill.Sn)
                            detail.IsSelected = bill.IsSelected;
                    }
                }
            }
            finally { _syncingSelection = false; }
        }
        else
        {
            // 明细勾选变化 → 同步单据
            _syncingSelection = true;
            try
            {
                var changedSns = new HashSet<string>();
                for (int i = min; i <= max && i < _currentDetails.Count; i++)
                    changedSns.Add(_currentDetails[i].Sn ?? "");
                foreach (var bill in _currentBills)
                {
                    if (changedSns.Contains(bill.Sn ?? ""))
                        bill.IsSelected = _currentDetails.Any(d => d.Sn == bill.Sn && d.IsSelected);
                }
            }
            finally { _syncingSelection = false; }
        }
    }

    private void DgDetails_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (dgDetails.SelectedItem is not SellQueryDetailItem row) return;
        var sn = row.Sn;
        if (string.IsNullOrEmpty(sn)) return;

        var mainWin = Window.GetWindow(this) as MainWindow;
        if (mainWin == null) return;

        var sellControl = new SellControl(App.ServiceProvider.GetRequiredService<SellViewModel>());
        mainWin.OpenTab($"sell_edit_{sn}", $"编辑-{sn}", sellControl);
        sellControl.Dispatcher.BeginInvoke(new Action(() =>
        {
            sellControl.LoadBillForEdit(sn);
        }), System.Windows.Threading.DispatcherPriority.Loaded);
    }

    private async void BtnVoid_Click(object sender, RoutedEventArgs e)
    {
        if (dgDetails.SelectedItem is not SellQueryDetailItem row) return;
        var sn = row.Sn;
        if (string.IsNullOrEmpty(sn)) return;
        if (MessageBox.Show($"确定删除单据 [{sn}]? 删除后不可恢复，库存将回补", "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        try
        {
            await _viewModel.VoidBillAsync(sn);
            LoadDetails();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"删除失败: {ex.Message}", "错误");
        }
    }

    private void BtnBatchSettle_Click(object sender, RoutedEventArgs e)
    {
        var selectedBills = _currentBills.Where(b => b.IsSelected).ToList();
        if (selectedBills.Count == 0)
        {
            MessageBox.Show("请先勾选需要做账的单据", "提示");
            return;
        }

        // 弹出做账对话框
        var dlg = new Window
        {
            Title = "一键做账",
            Width = 480,
            Height = 420,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            ResizeMode = ResizeMode.NoResize
        };

        var mainPanel = new StackPanel { Margin = new Thickness(12) };

        // 标题信息
        var hdrPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        hdrPanel.Children.Add(new TextBlock { Text = $"已选 {selectedBills.Count} 张单据", FontWeight = FontWeights.SemiBold });
        mainPanel.Children.Add(hdrPanel);

        // 支付方式选择
        var payPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 8) };
        payPanel.Children.Add(new TextBlock { Text = "收款方式:", VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) });

        var rbCash = new RadioButton { Content = "现金", Tag = "cash", Margin = new Thickness(0, 0, 10, 0), IsChecked = true, GroupName = "PayMethod" };
        var rbWeixin = new RadioButton { Content = "微信", Tag = "weixin", Margin = new Thickness(0, 0, 10, 0), GroupName = "PayMethod" };
        var rbAlipay = new RadioButton { Content = "支付宝", Tag = "zhifubao", Margin = new Thickness(0, 0, 10, 0), GroupName = "PayMethod" };
        var rbBank = new RadioButton { Content = "银行卡", Tag = "checks", GroupName = "PayMethod" };

        payPanel.Children.Add(rbCash);
        payPanel.Children.Add(rbWeixin);
        payPanel.Children.Add(rbAlipay);
        payPanel.Children.Add(rbBank);
        mainPanel.Children.Add(payPanel);

        // 单据列表
        mainPanel.Children.Add(new TextBlock { Text = "── 待做账单据 ──", Margin = new Thickness(0, 4, 0, 4), Foreground = Brushes.Gray });

        var dg = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,
            SelectionMode = DataGridSelectionMode.Single,
            MaxHeight = 200,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto
        };
        dg.Columns.Add(new DataGridTextColumn { Header = "单号", Binding = new System.Windows.Data.Binding("Sn"), Width = new DataGridLength(120) });
        dg.Columns.Add(new DataGridTextColumn { Header = "客户", Binding = new System.Windows.Data.Binding("ClientName"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        dg.Columns.Add(new DataGridTextColumn { Header = "挂账金额", Binding = new System.Windows.Data.Binding("Arrear") { StringFormat = "N2" }, Width = new DataGridLength(100) });

        mainPanel.Children.Add(dg);

        // 总金额
        var totalBar = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 6, 0, 0) };
        var txtTotalArrear = new TextBlock { FontWeight = FontWeights.Bold };
        totalBar.Children.Add(new TextBlock { Text = "总挂账金额: ", VerticalAlignment = VerticalAlignment.Center });
        totalBar.Children.Add(txtTotalArrear);
        mainPanel.Children.Add(totalBar);

        // 按钮
        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 10, 0, 0) };
        var btnCancel = new Button { Content = "取消", Width = 70, Margin = new Thickness(0, 0, 10, 0), IsCancel = true };
        var btnOk = new Button { Content = "确定做账", Width = 80, IsDefault = true };
        btnPanel.Children.Add(btnCancel);
        btnPanel.Children.Add(btnOk);
        mainPanel.Children.Add(btnPanel);

        dlg.Content = mainPanel;

        // 异步加载挂账数据
        var arrearList = new List<ArrearBillInfo>();
        _ = LoadArrearDataForDialog(selectedBills, dg, txtTotalArrear, arrearList);

        btnOk.Click += async (_, _) =>
        {
            // 获取选中的支付方式
            var selectedRb = new[] { rbCash, rbWeixin, rbAlipay, rbBank }.FirstOrDefault(r => r.IsChecked == true);
            if (selectedRb == null) return;

            var payMethod = selectedRb.Tag as string;
            if (arrearList.Count == 0)
            {
                MessageBox.Show("没有可做账的挂账单据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var totalArrear = arrearList.Sum(a => a.Arrear);
            var methodNames = new Dictionary<string, string>
            {
                { "cash", "现金" }, { "weixin", "微信" }, { "zhifubao", "支付宝" }, { "checks", "银行卡" }
            };
            var msg = $"确定将以下 {arrearList.Count} 张单据的挂账金额转为 [{methodNames[payMethod!]}] 收款吗？\n\n总金额: ¥{totalArrear:N2}";
            if (MessageBox.Show(msg, "确认做账", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;

            try
            {
                var sns = arrearList.Select(a => a.Sn!);
                var rows = await _viewModel.BatchSettleArrearAsync(sns, payMethod!);
                MessageBox.Show($"做账成功！共处理 {rows} 张单据，金额 ¥{totalArrear:N2}", "提示");
                dlg.DialogResult = true;
                dlg.Close();
                DoSearch(); // 刷新数据
            }
            catch (Exception ex)
            {
                MessageBox.Show($"做账失败: {ex.Message}", "错误");
            }
        };

        btnCancel.Click += (_, _) => { dlg.DialogResult = false; dlg.Close(); };
        dlg.ShowDialog();
    }

    private async System.Threading.Tasks.Task LoadArrearDataForDialog(
        List<SellBillItem> selectedBills, DataGrid dg, TextBlock txtTotalArrear, List<ArrearBillInfo> arrearList)
    {
        try
        {
            var sns = selectedBills.Select(b => b.Sn!).Where(s => !string.IsNullOrEmpty(s));
            // 查询所有勾选单据的挂账金额（不过滤arrear=0，让用户看到完整选择列表）
            var data = (await _viewModel.GetArrearBillsAllAsync(sns)).ToList();

            // 补充客户名称，显示所有勾选单据
            var displayList = selectedBills.Select(bill =>
            {
                var info = data.FirstOrDefault(a => a.Sn == bill.Sn);
                return new { bill.Sn, ClientName = bill.ClientName ?? "", Arrear = info?.Arrear ?? 0m };
            }).ToList();

            // 只将有挂账的记录加入待做账列表
            arrearList.Clear();
            arrearList.AddRange(data.Where(a => a.Arrear > 0.01m || a.Arrear < -0.01m));

            _ = Dispatcher.BeginInvoke(() =>
            {
                dg.ItemsSource = displayList;
                txtTotalArrear.Text = $"¥{arrearList.Sum(a => a.Arrear):N2}";
            });
        }
        catch (Exception ex)
        {
            _ = Dispatcher.BeginInvoke(() => txtTotalArrear.Text = $"加载失败: {ex.Message}");
        }
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var hasAnySelection = _currentBills.Any(b => b.IsSelected) || _currentDetails.Any(d => d.IsSelected);

        IEnumerable<SellBillItem> billsSource = hasAnySelection ? _currentBills.Where(b => b.IsSelected).ToList() : _currentBills;
        IEnumerable<SellQueryDetailItem> detailsSource = hasAnySelection ? _currentDetails.Where(d => d.IsSelected).ToList() : _currentDetails;

        var originalBills = dgBills.ItemsSource;
        var originalDetails = dgDetails.ItemsSource;

        dgBills.ItemsSource = billsSource;
        dgDetails.ItemsSource = detailsSource;

        var win = new ImagePreviewWindow(new[] { (dgBills, "销售单据"), (dgDetails, "销售明细") });
        win.Owner = Window.GetWindow(this);
        win.ShowDialog();

        dgBills.ItemsSource = originalBills;
        dgDetails.ItemsSource = originalDetails;
    }

    private async void BtnExportTable_Click(object sender, RoutedEventArgs e)
    {
        var hasAnySelection = _currentBills.Any(b => b.IsSelected) || _currentDetails.Any(d => d.IsSelected);

        var billsData = hasAnySelection ? _currentBills.Where(b => b.IsSelected).ToList() : _currentBills;
        var detailsData = hasAnySelection ? _currentDetails.Where(d => d.IsSelected).ToList() : _currentDetails;

        var clientName = string.IsNullOrWhiteSpace(cmbClient.SearchText) ? "全部客户" : cmbClient.SearchText.Trim();
        var dateRange = $"{dtStart.SelectedDate:yyyy-MM-dd}~{dtEnd.SelectedDate:yyyy-MM-dd}";
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "导出表格",
            Filter = "Excel 文件|*.xlsx",
            FileName = $"{clientName}销售单据{dateRange}.xlsx"
        };
        if (dlg.ShowDialog() != true) return;

        // 构建 DataTable: 销售单据
        var billsTable = new System.Data.DataTable("销售单据");
        billsTable.Columns.Add("单号", typeof(string));
        billsTable.Columns.Add("日期", typeof(string));
        billsTable.Columns.Add("客户", typeof(string));
        billsTable.Columns.Add("业务员", typeof(string));
        billsTable.Columns.Add("售价总额", typeof(string));
        billsTable.Columns.Add("开票总额", typeof(string));
        billsTable.Columns.Add("状态", typeof(string));
        billsTable.Columns.Add("备注", typeof(string));

        foreach (var b in billsData)
        {
            billsTable.Rows.Add(b.Sn, b.Datetime?.ToString("yyyy-MM-dd"), b.ClientName, b.WorkerName,
                b.Total.ToString("N2"), b.BillTotal.ToString("N2"), b.FlagText, b.Memo);
        }

        // 销售单据合计行
        var billSumTotal = billsData.Sum(b => b.Total).ToString("N2");
        var billSumBillTotal = billsData.Sum(b => b.BillTotal).ToString("N2");
        billsTable.Rows.Add($"共{billsData.Count}条", "", "", "", billSumTotal, billSumBillTotal, "", "");

        // 收集退货单据行索引
        var billRedRows = new HashSet<int>();
        for (int i = 0; i < billsData.Count; i++)
            if (billsData[i].Flag == (int)BusinessConstants.BillFlag.Returned) billRedRows.Add(i);

        // 构建 DataTable: 销售明细
        var detailsTable = new System.Data.DataTable("销售明细");
        detailsTable.Columns.Add("单号", typeof(string));
        detailsTable.Columns.Add("日期", typeof(string));
        detailsTable.Columns.Add("客户", typeof(string));
        detailsTable.Columns.Add("配件编号", typeof(string));
        detailsTable.Columns.Add("配件名称", typeof(string));
        detailsTable.Columns.Add("车型", typeof(string));
        detailsTable.Columns.Add("数量", typeof(string));
        detailsTable.Columns.Add("售价", typeof(string));
        detailsTable.Columns.Add("开票单价", typeof(string));
        detailsTable.Columns.Add("小计", typeof(string));
        detailsTable.Columns.Add("开票总额", typeof(string));
        detailsTable.Columns.Add("业务员", typeof(string));
        detailsTable.Columns.Add("单位", typeof(string));
        detailsTable.Columns.Add("备注", typeof(string));

        foreach (var d in detailsData)
        {
            detailsTable.Rows.Add(d.Sn, d.Datetime?.ToString("yyyy-MM-dd"), d.Client, d.Partno, d.Name,
                d.Cartype,
                (d.Amount ?? 0m).ToString(), (d.Price ?? 0m).ToString("N2"),
                (d.BillPrice ?? 0m).ToString("N2"), (d.Stotal ?? 0m).ToString("N2"),
                (d.Btotal ?? 0m).ToString("N2"),
                d.Worker, d.Unit, d.Memo);
        }

        // 销售明细合计行
        var detailSumAmount = detailsData.Sum(d => d.Amount ?? 0m).ToString();
        var detailSumStotal = detailsData.Sum(d => d.Stotal ?? 0m).ToString("N2");
        var detailSumBtotal = detailsData.Sum(d => d.Btotal ?? 0m).ToString("N2");
        detailsTable.Rows.Add($"共{detailsData.Count}条", "", "", "", "", "", detailSumAmount, "", "", detailSumStotal, detailSumBtotal, "", "", "");

        // 收集退货行索引（参考图片生成：BillFlag ?? Flag == 2）
        var detailRedRows = new HashSet<int>();
        for (int i = 0; i < detailsData.Count; i++)
            if ((detailsData[i].BillFlag ?? detailsData[i].Flag ?? 0) == 2) detailRedRows.Add(i);

        try
        {
            var exportSvc = new QP11.Services.ExportService();
            var (path, error) = await exportSvc.ExportMultiSheetAsync(
                Path.GetFileName(dlg.FileName),
                (billsTable, "销售单据", billRedRows),
                (detailsTable, "销售明细", detailRedRows));
            if (error != null)
                MessageBox.Show(error, "导出失败");
            else
                MessageBox.Show($"导出成功：{path}", "提示");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"导出失败：{ex.Message}", "错误");
        }
    }

    private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
    {
        if (_isDetailMode)
            foreach (var d in _currentDetails) d.IsSelected = true;
        else
            foreach (var b in _currentBills) b.IsSelected = true;
    }

    private void BtnInvertSelection_Click(object sender, RoutedEventArgs e)
    {
        if (_isDetailMode)
            foreach (var d in _currentDetails) d.IsSelected = !d.IsSelected;
        else
            foreach (var b in _currentBills) b.IsSelected = !b.IsSelected;
    }

    private void BindSelectionEvents()
    {
        foreach (var b in _currentBills)
            b.OnIsSelectedChanged += SyncDetailSelection;
        foreach (var d in _currentDetails)
            d.OnIsSelectedChanged += SyncBillSelection;
    }

    private void UnbindSelectionEvents()
    {
        foreach (var b in _currentBills)
            b.OnIsSelectedChanged -= SyncDetailSelection;
        foreach (var d in _currentDetails)
            d.OnIsSelectedChanged -= SyncBillSelection;
    }

    private void SyncDetailSelection(SellBillItem bill)
    {
        if (_syncingSelection) return;
        _syncingSelection = true;
        try
        {
            foreach (var detail in _currentDetails)
            {
                if (detail.Sn == bill.Sn)
                    detail.IsSelected = bill.IsSelected;
            }
        }
        finally { _syncingSelection = false; }
    }

    private void SyncBillSelection(SellQueryDetailItem detail)
    {
        if (_syncingSelection) return;
        _syncingSelection = true;
        try
        {
            var bill = _currentBills.FirstOrDefault(b => b.Sn == detail.Sn);
            if (bill == null) return;
            var anySelected = _currentDetails.Any(d => d.Sn == detail.Sn && d.IsSelected);
            bill.IsSelected = anySelected;
        }
        finally { _syncingSelection = false; }
    }

    public void OnAdd() { }
    public void OnEdit() { }
    public void OnQuery() => DoSearch();
    public void OnDelete() { }
    public void OnSave() { }
    public void OnSettle() { }
    public void OnPrint() => BtnPrint_Click(this, new RoutedEventArgs());
    public void OnReturn() { }
    public void OnCancel() { }
    public void OnHistory() { }
    public void OnClose() => RequestClose?.Invoke(this, EventArgs.Empty);
}

public class WorkerItem
{
    public string Workid { get; set; } = "";
    public string Name { get; set; } = "";
    public override string ToString() => Name;
}

public class ActiveClientItem
{
    public string Cid { get; set; } = "";
    public string Name { get; set; } = "";
    public int SellCount { get; set; }
    public decimal TotalAmount { get; set; }
    public override string ToString() => Name;
}

public class SellBillItem : System.ComponentModel.INotifyPropertyChanged
{
    public string? Sn { get; set; }
    public DateTime? Datetime { get; set; }
    public string? ClientName { get; set; }
    public string? WorkerName { get; set; }
    public decimal Total { get; set; }
    public decimal BillTotal { get; set; }
    public int Flag { get; set; }
    public string? Memo { get; set; }
    public decimal Arrear { get; set; }

    public string FlagText => Flag switch
    {
        2 => "退货",
        3 => "作废",
        _ => "正常"
    };

    public string SettleStatusText => Math.Abs(Arrear) > 0.01m ? "未做账" : "已做账";

    public System.Windows.Media.Brush SettleStatusColor =>
        Math.Abs(Arrear) > 0.01m ? System.Windows.Media.Brushes.Red : System.Windows.Media.Brushes.Green;

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); OnIsSelectedChanged?.Invoke(this); } }
    }

    public event Action<SellBillItem>? OnIsSelectedChanged;
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}

public class SellQueryDetailItem : System.ComponentModel.INotifyPropertyChanged
{
    public string? Sn { get; set; }
    public long? Partid { get; set; }
    public string? Partno { get; set; }
    public string? Name { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Price { get; set; }
    public decimal? BillPrice { get; set; }
    public string? Cartype { get; set; }
    public string? CarMark { get; set; }
    public string? Memo { get; set; }
    public DateTime? Datetime { get; set; }
    public string? Unit { get; set; }
    public decimal? Stotal { get; set; }
    public decimal? Btotal { get; set; }
    public long? Id { get; set; }
    public string? Tsn { get; set; }
    public string? Type { get; set; }
    public string? Place { get; set; }
    public int? Flag { get; set; }
    public int? Cb { get; set; }
    public string? PartTh { get; set; }
    public string? PartGg { get; set; }
    public string? PartCclb { get; set; }
    public int? BillFlag { get; set; }
    public string? Client { get; set; }
    public string? Worker { get; set; }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); OnIsSelectedChanged?.Invoke(this); } }
    }

    public event Action<SellQueryDetailItem>? OnIsSelectedChanged;
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}
