using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace QP11.Wpf.Controls;

/// <summary>
/// 毫米标尺控件：水平（顶部）/ 垂直（左侧）两种。
/// 1mm 短刻度、5mm 中刻度、10mm 长刻度并标注数字，配合预览画布定位文字位置。
/// </summary>
public class RulerControl : FrameworkElement
{
    private const double MmToPx = 96.0 / 25.4;

    /// <summary>垂直（true）或水平（false）</summary>
    public bool IsVertical { get; set; }

    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        var brush = new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x6E));
        var pen = new Pen(brush, 1);
        double length = IsVertical ? Height : Width;

        for (double mm = 0; mm * MmToPx <= length; mm += 1)
        {
            double pos = mm * MmToPx;
            double tickH;
            if (mm % 10 == 0) tickH = 9;
            else if (mm % 5 == 0) tickH = 6;
            else tickH = 3;

            if (IsVertical)
            {
                dc.DrawLine(pen, new Point(0, pos), new Point(tickH, pos));
                if (mm % 10 == 0)
                    DrawText(dc, ((int)mm).ToString(), 2, pos - 4);
            }
            else
            {
                dc.DrawLine(pen, new Point(pos, 0), new Point(pos, tickH));
                if (mm % 10 == 0)
                    DrawText(dc, ((int)mm).ToString(), pos - 6, 1);
            }
        }
        pen.Freeze();
        brush.Freeze();
    }

    private void DrawText(DrawingContext dc, string text, double x, double y)
    {
        var ft = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface("Segoe UI"),
            8,
            new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x6E)),
            VisualTreeHelper.GetDpi(this).PixelsPerDip);
        dc.DrawText(ft, new Point(x, y));
    }
}