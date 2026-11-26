using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace QP11.Wpf.Controls;

/// <summary>
/// TextBox 整数输入限制：只允许 0-9 数字，禁止小数点、负号、空格；粘贴含非数字内容时整体拒绝。
/// 用法：
///   1. TextBox 直接设置 controls:IntegerInputHelper.IsEnabled="True"
///   2. DataGrid 数量列通过 EditingElementStyle 的 Setter 设置同一附加属性
/// </summary>
public static class IntegerInputHelper
{
    public static readonly DependencyProperty IsEnabledProperty =
        DependencyProperty.RegisterAttached("IsEnabled", typeof(bool), typeof(IntegerInputHelper),
            new PropertyMetadata(false, OnIsEnabledChanged));

    public static bool GetIsEnabled(DependencyObject obj) => (bool)obj.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(DependencyObject obj, bool value) => obj.SetValue(IsEnabledProperty, value);

    private static void OnIsEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBox tb) return;
        if ((bool)e.NewValue)
        {
            tb.PreviewTextInput += OnPreviewTextInput;
            DataObject.AddPastingHandler(tb, OnPasting);
        }
        else
        {
            tb.PreviewTextInput -= OnPreviewTextInput;
            DataObject.RemovePastingHandler(tb, OnPasting);
        }
    }

    private static void OnPreviewTextInput(object sender, TextCompositionEventArgs e)
        => e.Handled = !e.Text.All(char.IsDigit);

    private static void OnPasting(object sender, DataObjectPastingEventArgs e)
    {
        if (e.DataObject.GetDataPresent(DataFormats.Text))
        {
            var text = e.DataObject.GetData(DataFormats.Text) as string ?? "";
            if (!text.All(char.IsDigit))
                e.CancelCommand();
        }
        else
        {
            e.CancelCommand();
        }
    }
}
