using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QP11.Wpf.Services.LabelPrint;

namespace QP11.Wpf.Views;

/// <summary>
/// 标签预览编辑器（可复用）：毫米标尺 + 多标签连排 + 拖动调整位置 +
/// 字号/显隐 + 双击修改文字内容（仅内存，不回写数据库）。
/// </summary>
public partial class LabelPreviewEditor : UserControl
{
    /// <summary>双击修改文字内容后触发（供外部同步显示与打印数据）</summary>
    public event EventHandler? ItemsChanged;

    private const int EditorMaxLabels = 500;
    private LabelTemplate? _tpl;
    private readonly List<LabelPrintItem> _items = new();
    private readonly Dictionary<Border, Point> _origins = new();
    private readonly Dictionary<Border, LabelPrintItem> _rowByEl = new();
    private LabelField? _selectedField;
    private Border? _dragEl;
    private LabelField? _dragField;
    private Point _dragStart;

    /// <summary>当前可编辑数据行（双击修改后的值会更新到此集合）</summary>
    public IReadOnlyList<LabelPrintItem> Items => _items;

    public LabelPreviewEditor()
    {
        InitializeComponent();
    }

    /// <summary>设置模板（引用同一对象，布局修改即时生效）</summary>
    public void SetTemplate(LabelTemplate tpl)
    {
        _tpl = tpl;
        Render();
    }

    /// <summary>设置打印数据行</summary>
    public void SetItems(IEnumerable<LabelPrintItem> items)
    {
        _items.Clear();
        _items.AddRange(items.Where(i => i != null));
        Render();
    }

    public void Refresh() => Render();

    // ── 渲染 ──

