using System.Windows.Controls;
using System.Windows.Input;
using QP11.Wpf.Utilities;

namespace QP11.Wpf.Controls;

/// <summary>
/// 复制/剪切走 Win32 剪贴板（WinClipboard）的 TextBox。
/// WPF 默认复制走 OLE 剪贴板，剪贴板被其他程序占用时 Ctrl+C 会阻塞卡顿；
/// 实例级 CommandBinding 优先于 TextBoxBase 内置命令绑定，可完全接管复制/剪切。
/// </summary>
public class FastCopyTextBox : TextBox
{
    public FastCopyTextBox()
    {
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Copy, OnCopy));
        CommandBindings.Add(new CommandBinding(ApplicationCommands.Cut, OnCut));
    }

    private void OnCopy(object sender, ExecutedRoutedEventArgs e)
    {
        if (SelectionLength > 0)
            WinClipboard.SetText(SelectedText);
        e.Handled = true;
    }

    private void OnCut(object sender, ExecutedRoutedEventArgs e)
    {
        if (SelectionLength > 0 && !IsReadOnly)
        {
            WinClipboard.SetText(SelectedText);
            SelectedText = "";
        }
        e.Handled = true;
    }
}
