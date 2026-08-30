using System.Windows;
using System.Windows.Media;

namespace QP11.Wpf.Services.LabelPrint;

/// <summary>标签字段样式辅助：颜色解析、旋转应用（供布局引擎与预览编辑器共用）</summary>
public static class LabelStyleHelper
{
    /// <summary>解析 ARGB 颜色字符串，失败回退黑色</summary>
    public static Brush ParseBrush(string? hex)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(hex) && new BrushConverter().ConvertFromString(hex) is Brush b)
                return b;
        }
        catch { }
        return Brushes.Black;
    }

    /// <summary>应用顺时针旋转（绕元素中心，0 度时清除）</summary>
    public static void ApplyRotation(UIElement el, double angle)
    {
        double a = angle % 360;
        if (a == 0)
        {
            el.RenderTransform = null;
            el.RenderTransformOrigin = default;
        }
        else
        {
            el.RenderTransformOrigin = new Point(0.5, 0.5);
            el.RenderTransform = new RotateTransform(a);
        }
    }
}