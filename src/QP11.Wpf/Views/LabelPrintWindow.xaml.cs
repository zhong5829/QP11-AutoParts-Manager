using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using QP11.Core.Interfaces;
using QP11.Wpf.Controls;
using QP11.Wpf.Services;
using QP11.Wpf.Services.LabelPrint;

namespace QP11.Wpf.Views;

/// <summary>标签打印行模型（默认不勾选；文本属性支持变更通知，列表自动刷新）</summary>
public class LabelPrintRow : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private bool _isSelected;
    private string? _partNo;
    private string? _name;
    private string? _carType;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                OnChanged(nameof(IsSelected));
            }
        }
    }
    public string? PartNo
    {
        get => _partNo;
        set
        {
            if (_partNo != value)
            {
                _partNo = value;
                OnChanged(nameof(PartNo));
            }
        }
    }
    public string? Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnChanged(nameof(Name));
            }
        }
    }
    public string? CarType
    {
        get => _carType;
        set
        {
            if (_carType != value)
            {
                _carType = value;
                OnChanged(nameof(CarType));
            }
        }
    }
    public string? Place { get; set; }
    public long? Amount { get; set; }

    private void OnChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// 标签打印独立页：三条件实时查询（防抖）、左右方向键切换输入框并自动全选、
/// 车型可编辑（仅本次打印）、模板/份数/打印机设置、勾选打印。
/// </summary>
public partial class LabelPrintWindow : Window
{
    private const int PreviewLimit = 300;  // 预览最多渲染行数（×份数），打印不受限
    private readonly IPartRepository _partRepo;
    private CancellationTokenSource? _searchCts;
    private readonly ObservableCollection<LabelPrintRow> _items = new();
    /// <summary>抑制勾选批量更新时的重复预览刷新</summary>
    private bool _updatingSelection;

    public LabelPrintWindow(IPartRepository partRepo)
    {
        _partRepo = partRepo;
        InitializeComponent();
        dgItems.ItemsSource = _items;
        LoadTemplates();       // 下拉选中会触发 SelectionChanged → RefreshPreview，此时 _uiReady=false 被跳过
        LoadPrinters();
        _uiReady = true;
        LoadAsync();           // 直接加载首屏数据（WindowHostControl 托管下 Window 不触发 Loaded 事件）
    }

    private bool _uiReady;

    // ── 查询 ──

