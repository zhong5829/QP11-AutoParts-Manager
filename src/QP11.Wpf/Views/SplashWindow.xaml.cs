using System.Windows;

namespace QP11.Wpf.Views;

/// <summary>启动画面：显示"正在连接数据库"，避免同步检测时界面无反馈假死</summary>
public partial class SplashWindow : Window
{
    public SplashWindow()
    {
        InitializeComponent();
    }

    public void UpdateStatus(string text)
    {
        txtStatus.Text = text;
    }
}