    private void Render()
    {
        _selectedField = null;
        _dragField = null;
        _dragEl = null;
        _origins.Clear();
        _rowByEl.Clear();
        editCanvas.Children.Clear();
        if (_tpl == null) return;
        _tpl.EnsureFields();

        var sampleList = _items.Count > 0 ? _items : new List<LabelPrintItem> { new() };
        double mm = LabelLayoutBuilder.MmToPx;
        int cols = Math.Max(1, _tpl.ColsPerRow);
        double lw = (_tpl.LabelWidthMm + 2) * mm;
        double lh = (_tpl.LabelHeightMm + 2) * mm;
        double gap = Math.Max(8, _tpl.GapMm * mm);
        int rows = (int)Math.Ceiling(sampleList.Count / (double)cols);
        double canvasW0 = cols * lw + (cols - 1) * gap;
        double canvasH0 = Math.Max(rows, 1) * lh + Math.Max(rows - 1, 0) * gap;

        const double rulerSize = 22;
        editCanvas.Width = canvasW0 + rulerSize;
        editCanvas.Height = canvasH0 + rulerSize;

        var hRuler = new QP11.Wpf.Controls.RulerControl { IsVertical = false, Width = canvasW0, Height = rulerSize, IsHitTestVisible = false };
        Canvas.SetLeft(hRuler, rulerSize);
        Canvas.SetTop(hRuler, 0);
        editCanvas.Children.Add(hRuler);

        var vRuler = new QP11.Wpf.Controls.RulerControl { IsVertical = true, Width = rulerSize, Height = canvasH0, IsHitTestVisible = false };
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
                AddLabel(sampleList[idx], new Point(rulerSize + c * (lw + gap), rulerSize + r * (lh + gap)));
                shown++;
            }
        }
        UpdateSelText();
    }

    private void AddLabel(LabelPrintItem item, Point origin)
    {
        var mm = LabelLayoutBuilder.MmToPx;
        var labelBorder = new Border
        {
            Width = _tpl!.LabelWidthMm * mm,
            Height = _tpl.LabelHeightMm * mm,
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(1),
            Background = Brushes.White
        };
        Canvas.SetLeft(labelBorder, origin.X + 1);
        Canvas.SetTop(labelBorder, origin.Y + 1);
        editCanvas.Children.Add(labelBorder);

        foreach (var f in _tpl.Fields)
        {
            if (f == null || !f.Visible) continue;
            var brd = CreateField(item, f);
            if (brd == null) continue;
            brd.Tag = f;
            brd.MouseLeftButtonDown += El_MouseDown;
            Canvas.SetLeft(brd, origin.X + 1 + f.XMm * mm);
            Canvas.SetTop(brd, origin.Y + 1 + f.YMm * mm);
            editCanvas.Children.Add(brd);
            _origins[brd] = origin;
            _rowByEl[brd] = item;
        }
    }

    private Border? CreateField(LabelPrintItem item, LabelField f)
    {
        double mm = LabelLayoutBuilder.MmToPx;
        double maxW = Math.Max(24, (_tpl!.LabelWidthMm - f.XMm) * mm - 6);
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

    // ── 交互 ──

    private void El_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border brd || brd.Tag is not LabelField f) return;
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

        foreach (var child in editCanvas.Children)
        {
            if (child is Border b && b.Tag is LabelField && b != brd)
                b.BorderThickness = new Thickness(0);
        }
        brd.BorderBrush = Brushes.DodgerBlue;
        brd.BorderThickness = new Thickness(1);
        brd.CaptureMouse();
        UpdateSelText();
        e.Handled = true;
    }

    private void EditCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragField == null || _dragEl == null || _tpl == null) return;
        double mm = LabelLayoutBuilder.MmToPx;
        var pos = e.GetPosition(editCanvas);
        if (!_origins.TryGetValue(_dragEl, out var origin)) return;

        double maxX = Math.Max(0, _tpl.LabelWidthMm * mm - _dragEl.ActualWidth);
        double maxY = Math.Max(0, _tpl.LabelHeightMm * mm - _dragEl.ActualHeight);
        double nx = Math.Clamp(pos.X - _dragStart.X - origin.X - 1, 0, maxX);
        double ny = Math.Clamp(pos.Y - _dragStart.Y - origin.Y - 1, 0, maxY);
        _dragField.XMm = nx / mm;
        _dragField.YMm = ny / mm;

        foreach (var child in editCanvas.Children)
        {
            if (child is not Border b || b.Tag is not LabelField tf) continue;
            if (tf.Key != _dragField.Key || !_origins.TryGetValue(b, out var o)) continue;
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

    private void El_MouseDoubleClick(Border brd, LabelField f)
    {
        if (!_rowByEl.TryGetValue(brd, out var row)) return;
        string prompt, title;
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
        Render();
        ItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void BtnFontUp_Click(object sender, RoutedEventArgs e) => AdjustFontSize(2);

    private void BtnFontDown_Click(object sender, RoutedEventArgs e) => AdjustFontSize(-2);

    private void AdjustFontSize(int step)
    {
        if (_selectedField == null) return;
        double min = _selectedField.Key == LabelTemplate.FieldBarcode ? 4 : 6;
        double max = _selectedField.Key == LabelTemplate.FieldBarcode ? 40 : 48;
        _selectedField.FontSize = Math.Clamp(_selectedField.FontSize + step, min, max);

        foreach (var child in editCanvas.Children)
        {
            if (child is not Border b || b.Tag is not LabelField tf) continue;
            if (tf.Key != _selectedField.Key) continue;
            if (b.Child is TextBlock tb)
                tb.FontSize = tf.FontSize;
            else if (b.Child is Image img)
                img.Height = Math.Max(4 * LabelLayoutBuilder.MmToPx, tf.FontSize * LabelLayoutBuilder.MmToPx);
        }
        UpdateSelText();
    }

    private void BtnToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedField == null) return;
        _selectedField.Visible = !_selectedField.Visible;
        Render();
    }

    private void UpdateSelText()
    {
        if (_selectedField == null)
        {
            txtSel.Text = "未选中";
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
        txtSel.Text = $"{name} · {_selectedField.FontSize:0}{unit}";
    }
}