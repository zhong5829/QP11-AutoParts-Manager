using System.Collections.Generic;
using System.Linq;
using System.Media;
using System.Windows;
using QP11.Core.Entities;

namespace QP11.Wpf.Views;

public partial class MemoRemindDialog : Window
{
    /// <summary>提醒条目显示模型（供 ItemsControl 绑定）</summary>
    public class RemindItem
    {
        public string? Content { get; set; }
        public string? RemindText { get; set; }
    }

    public MemoRemindDialog(List<Memo> items)
    {
        InitializeComponent();

        var list = new List<RemindItem>();
        foreach (var m in items)
        {
            list.Add(new RemindItem
            {
                Content = m.Content,
                RemindText = m.RemindAt?.ToString("MM-dd HH:mm")
            });
        }
        listItems.ItemsSource = list;

        // 播放系统提示音
        try { SystemSounds.Exclamation.Play(); } catch { /* 声音失败不影响提醒 */ }
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }
}