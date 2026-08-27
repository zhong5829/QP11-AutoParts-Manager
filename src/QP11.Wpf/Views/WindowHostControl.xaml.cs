using System;
using System.Windows;
using System.Windows.Controls;

namespace QP11.Wpf.Views;

public partial class WindowHostControl : UserControl, ITabContent
{
    private readonly Func<Window> _windowFactory;
    private Window? _hostedWindow;

    public string TabTitle { get; }
    public bool HasUnsavedChanges => false;
    public event EventHandler? RequestClose;

    public WindowHostControl(string title, Func<Window> windowFactory)
    {
        InitializeComponent();
        TabTitle = title;
        _windowFactory = windowFactory;
        Loaded += WindowHostControl_Loaded;
    }

    private void WindowHostControl_Loaded(object sender, RoutedEventArgs e)
    {
        if (_hostedWindow == null)
        {
            // 首次加载：创建窗口
            _hostedWindow = _windowFactory();
            _hostedWindow.WindowStyle = WindowStyle.None;
            _hostedWindow.ResizeMode = ResizeMode.NoResize;
            _hostedWindow.ShowInTaskbar = false;
            _hostedWindow.BorderThickness = new Thickness(0);
        }

        // 将窗口内容挂载到Grid（首次或切回时）
        // 如果内容已在Grid中（hostGrid有子元素），则跳过
        if (hostGrid.Children.Count > 0) return;

        if (_hostedWindow.Content is UIElement content)
        {
            _hostedWindow.Content = null;
            hostGrid.Children.Add(content);
        }
    }

    public void OnAdd() { }
    public void OnEdit() { }
    public void OnQuery() { }
    public void OnDelete() { }
    public void OnSave() { }
    public void OnSettle() { }
    public void OnPrint() { }
    public void OnReturn() { }
    public void OnCancel() { }
    public void OnHistory() { }
    public void OnClose() => RequestClose?.Invoke(this, EventArgs.Empty);
}
