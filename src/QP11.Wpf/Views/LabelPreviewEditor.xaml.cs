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
/// 标签预览编辑器（可复用）：毫米标尺 + 多标签连排 + 拖动/缩放/旋转/字号/颜色/加粗 +
/// 双击修改文字内容（仅内存，不回写数据库）。
/// 所有交互均基于画布视觉坐标，旋转后行为自动正确。
/// </summary>
public partial class LabelPreviewEditor : UserControl
{
    public event EventHandler? ItemsChanged;

    private const int EditorMaxLabels = 500;
    private const double ResizeZone = 9;       // 边框缩放热区阈值（px）

    private LabelTemplate? _tpl;
    private readonly List<LabelPrintItem> _items = new();
    private readonly Dictionary<Border, Point> _origins = new();      // 元素 → 所属标签原点
    private readonly Dictionary<Border, LabelPrintItem> _rowByEl = new();

    // 交互状态
    private LabelField? _selectedField;
    private Border? _dragEl;
    private LabelField? _dragField;
    private string? _resizeKind;      // 视觉方向 n/s/e/w/ne/nw/se/sw（null=移动）
    private Point _startMouse;         // 按下时鼠标在画布上的位置
    private double _startX, _startY, _startW, _startH;  // 按下时字段的 mm 值

    public IReadOnlyList<LabelPrintItem> Items => _items;

    public LabelPreviewEditor()
    {
        InitializeComponent();
    }

    public void SetTemplate(LabelTemplate tpl)
    {
        _tpl = tpl;
        if (tpl != null) chk180.IsChecked = tpl.Rotate180;
        Render();
    }

    public void SetItems(IEnumerable<LabelPrintItem> items)
    {
        _items.Clear();
        _items.AddRange(items.Where(i => i != null));
        Render();
    }

    public void Refresh() => Render();

    // ═══════════════ 渲染 ═══════════════

    private void Render()
    {
        _selectedField = null; _dragField = null; _dragEl = null; _resizeKind = null;
        _origins.Clear(); _rowByEl.Clear();
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

        var hRuler = new Controls.RulerControl { IsVertical = false, Width = canvasW0, Height = rulerSize, IsHitTestVisible = false };
        Canvas.SetLeft(hRuler, rulerSize); Canvas.SetTop(hRuler, 0);
        editCanvas.Children.Add(hRuler);

        var vRuler = new Controls.RulerControl { IsVertical = true, Width = rulerSize, Height = canvasH0, IsHitTestVisible = false };
        Canvas.SetLeft(vRuler, 0); Canvas.SetTop(vRuler, rulerSize);
        editCanvas.Children.Add(vRuler);

        int shown = 0;
        for (int r = 0; r < rows && shown < EditorMaxLabels; r++)
            for (int c = 0; c < cols && shown < EditorMaxLabels; c++)
            {
                int idx = r * cols + c;
                if (idx >= sampleList.Count) break;
                AddLabel(sampleList[idx], new Point(rulerSize + c * (lw + gap), rulerSize + r * (lh + gap)));
                shown++;
            }
        UpdateSelText();
    }