    private async void Txt_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _searchCts?.Cancel();
        var cts = new CancellationTokenSource();
        _searchCts = cts;
        try
        {
            await Task.Delay(300, cts.Token);
        }
        catch (OperationCanceledException) { return; }
        if (cts.IsCancellationRequested) return;
        LoadAsync();
    }

    private async void LoadAsync()
    {
        try
        {
            _items.Clear();
            dgItems.UnselectAll();
            var data = await _partRepo.GetLabelItemsAsync(txtPartNo.Text.Trim(), txtName.Text.Trim(), txtCarType.Text.Trim());
            foreach (var d in data)
            {
                var row = new LabelPrintRow
                {
                    IsSelected = false,   // 默认不勾选
                    PartNo = d.PartNo,
                    Name = d.Name,
                    CarType = d.CarType,
                    Place = d.Place,
                    Amount = d.Amount
                };
                row.PropertyChanged += Item_PropertyChanged;
                _items.Add(row);
            }
            chkAll.IsChecked = false;
            txtStatus.Text = $"共 {_items.Count} 条 · 标签按 仓位→零件编码 排序输出";
            RefreshPreview();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "标签打印查询失败");
            MessageBox.Show($"查询失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ── 输入框交互：自动全选 + 左右方向键切换 ──

    private void Txt_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is System.Windows.Controls.TextBox tb)
            tb.Dispatcher.BeginInvoke(() => tb.SelectAll(), DispatcherPriority.Input);
    }

    private void Txt_KeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not System.Windows.Controls.TextBox tb) return;
        bool atStart = tb.CaretIndex == 0;
        bool atEnd = tb.CaretIndex >= tb.Text.Length;
        bool allSelected = tb.SelectionLength > 0 && tb.SelectionLength == tb.Text.Length;

        // 光标在端部或全选状态时，左右方向键切换输入框
        if (e.Key == Key.Left && (atStart || allSelected))
        {
            e.Handled = true;
            MoveFocus(-1);
        }
        else if (e.Key == Key.Right && (atEnd || allSelected))
        {
            e.Handled = true;
            MoveFocus(1);
        }
    }

    private void MoveFocus(int delta)
    {
        var boxes = new[] { txtPartNo, txtName, txtCarType };
        int idx = -1;
        for (int i = 0; i < boxes.Length; i++)
        {
            if (boxes[i].IsKeyboardFocusWithin) { idx = i; break; }
        }
        if (idx < 0) return;
        int next = idx + delta;
        if (next >= 0 && next < boxes.Length)
            boxes[next].Dispatcher.BeginInvoke(() =>
            {
                boxes[next].Focus();
                boxes[next].SelectAll();
            }, DispatcherPriority.Input);
    }

    // ── 模板 ──

    private void LoadTemplates()
    {
        cboTemplate.Items.Clear();
        foreach (var tpl in LabelTemplateService.GetAll())
            cboTemplate.Items.Add(tpl);
        if (cboTemplate.Items.Count > 0)
            cboTemplate.SelectedIndex = 0;
    }

    private void Template_Changed(object sender, System.Windows.Controls.SelectionChangedEventArgs e) => RefreshPreview();

    private void BtnNewTemplate_Click(object sender, RoutedEventArgs e)
    {
        var cur = cboTemplate.SelectedItem as LabelTemplate;
        var dlg = new TemplateSpecWindow(
            cur == null ? "" : cur.Name + " 副本",
            cur?.LabelWidthMm ?? 50,
            cur?.LabelHeightMm ?? 30);
        // 本页被 WindowHostControl 托管（自身从未 Show），Owner 须用已显示的窗口
        if (Application.Current.MainWindow is { IsVisible: true } mainWin)
            dlg.Owner = mainWin;
        if (dlg.ShowDialog() != true) return;

        // 继承当前模板的字号，按新尺寸生成默认字段布局
        var tpl = cur?.Clone() ?? new LabelTemplate();
        tpl.Name = dlg.TemplateName;
        tpl.LabelWidthMm = dlg.WidthMm;
        tpl.LabelHeightMm = dlg.HeightMm;
        tpl.IsBuiltIn = false;
        tpl.Fields = new System.Collections.Generic.List<LabelField>();
        tpl.EnsureFields();

        if (!LabelTemplateService.AddCustom(tpl))
        {
            MessageBox.Show("模板名称已存在", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        LoadTemplates();
        SelectTemplate(tpl.Name);
    }

    private void BtnSaveTemplate_Click(object sender, RoutedEventArgs e)
    {
        if (cboTemplate.SelectedItem is not LabelTemplate cur)
        {
            MessageBox.Show("请先选择模板", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (cur.IsBuiltIn)
        {
            MessageBox.Show("内置模板不可保存，请用「新建」创建自定义模板。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!LabelTemplateService.SaveCustom(cur))
        {
            MessageBox.Show("保存失败：未找到同名自定义模板，请用「新建」创建。", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        MessageBox.Show("模板已保存，可直接复用", "提示");
    }

    /// <summary>按名称选中下拉模板</summary>
    private void SelectTemplate(string name)
    {
        for (int i = 0; i < cboTemplate.Items.Count; i++)
        {
            if (cboTemplate.Items[i] is LabelTemplate t && t.Name == name)
            {
                cboTemplate.SelectedIndex = i;
                break;
            }
        }
    }

    private void BtnDeleteTemplate_Click(object sender, RoutedEventArgs e)
    {
        var name = InputBoxDialog.Show("请输入要删除的自定义模板名称：", "删除模板");
        if (string.IsNullOrWhiteSpace(name)) return;
        if (!LabelTemplateService.DeleteCustom(name.Trim()))
        {
            MessageBox.Show("未找到该自定义模板（内置模板不可删除）", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        LoadTemplates();
        MessageBox.Show("已删除", "提示");
    }

    // ── 分机与份数 ──

    private void LoadPrinters()
    {
        cboPrinter.Items.Clear();
        var printServer = new System.Printing.LocalPrintServer();
        foreach (var queue in printServer.GetPrintQueues())
            cboPrinter.Items.Add(queue.Name);

        var settings = PrintSettingsService.Load();
        var savedName = settings.PagePrint.PrinterName;
        if (!string.IsNullOrEmpty(savedName))
        {
            for (int i = 0; i < cboPrinter.Items.Count; i++)
            {
                if (cboPrinter.Items[i]?.ToString() == savedName)
                {
                    cboPrinter.SelectedIndex = i;
                    break;
                }
            }
        }
        if (cboPrinter.SelectedIndex < 0 && cboPrinter.Items.Count > 0)
            cboPrinter.SelectedIndex = 0;
    }

    private int CurrentCopies()
    {
        if (int.TryParse(txtCopies.Text, out var c) && c > 0 && c <= 9999) return c;
        return 1;
    }

    private void Copies_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
        => RefreshPreview();

    private void ChkAll_Changed(object sender, RoutedEventArgs e)
    {
        var all = chkAll.IsChecked == true;
        _updatingSelection = true;
        try
        {
            foreach (var item in _items)
                item.IsSelected = all;
        }
        finally
        {
            _updatingSelection = false;
        }
        dgItems.Items.Refresh();
        RefreshPreview();
    }

    /// <summary>手动勾选/取消勾选时实时刷新预览</summary>
    private void Item_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(LabelPrintRow.IsSelected) && !_updatingSelection)
            RefreshPreview();
    }

    /// <summary>勾选列点击：立即提交单元格（否则要等失去焦点才写回）并刷新预览</summary>
    private void ChkItem_Click(object sender, RoutedEventArgs e)
    {
        dgItems.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true);
        dgItems.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
        RefreshPreview();
    }

    // ── 预览编辑（画布：多标签连排，拖动位置 / 字号 / 显隐，直写模板 Fields） ──

    private const int EditorMaxLabels = 500;     // 画布最多显示标签数（防止超大勾选量导致预览过载）
    private LabelField? _selectedField;
    private Border? _dragEl;
    private LabelField? _dragField;
    private Point _dragStart;                      // 按下时鼠标相对画布的位置
    private readonly Dictionary<Border, Point> _origins = new();  // 元素 → 所属标签在画布上的原点
    private readonly Dictionary<Border, LabelPrintRow> _rowByEl = new();  // 元素 → 其数据行（双击修改用）

    private void RefreshPreview()
    {
        // 构造期模板下拉触发的 SelectionChanged 回调直接跳过（UI 未就绪）
        if (!_uiReady) return;
        RenderEditor();
    }

    private void RenderEditor()
    {
        // 防御：UI 未就绪时跳过（如构造期模板下拉触发 SelectionChanged 的回调）
        if (editCanvas == null) return;
        _selectedField = null;
        _dragField = null;
        _dragEl = null;
        _origins.Clear();
        _rowByEl.Clear();
        editCanvas.Children.Clear();
        if (cboTemplate.SelectedItem is not LabelTemplate tpl) return;
        tpl.EnsureFields();

        // 内容：所有勾选行；无勾选时显示第一行
        var checkedRows = _items.Where(i => i.IsSelected).ToList();
        var sampleList = checkedRows.Count > 0 ? checkedRows : _items.Take(1).ToList();

        double mm = LabelLayoutBuilder.MmToPx;
        int cols = Math.Max(1, tpl.ColsPerRow);
        double lw = (tpl.LabelWidthMm + 2) * mm;   // 含 1px 外圈
        double lh = (tpl.LabelHeightMm + 2) * mm;
        double gap = Math.Max(8, tpl.GapMm * mm);

        int rows = (int)Math.Ceiling(sampleList.Count / (double)cols);
        double canvasW0 = cols * lw + (cols - 1) * gap;
        double canvasH0 = Math.Max(rows, 1) * lh + Math.Max(rows - 1, 0) * gap;

        // 标尺留白：预览顶部/左侧各留出标尺条宽度
        const double rulerSize = 22;
        editCanvas.Width = canvasW0 + rulerSize;
        editCanvas.Height = canvasH0 + rulerSize;

        var hRuler = new RulerControl { IsVertical = false, Width = canvasW0, Height = rulerSize, IsHitTestVisible = false };
        Canvas.SetLeft(hRuler, rulerSize);
        Canvas.SetTop(hRuler, 0);
        editCanvas.Children.Add(hRuler);

        var vRuler = new RulerControl { IsVertical = true, Width = rulerSize, Height = canvasH0, IsHitTestVisible = false };
        Canvas.SetLeft(vRuler, 0);
        Canvas.SetTop(vRuler, rulerSize);
        editCanvas.Children.Add(vRuler);

        int shown = 0;
        for (int r = 0; r < rows && shown < EditorMaxLabels; r++)
        {
            for (int c = 0; c < cols && shown < EditorMaxLabels; c++)
            {
                int idx = r * cols + c;
                if (idx >= sampleList.Count) break;
                var src = sampleList[idx];
                AddLabelToCanvas(src, tpl, new Point(rulerSize + c * (lw + gap), rulerSize + r * (lh + gap)));
                shown++;
            }
        }
        UpdateSelectedLabel();
        if (sampleList.Count > EditorMaxLabels)
            txtSelected.Text = $"预览前 {EditorMaxLabels} 张 / 共 {sampleList.Count} 张";
    }

    /// <summary>在画布 origin 处渲染一张完整标签（外框 + 可编辑字段元素），并记录元素对应的数据行</summary>
    private void AddLabelToCanvas(LabelPrintRow src, LabelTemplate tpl, Point origin)
    {
        double mm = LabelLayoutBuilder.MmToPx;
        var item = new LabelPrintItem { PartNo = src.PartNo, Name = src.Name, CarType = src.CarType };

        var labelBorder = new Border
        {
            Width = tpl.LabelWidthMm * mm,
            Height = tpl.LabelHeightMm * mm,
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(1),
            Background = Brushes.White
        };
        Canvas.SetLeft(labelBorder, origin.X + 1);
        Canvas.SetTop(labelBorder, origin.Y + 1);
        editCanvas.Children.Add(labelBorder);

        foreach (var f in tpl.Fields)
        {
            if (f == null || !f.Visible) continue;
            var brd = CreateEditorItem(item, tpl, f);
            if (brd == null) continue;
            brd.Tag = f;
            brd.MouseLeftButtonDown += El_MouseDown;
            Canvas.SetLeft(brd, origin.X + 1 + f.XMm * mm);
            Canvas.SetTop(brd, origin.Y + 1 + f.YMm * mm);
            editCanvas.Children.Add(brd);
            _origins[brd] = origin;
            _rowByEl[brd] = src;
        }
    }

    private Border? CreateEditorItem(LabelPrintItem item, LabelTemplate tpl, LabelField f)
    {
        double mm = LabelLayoutBuilder.MmToPx;
        double maxW = Math.Max(24, (tpl.LabelWidthMm - f.XMm) * mm - 6);
        UIElement content;
        switch (f.Key)
        {
            case LabelTemplate.FieldCode:
                content = new TextBlock { Text = item.PartNo ?? "", FontSize = f.FontSize, FontWeight = FontWeights.Bold, MaxWidth = maxW, TextTrimming = TextTrimming.CharacterEllipsis };
                break;
            case LabelTemplate.FieldBarcode:
                var bh = Math.Max(4 * mm, f.FontSize * mm);
                content = new Image { Source = Code128Renderer.Render(item.PartNo ?? "", maxW, bh, 0.8), Width = maxW, Height = bh, Stretch = Stretch.Fill };
                break;
            case LabelTemplate.FieldName:
                content = new TextBlock { Text = item.Name ?? "", FontSize = f.FontSize, MaxWidth = maxW, TextTrimming = TextTrimming.CharacterEllipsis };
                break;
            case LabelTemplate.FieldCarType:
                content = new TextBlock { Text = "车型：" + (item.CarType ?? ""), FontSize = f.FontSize, Foreground = Brushes.DimGray, MaxWidth = maxW, TextTrimming = TextTrimming.CharacterEllipsis };
                break;
            default:
                return null;
        }
        return new Border { Child = content, Background = Brushes.Transparent, Cursor = Cursors.SizeAll };
    }

    private void El_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border brd || brd.Tag is not LabelField f) return;
        // 双击：修改该行文字内容（仅本次打印，不回写数据库）
        if (e.ClickCount >= 2)
        {
            El_MouseDoubleClick(brd, f);
            e.Handled = true;
            return;
        }
        _selectedField = f;
        _dragField = f;
        _dragEl = brd;
        var pos = e.GetPosition(editCanvas);
        _dragStart = new Point(pos.X - Canvas.GetLeft(brd), pos.Y - Canvas.GetTop(brd));

        // 只清“字段元素”的选中态，不碰标签外框（否则拖动时外框会消失）
        foreach (var child in editCanvas.Children)
        {
            if (child is Border b && b.Tag is LabelField && b != brd)
                b.BorderThickness = new Thickness(0);
        }
        brd.BorderBrush = Brushes.DodgerBlue;
        brd.BorderThickness = new Thickness(1);
        brd.CaptureMouse();
        UpdateSelectedLabel();
        e.Handled = true;
    }

    /// <summary>双击字段：弹出修改该行文字内容（仅本次打印，不回写数据库）</summary>
    private void El_MouseDoubleClick(Border brd, LabelField f)
    {
        if (!_rowByEl.TryGetValue(brd, out var row)) return;

        string prompt;
        string title;
        string? current;
        switch (f.Key)
        {
            case LabelTemplate.FieldCode:
            case LabelTemplate.FieldBarcode:
                prompt = "修改零件编码（仅本次打印，不回写数据库）：";
                title = "修改编码";
                current = row.PartNo;
                break;
            case LabelTemplate.FieldName:
                prompt = "修改配件名称（仅本次打印，不回写数据库）：";
                title = "修改名称";
                current = row.Name;
                break;
            default:
                prompt = "修改车型（仅本次打印，不回写数据库）：";
                title = "修改车型";
                current = row.CarType;
                break;
        }

        var input = InputBoxDialog.Show(prompt, title, current ?? "");
        if (input == null) return;

        switch (f.Key)
        {
            case LabelTemplate.FieldCode:
            case LabelTemplate.FieldBarcode:
                row.PartNo = input.Trim();
                break;
            case LabelTemplate.FieldName:
                row.Name = input.Trim();
                break;
            default:
                row.CarType = input.Trim();
                break;
        }
        dgItems.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true);
        dgItems.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);
        // 行属性已实现变更通知，列表自动刷新（避免在编辑事务中调用 Items.Refresh 抛异常）
        RenderEditor();
    }

    private void EditCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragField == null || _dragEl == null) return;
        if (cboTemplate.SelectedItem is not LabelTemplate tpl) return;
        double mm = LabelLayoutBuilder.MmToPx;
        var pos = e.GetPosition(editCanvas);
        if (!_origins.TryGetValue(_dragEl, out var origin)) return;

        double maxX = Math.Max(0, tpl.LabelWidthMm * mm - _dragEl.ActualWidth);
        double maxY = Math.Max(0, tpl.LabelHeightMm * mm - _dragEl.ActualHeight);
        double nx = Math.Clamp(pos.X - _dragStart.X - origin.X - 1, 0, maxX);
        double ny = Math.Clamp(pos.Y - _dragStart.Y - origin.Y - 1, 0, maxY);
        _dragField.XMm = nx / mm;
        _dragField.YMm = ny / mm;

        // 同步所有标签上同名元素（所见即所得：布局统一写模板 Fields）
        foreach (var child in editCanvas.Children)
        {
            if (child is not Border b || b.Tag is not LabelField tf) continue;
            if (tf.Key != _dragField.Key) continue;
            if (!_origins.TryGetValue(b, out var o)) continue;
            Canvas.SetLeft(b, o.X + 1 + tf.XMm * mm);
            Canvas.SetTop(b, o.Y + 1 + tf.YMm * mm);
        }
    }

    private void EditCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_dragEl != null) _dragEl.ReleaseMouseCapture();
        _dragField = null;
        _dragEl = null;
    }

    private void BtnFontUp_Click(object sender, RoutedEventArgs e) => AdjustFontSize(2);

    private void BtnFontDown_Click(object sender, RoutedEventArgs e) => AdjustFontSize(-2);

    private void AdjustFontSize(int step)
    {
        if (_selectedField == null) return;
        double min = _selectedField.Key == LabelTemplate.FieldBarcode ? 4 : 6;
        double max = _selectedField.Key == LabelTemplate.FieldBarcode ? 40 : 48;
        _selectedField.FontSize = Math.Clamp(_selectedField.FontSize + step, min, max);

        // 同步所有标签上同名元素尺寸（保留选中态）
        foreach (var child in editCanvas.Children)
        {
            if (child is not Border b || b.Tag is not LabelField tf) continue;
            if (tf.Key != _selectedField.Key) continue;
            if (b.Child is TextBlock tb)
                tb.FontSize = tf.FontSize;
            else if (b.Child is Image img)
                img.Height = Math.Max(4 * LabelLayoutBuilder.MmToPx, tf.FontSize * LabelLayoutBuilder.MmToPx);
        }
        UpdateSelectedLabel();
    }

    private void BtnToggleVisible_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedField == null) return;
        _selectedField.Visible = !_selectedField.Visible;
        RenderEditor();
    }

    private void UpdateSelectedLabel()
    {
        if (_selectedField == null)
        {
            txtSelected.Text = "未选中";
            return;
        }
        var name = _selectedField.Key switch
        {
            LabelTemplate.FieldCode => "编码",
            LabelTemplate.FieldBarcode => "条码",
            LabelTemplate.FieldName => "名称",
            LabelTemplate.FieldCarType => "车型",
            _ => _selectedField.Key
        };
        var unit = _selectedField.Key == LabelTemplate.FieldBarcode ? "mm" : "px";
        txtSelected.Text = $"{name} · {_selectedField.FontSize:0}{unit}";
    }

    private List<LabelPrintItem> SelectedRows(int limit = 0)
    {
        dgItems.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Cell, true);
        dgItems.CommitEdit(System.Windows.Controls.DataGridEditingUnit.Row, true);

        var list = new List<LabelPrintItem>();
        int copies = CurrentCopies();
        foreach (var item in _items.Where(i => i.IsSelected))
        {
            for (int c = 0; c < copies; c++)
            {
                list.Add(new LabelPrintItem { PartNo = item.PartNo, Name = item.Name, CarType = item.CarType });
                if (limit > 0 && list.Count >= limit) return list;
            }
        }
        return list;
    }

    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        var printerName = cboPrinter.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(printerName))
        {
            MessageBox.Show("请选择打印机", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (cboTemplate.SelectedItem is not LabelTemplate tpl)
        {
            MessageBox.Show("请选择标签模板", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        var rows = SelectedRows();  // 预览外：全部
        if (rows.Count == 0)
        {
            MessageBox.Show("请先勾选要打印的零件", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var (doc, pageCount) = LabelLayoutBuilder.Build(rows, tpl);
            if (pageCount == 0)
            {
                MessageBox.Show("没有可打印的标签", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            LabelPrintHelper.Print(doc, printerName, tpl);
            MessageBox.Show($"已发送 {rows.Count} 张标签到打印机：{printerName}", "提示");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打印失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}