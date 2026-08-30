using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

namespace QP11.Wpf.Services.LabelPrint;

/// <summary>标签打印单行数据（编码/名称/车型，车型可编辑且仅本次打印生效）</summary>
public class LabelPrintItem
{
    public string? PartNo { get; set; }
    public string? Name { get; set; }
    public string? CarType { get; set; }
}

/// <summary>
/// 标签布局引擎：按模板毫米尺寸生成 FixedDocument（96dpi，1mm = 96/25.4px）。
/// 每一“页”即一排标签，连续纸自动向下出纸。
/// </summary>
public static class LabelLayoutBuilder
{
    public const double MmToPx = 96.0 / 25.4;

    public static (FixedDocument Document, int PageCount) Build(IEnumerable<LabelPrintItem> items, LabelTemplate tpl)
    {
        tpl.EnsureFields();
        var doc = new FixedDocument();
        var list = items.Where(i => i != null).ToList();
        if (list.Count == 0 || tpl.ColsPerRow <= 0)
            return (doc, 0);

        var labelW = tpl.LabelWidthMm * MmToPx;
        var labelH = tpl.LabelHeightMm * MmToPx;
        var pageW = (tpl.MarginLeftMm + tpl.MarginRightMm + tpl.ColsPerRow * tpl.LabelWidthMm + (tpl.ColsPerRow - 1) * tpl.GapMm) * MmToPx;
        var pageH = (tpl.MarginTopMm + tpl.MarginBottomMm + tpl.LabelHeightMm) * MmToPx;
        doc.DocumentPaginator.PageSize = new Size(pageW, pageH);

        int rowCount = (int)Math.Ceiling(list.Count / (double)tpl.ColsPerRow);
        for (int r = 0; r < rowCount; r++)
        {
            var page = new FixedPage { Width = pageW, Height = pageH };
            // 模板级整体旋转 180°（应对热敏机纸卷反向装入）
            if (tpl.Rotate180)
                page.RenderTransform = new RotateTransform(180, pageW / 2, pageH / 2);
            for (int c = 0; c < tpl.ColsPerRow; c++)
            {
                int idx = r * tpl.ColsPerRow + c;
                if (idx >= list.Count) break;

                double x = (tpl.MarginLeftMm + c * (tpl.LabelWidthMm + tpl.GapMm)) * MmToPx;
                double y = tpl.MarginTopMm * MmToPx;
                page.Children.Add(CreateLabel(list[idx], tpl, x, y, labelW, labelH));
            }

            var pageContent = new PageContent { Width = pageW, Height = pageH };
            ((IAddChild)pageContent).AddChild(page);
            doc.Pages.Add(pageContent);
        }
        return (doc, rowCount);
    }

    private static UIElement CreateLabel(LabelPrintItem item, LabelTemplate tpl, double x, double y, double w, double h)
    {
        var canvas = new Canvas { Width = w, Height = h };
        foreach (var f in tpl.Fields)
        {
            if (f == null || !f.Visible) continue;
            var el = BuildFieldElement(item, tpl, f);
            if (el == null) continue;
            LabelStyleHelper.ApplyRotation(el, f.Rotation);
            Canvas.SetLeft(el, f.XMm * MmToPx);
            Canvas.SetTop(el, f.YMm * MmToPx);
            canvas.Children.Add(el);
        }

        var border = new Border
        {
            Width = w,
            Height = h,
            // 标签外框仅为预览参考，打印时去掉边框（标签纸本身自带边界）
            BorderThickness = new Thickness(0),
            Background = Brushes.White,
            Child = canvas
        };
        FixedPage.SetLeft(border, x);
        FixedPage.SetTop(border, y);
        return border;
    }

    /// <summary>按字段生成标签内元素（坐标由调用方按 Fields.XMm/YMm 定位）</summary>
    private static UIElement? BuildFieldElement(LabelPrintItem item, LabelTemplate tpl, LabelField f)
    {
        var mm = MmToPx;
        // 显式宽高（0=自动）；文本固定宽度时自动折行
        var width = f.WidthMm > 0 ? f.WidthMm * mm : double.NaN;
        var height = f.HeightMm > 0 ? f.HeightMm * mm : double.NaN;
        double maxW = Math.Max(20, (tpl.LabelWidthMm - f.XMm) * mm - 2);   // 距右缘 2px
        var brush = LabelStyleHelper.ParseBrush(f.Color);
        switch (f.Key)
        {
            case LabelTemplate.FieldCode:
                return new TextBlock
                {
                    Text = item.PartNo ?? "",
                    FontSize = f.FontSize,
                    FontWeight = FontWeights.Bold,
                    Foreground = brush,
                    Width = width,
                    Height = height,
                    MaxWidth = double.IsNaN(width) ? maxW : double.PositiveInfinity,
                    TextWrapping = double.IsNaN(width) ? TextWrapping.NoWrap : TextWrapping.Wrap,
                    TextTrimming = double.IsNaN(width) ? TextTrimming.CharacterEllipsis : TextTrimming.None
                };
            case LabelTemplate.FieldBarcode:
                var barcodeW = f.WidthMm > 0 ? f.WidthMm * mm : maxW;
                var barcodeH = f.HeightMm > 0 ? f.HeightMm * mm : Math.Max(4 * mm, f.FontSize * mm);
                return new Image
                {
                    Source = Code128Renderer.Render(item.PartNo ?? "", barcodeW, barcodeH, 0.8),
                    Width = barcodeW,
                    Height = barcodeH,
                    Stretch = Stretch.Fill
                };
            case LabelTemplate.FieldName:
                return new TextBlock
                {
                    Text = item.Name ?? "",
                    FontSize = f.FontSize,
                    FontWeight = f.Bold ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = brush,
                    Width = width,
                    Height = height,
                    MaxWidth = double.IsNaN(width) ? maxW : double.PositiveInfinity,
                    TextWrapping = double.IsNaN(width) ? TextWrapping.NoWrap : TextWrapping.Wrap,
                    TextTrimming = double.IsNaN(width) ? TextTrimming.CharacterEllipsis : TextTrimming.None
                };
            case LabelTemplate.FieldCarType:
                return new TextBlock
                {
                    Text = item.CarType ?? "",
                    FontSize = f.FontSize,
                    FontWeight = f.Bold ? FontWeights.Bold : FontWeights.Normal,
                    Foreground = brush,
                    Width = width,
                    Height = height,
                    MaxWidth = double.IsNaN(width) ? maxW : double.PositiveInfinity,
                    TextWrapping = double.IsNaN(width) ? TextWrapping.NoWrap : TextWrapping.Wrap,
                    TextTrimming = double.IsNaN(width) ? TextTrimming.CharacterEllipsis : TextTrimming.None
                };
            default:
                // 自定义文本字段（FieldTextPrefix 开头）：支持换行/加粗/颜色
                if (f.Key.StartsWith(LabelTemplate.FieldTextPrefix, StringComparison.Ordinal))
                {
                    return new TextBlock
                    {
                        Text = f.CustomText ?? "",
                        FontSize = f.FontSize,
                        FontWeight = f.Bold ? FontWeights.Bold : FontWeights.Normal,
                        Foreground = brush,
                        Width = width,
                        Height = height,
                        MaxWidth = double.IsNaN(width) ? maxW : double.PositiveInfinity,
                        TextWrapping = double.IsNaN(width) ? TextWrapping.Wrap : TextWrapping.Wrap,
                        TextTrimming = TextTrimming.None
                    };
                }
                return null;
        }
    }
}