    private void AddLabel(LabelPrintItem item, Point origin)
    {
        double mm = LabelLayoutBuilder.MmToPx;
        var labelBorder = new Border
        {
            Width = _tpl!.LabelWidthMm * mm, Height = _tpl.LabelHeightMm * mm,
            BorderBrush = Brushes.Black, BorderThickness = new Thickness(1), Background = Brushes.White
        };
        Canvas.SetLeft(labelBorder, origin.X + 1); Canvas.SetTop(labelBorder, origin.Y + 1);
        editCanvas.Children.Add(labelBorder);

        foreach (var f in _tpl.Fields)
        {
            if (f == null || !f.Visible) continue;
            var brd = CreateField(item, f);
            if (brd == null) continue;
            brd.Tag = f;
            brd.MouseLeftButtonDown += El_MouseDown;
            brd.MouseMove += El_MouseMove;
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
        var width = f.WidthMm > 0 ? f.WidthMm * mm : double.NaN;
        var height = f.HeightMm > 0 ? f.HeightMm * mm : double.NaN;
        double maxW = Math.Max(24, (_tpl!.LabelWidthMm - f.XMm) * mm - 6);
        var brush = LabelStyleHelper.ParseBrush(f.Color);
        UIElement content;
        switch (f.Key)
        {
            case LabelTemplate.FieldCode:
                content = new TextBlock { Text = item.PartNo ?? "", FontSize = f.FontSize, FontWeight = FontWeights.Bold, Foreground = brush,
                    Width = width, Height = height, MaxWidth = double.IsNaN(width) ? maxW : double.PositiveInfinity,
                    TextTrimming = double.IsNaN(width) ? TextTrimming.CharacterEllipsis : TextTrimming.None, TextWrapping = TextWrapping.NoWrap };
                break;
            case LabelTemplate.FieldBarcode:
                var bw = f.WidthMm > 0 ? f.WidthMm * mm : maxW;
                var bh = f.HeightMm > 0 ? f.HeightMm * mm : Math.Max(4 * mm, f.FontSize * mm);
                content = new Image { Source = Code128Renderer.Render(item.PartNo ?? "", bw, bh, 0.8), Width = bw, Height = bh, Stretch = Stretch.Fill };
                break;
            case LabelTemplate.FieldName:
                content = new TextBlock { Text = item.Name ?? "", FontSize = f.FontSize, FontWeight = f.Bold ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = brush, Width = width, Height = height, MaxWidth = double.IsNaN(width) ? maxW : double.PositiveInfinity,
                    TextTrimming = double.IsNaN(width) ? TextTrimming.CharacterEllipsis : TextTrimming.None, TextWrapping = TextWrapping.NoWrap };
                break;
            case LabelTemplate.FieldCarType:
                content = new TextBlock { Text = item.CarType ?? "", FontSize = f.FontSize, FontWeight = f.Bold ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = brush, Width = width, Height = height, MaxWidth = double.IsNaN(width) ? maxW : double.PositiveInfinity,
                    TextTrimming = double.IsNaN(width) ? TextTrimming.CharacterEllipsis : TextTrimming.None, TextWrapping = TextWrapping.NoWrap };
                break;
            default:
                if (f.Key.StartsWith(LabelTemplate.FieldTextPrefix, StringComparison.Ordinal))
                    content = new TextBlock { Text = f.CustomText ?? "", FontSize = f.FontSize, FontWeight = f.Bold ? FontWeights.Bold : FontWeights.Normal,
                        Foreground = brush, Width = width, Height = height, MaxWidth = double.IsNaN(width) ? maxW : double.PositiveInfinity,
                        TextWrapping = TextWrapping.Wrap, TextTrimming = TextTrimming.None };
                else return null;
                break;
        }
        var brd = new Border { Child = content, Background = Brushes.Transparent, Cursor = Cursors.SizeAll };
        LabelStyleHelper.ApplyRotation(brd, f.Rotation);
        return brd;
    }

    // ═══════════════ 交互（全部基于画布视觉坐标） ═══════════════

    /// <summary>取元素在画布上的视觉包围盒（含旋转）</summary>
    private Rect VisualBox(Border brd)
    {
        var toCanvas = brd.TransformToAncestor(editCanvas);
        var pts = new[]
        {
            toCanvas.Transform(new Point(0, 0)),
            toCanvas.Transform(new Point(brd.ActualWidth, 0)),
            toCanvas.Transform(new Point(0, brd.ActualHeight)),
            toCanvas.Transform(new Point(brd.ActualWidth, brd.ActualHeight))
        };
        double minX = pts.Min(p => p.X), maxX = pts.Max(p => p.X);
        double minY = pts.Min(p => p.Y), maxY = pts.Max(p => p.Y);
        return new Rect(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>按画布坐标 + 视觉包围盒检测缩放方向（视觉方向）</summary>
    private string? DetectZone(Point canvasPos, Border brd)
    {
        var box = VisualBox(brd);
        double visX = canvasPos.X - box.X, visY = canvasPos.Y - box.Y;
        bool left = visX <= ResizeZone, right = visX >= box.Width - ResizeZone;
        bool top = visY <= ResizeZone, bottom = visY >= box.Height - ResizeZone;
        if (top && left) return "nw";
        if (top && right) return "ne";
        if (bottom && left) return "sw";
        if (bottom && right) return "se";
        if (top) return "n";
        if (bottom) return "s";
        if (left) return "w";
        if (right) return "e";
        return null;
    }

    private void El_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border brd || brd.Tag is not LabelField f) return;
        if (e.ClickCount >= 2)
        {
            El_MouseDoubleClick(brd, f);
            e.Handled = true; return;
        }

        var canvasPos = e.GetPosition(editCanvas);
        _resizeKind = DetectZone(canvasPos, brd);
        _selectedField = f; _dragField = f; _dragEl = brd;
        _startMouse = canvasPos;
        _startX = f.XMm; _startY = f.YMm;
        _startW = f.WidthMm; _startH = f.HeightMm;

        foreach (var child in editCanvas.Children)
            if (child is Border b && b.Tag is LabelField && b != brd)
                b.BorderThickness = new Thickness(0);
        brd.BorderBrush = Brushes.DodgerBlue;
        brd.BorderThickness = new Thickness(1);
        brd.CaptureMouse();
        UpdateSelText();
        e.Handled = true;
    }

    /// <summary>悬停时按视觉方向显示光标</summary>
    private void El_MouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not Border brd) return;
        if (_dragEl == brd) return;  // 拖动中不更新光标
        var kind = DetectZone(e.GetPosition(editCanvas), brd);
        brd.Cursor = kind switch
        {
            "n" or "s" => Cursors.SizeNS,
            "e" or "w" => Cursors.SizeWE,
            "nw" or "se" => Cursors.SizeNWSE,
            "ne" or "sw" => Cursors.SizeNESW,
            _ => Cursors.SizeAll
        };
    }

    private void EditCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_dragField == null || _dragEl == null || _tpl == null) return;
        var pos = e.GetPosition(editCanvas);
        double mm = LabelLayoutBuilder.MmToPx;
        double labW = _tpl.LabelWidthMm * mm, labH = _tpl.LabelHeightMm * mm;

        if (_resizeKind != null)
        {
            // ── 缩放：全部用画布坐标差值，按视觉方向改视觉宽/高 ──
            double dx = (pos.X - _startMouse.X) / mm;   // 视觉水平 delta (mm)
            double dy = (pos.Y - _startMouse.Y) / mm;   // 视觉垂直 delta (mm)

            // 旋转 90°/270° 时视觉宽=本地高、视觉高=本地宽
            bool swapped = _dragField.Rotation % 180 != 0;
            // 视觉宽/高对应的本地字段
            double localW = _startW > 0 ? _startW : (_dragEl.ActualWidth / mm);
            double localH = _startH > 0 ? _startH : (_dragEl.ActualHeight / mm);

            double newVisW, newVisH;   // 新视觉宽/高 (mm)
            bool leftEdge = _resizeKind.Contains('w'), topEdge = _resizeKind.Contains('n');

            // 按视觉方向计算新视觉宽高
            if (_resizeKind.Contains('e')) newVisW = localW + dx;       // 拖右边 → 视觉宽增大
            else if (_resizeKind.Contains('w')) newVisW = localW - dx;   // 拖左边 → 视觉宽减小
            else newVisW = swapped ? localH : localW;                    // 不改宽

            if (_resizeKind.Contains('s')) newVisH = localH + dy;       // 拖下边
            else if (_resizeKind.Contains('n')) newVisH = localH - dy; // 拖上边
            else newVisH = swapped ? localW : localH;

            const double minMm = 2;
            newVisW = Math.Max(minMm, newVisW);
            newVisH = Math.Max(minMm, newVisH);

            // 写回本地字段：视觉宽→本地宽(0°)或本地高(90°)
            double newLocalW, newLocalH;
            if (swapped)
            {
                newLocalW = newVisH;   // 视觉高 → 本地宽
                newLocalH = newVisW;   // 视觉宽 → 本地高
            }
            else
            {
                newLocalW = newVisW;
                newLocalH = newVisH;
            }
            _dragField.WidthMm = newLocalW;
            _dragField.HeightMm = newLocalH;

            // 位置补偿：拖左/上边时，视觉框左/上移动，需调整锚点
            double newX = _startX, newY = _startY;
            if (leftEdge || topEdge)
            {
                double visShiftX = leftEdge ? (localW - newVisW) : 0;
                double visShiftY = topEdge ? (localH - newVisH) : 0;
                if (swapped) (visShiftX, visShiftY) = (visShiftY, visShiftX);
                newX = _startX + visShiftX;
                newY = _startY + visShiftY;
            }

            // 限制元素+位置不超出标签方框：X+Width ≤ 标签宽，Y+Height ≤ 标签高
            newX = Math.Clamp(newX, 0, Math.Max(0, _tpl.LabelWidthMm - newLocalW));
            newY = Math.Clamp(newY, 0, Math.Max(0, _tpl.LabelHeightMm - newLocalH));
            // 如果缩放后超出，再缩小尺寸使其装得下
            if (newX + newLocalW > _tpl.LabelWidthMm) newLocalW = Math.Max(minMm, _tpl.LabelWidthMm - newX);
            if (newY + newLocalH > _tpl.LabelHeightMm) newLocalH = Math.Max(minMm, _tpl.LabelHeightMm - newY);
            _dragField.XMm = newX;
            _dragField.YMm = newY;
            _dragField.WidthMm = newLocalW;
            _dragField.HeightMm = newLocalH;

            ApplyFieldSizeSync();
            return;
        }

        // ── 移动：限制元素整体（X+宽 / Y+高）不超出标签方框 ──
        if (!_origins.TryGetValue(_dragEl, out var origin)) return;
        double elW = _dragField.WidthMm > 0 ? _dragField.WidthMm : (_dragEl.ActualWidth / mm);
        double elH = _dragField.HeightMm > 0 ? _dragField.HeightMm : (_dragEl.ActualHeight / mm);
        double mvX = (pos.X - _startMouse.X) / mm;
        double mvY = (pos.Y - _startMouse.Y) / mm;
        double moveX = Math.Clamp(_startX + mvX, 0, Math.Max(0, _tpl.LabelWidthMm - elW));
        double moveY = Math.Clamp(_startY + mvY, 0, Math.Max(0, _tpl.LabelHeightMm - elH));
        _dragField.XMm = moveX;
        _dragField.YMm = moveY;
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
        _dragField = null; _dragEl = null; _resizeKind = null;
        UpdateSelText();
    }

    /// <summary>按字段新尺寸同步画布上所有同名元素（含重新渲染条码）</summary>
    private void ApplyFieldSizeSync()
    {
        if (_dragField == null) return;
        double mm = LabelLayoutBuilder.MmToPx;
        foreach (var child in editCanvas.Children)
        {
            if (child is not Border b || b.Tag is not LabelField tf) continue;
            if (tf.Key != _dragField.Key) continue;
            if (_origins.TryGetValue(b, out var o))
            {
                Canvas.SetLeft(b, o.X + 1 + tf.XMm * mm);
                Canvas.SetTop(b, o.Y + 1 + tf.YMm * mm);
            }
            if (b.Child is TextBlock tb)
            {
                if (tf.WidthMm > 0) { tb.Width = tf.WidthMm * mm; tb.MaxWidth = double.PositiveInfinity; tb.TextWrapping = TextWrapping.Wrap; tb.TextTrimming = TextTrimming.None; }
                else { tb.Width = double.NaN; }
                tb.Height = tf.HeightMm > 0 ? tf.HeightMm * mm : double.NaN;
            }
            else if (b.Child is Image img)
            {
                double bw = tf.WidthMm > 0 ? tf.WidthMm * mm : img.Width;
                double bh = tf.HeightMm > 0 ? tf.HeightMm * mm : img.Height;
                img.Width = bw; img.Height = bh;
                if (_rowByEl.TryGetValue(b, out var row))
                    img.Source = Code128Renderer.Render(row.PartNo ?? "", bw, bh, 0.8);
            }
        }
    }

    // ═══════════════ 双击修改文字 ═══════════════

    private void El_MouseDoubleClick(Border brd, LabelField f)
    {
        if (!_rowByEl.TryGetValue(brd, out var row)) return;
        string prompt, title; string? current;
        switch (f.Key)
        {
            case LabelTemplate.FieldCode:
            case LabelTemplate.FieldBarcode:
                prompt = "修改零件编码（仅本次打印，不回写数据库）："; title = "修改编码"; current = row.PartNo; break;
            case LabelTemplate.FieldName:
                prompt = "修改配件名称（仅本次打印，不回写数据库）："; title = "修改名称"; current = row.Name; break;
            default:
                if (f.Key.StartsWith(LabelTemplate.FieldTextPrefix, StringComparison.Ordinal))
                { prompt = "修改自定义文字（仅本次打印）："; title = "修改文字"; current = f.CustomText; }
                else
                { prompt = "修改车型（仅本次打印，不回写数据库）："; title = "修改车型"; current = row.CarType; }
                break;
        }
        var input = InputBoxDialog.Show(prompt, title, current ?? "");
        if (input == null) return;
        switch (f.Key)
        {
            case LabelTemplate.FieldCode:
            case LabelTemplate.FieldBarcode: row.PartNo = input.Trim(); break;
            case LabelTemplate.FieldName: row.Name = input.Trim(); break;
            default:
                if (f.Key.StartsWith(LabelTemplate.FieldTextPrefix, StringComparison.Ordinal)) f.CustomText = input.Trim();
                else row.CarType = input.Trim();
                break;
        }
        Render();
        ItemsChanged?.Invoke(this, EventArgs.Empty);
    }

    // ═══════════════ 工具栏按钮 ═══════════════

    private void BtnFontUp_Click(object sender, RoutedEventArgs e) => AdjustFontSize(2);
    private void BtnFontDown_Click(object sender, RoutedEventArgs e) => AdjustFontSize(-2);

    private void AdjustFontSize(int step)
    {
        if (_selectedField == null) return;
        double min = _selectedField.Key == LabelTemplate.FieldBarcode ? 4 : 6;
        double max = _selectedField.Key == LabelTemplate.FieldBarcode ? 40 : 48;
        _selectedField.FontSize = Math.Clamp(_selectedField.FontSize + step, min, max);
        ApplyToMatching(b =>
        {
            if (b.Child is TextBlock tb) tb.FontSize = _selectedField.FontSize;
            else if (b.Child is Image img) img.Height = Math.Max(4 * LabelLayoutBuilder.MmToPx, _selectedField.FontSize * LabelLayoutBuilder.MmToPx);
        });
        UpdateSelText();
    }

    private void BtnRotate_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedField == null) return;
        _selectedField.Rotation = (_selectedField.Rotation + 90) % 360;
        ApplyToMatching(b => LabelStyleHelper.ApplyRotation(b, _selectedField.Rotation));
        UpdateSelText();
    }

    private void BtnBold_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedField == null) return;
        _selectedField.Bold = !_selectedField.Bold;
        ApplyToMatching(b => { if (b.Child is TextBlock tb) tb.FontWeight = _selectedField.Bold ? FontWeights.Bold : FontWeights.Normal; });
        UpdateSelText();
    }

    private static readonly string[] _colorCycle = { "#000000", "#E8463A", "#1D4ED8" };

    private void BtnColor_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedField == null) return;
        int idx = Array.IndexOf(_colorCycle, _selectedField.Color);
        if (idx < 0) idx = 0;
        _selectedField.Color = _colorCycle[(idx + 1) % _colorCycle.Length];
        ApplyToMatching(b => { if (b.Child is TextBlock tb) tb.Foreground = LabelStyleHelper.ParseBrush(_selectedField.Color); });
        UpdateSelText();
    }

    private void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedField == null) return;
        var key = _selectedField.Key;
        if (key == LabelTemplate.FieldCode || key == LabelTemplate.FieldBarcode ||
            key == LabelTemplate.FieldName || key == LabelTemplate.FieldCarType)
        {
            _selectedField.Visible = false;
            _selectedField = null;
            Render();
            MessageBox.Show("标准字段不能删除，已隐藏。", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        _tpl?.Fields.Remove(_selectedField);
        _selectedField = null;
        Render();
    }

    private void BtnAddText_Click(object sender, RoutedEventArgs e)
    {
        if (_tpl == null) return;
        var text = InputBoxDialog.Show("输入自定义文字（仅本次模板）：", "添加文字");
        if (string.IsNullOrWhiteSpace(text)) return;
        int n = 0;
        while (_tpl.Fields.Any(f => f.Key == LabelTemplate.FieldTextPrefix + n)) n++;
        _tpl.Fields.Add(new LabelField
        {
            Key = LabelTemplate.FieldTextPrefix + n,
            XMm = Math.Max(2, _tpl.LabelWidthMm / 2 - 10),
            YMm = Math.Max(2, _tpl.LabelHeightMm / 2 - 3),
            FontSize = 12, Visible = true, CustomText = text.Trim(), Bold = false, Color = "#000000"
        });
        Render();
    }

    private void Chk180_Changed(object sender, RoutedEventArgs e)
    {
        if (_tpl == null) return;
        _tpl.Rotate180 = chk180.IsChecked == true;
    }

    /// <summary>对画布上所有与选中字段同 Key 的元素执行操作</summary>
    private void ApplyToMatching(Action<Border> action)
    {
        if (_selectedField == null) return;
        foreach (var child in editCanvas.Children)
        {
            if (child is not Border b || b.Tag is not LabelField tf) continue;
            if (tf.Key != _selectedField.Key) continue;
            action(b);
        }
    }

    private void UpdateSelText()
    {
        if (_selectedField == null) { txtSel.Text = "未选中"; return; }
        var name = _selectedField.Key switch
        {
            LabelTemplate.FieldCode => "编码", LabelTemplate.FieldBarcode => "条码",
            LabelTemplate.FieldName => "名称", LabelTemplate.FieldCarType => "车型",
            _ => _selectedField.Key
        };
        var unit = _selectedField.Key == LabelTemplate.FieldBarcode ? "mm" : "px";
        var size = _selectedField.WidthMm > 0 || _selectedField.HeightMm > 0
            ? $" · {_selectedField.WidthMm:0.#}×{_selectedField.HeightMm:0.#}mm" : "";
        txtSel.Text = $"{name} · {_selectedField.FontSize:0}{unit}{size}";
    }
}