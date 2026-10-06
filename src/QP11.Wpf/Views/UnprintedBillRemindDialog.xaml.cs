using System.Collections.Generic;
using System.Media;
using System.Windows;
using QP11.Services;

namespace QP11.Wpf.Views;

/// <summary>
/// 未打印单据清单窗口。
/// isReminder = true 时作为到点提醒弹窗（非模态、置顶、带提示音）；
/// isReminder = false 时作为状态栏计数的查看窗口（不响铃、不置顶）。
/// </summary>
public partial class UnprintedBillRemindDialog : Window
{
    public UnprintedBillRemindDialog(List<UnprintedBill> bills, bool isReminder = true)
    {
        InitializeComponent();

        dgBills.ItemsSource = bills;

        if (isReminder)
        {
            txtSummary.Text = $"全店今天共有 {bills.Count} 张单据尚未打印，请确认是否漏打：";

            // 播放系统提示音
            try { SystemSounds.Exclamation.Play(); } catch { /* 声音失败不影响提醒 */ }
        }
        else
        {
            Title = "未打印单据";
            txtHeader.Text = "未打印单据";
            txtSummary.Text = $"全店今天共有 {bills.Count} 张单据尚未打印：";
            btnOk.Content = "关闭";
            Topmost = false;
        }
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}