using System;
using System.Windows;
using QP11.Core.Entities;

namespace QP11.Wpf.Views;

public partial class MemoEditDialog : Window
{
    private readonly Memo? _source;
    private Memo? _result;

    /// <summary>编辑结果，确认返回非 null</summary>
    public Memo? ResultMemo => _result;

    /// <param name="source">编辑时传入现有条目；新增时传 null</param>
    public MemoEditDialog(Memo? source)
    {
        InitializeComponent();
        _source = source;
        Loaded += (s, e) => InitFields();
    }

    private void InitFields()
    {
        if (_source != null)
        {
            txtContent.Text = _source.Content ?? "";
            cboPriority.SelectedIndex = Math.Clamp(_source.Priority - 1, 0, 2);
        }
        else
        {
            cboPriority.SelectedIndex = 0;
        }

        var remindType = _source?.RemindType ?? 0;
        rbNone.IsChecked = remindType == 0;
        rbScheduled.IsChecked = remindType == 1;
        rbCountdown.IsChecked = remindType == 2;

        dtpRemindDate.SelectedDate = _source?.RemindAt?.Date ?? DateTime.Today;
        txtRemindTime.Text = _source?.RemindAt?.ToString("HH:mm") ?? "09:00";
        txtCountdown.Text = _source?.CountdownMinutes?.ToString() ?? "30";

        UpdateRemindPanels();
        txtContent.Focus();
    }

    private void Remind_Changed(object sender, RoutedEventArgs e) => UpdateRemindPanels();

    private void UpdateRemindPanels()
    {
        // InitializeComponent 解析期间 rbNone(默认选中)即触发 Checked，此时后续控件尚未初始化，判空跳过
        if (rbNone == null || rbScheduled == null || rbCountdown == null ||
            lblScheduled == null || lblCountdown == null ||
            panelScheduled == null || panelCountdown == null)
            return;

        var isScheduled = rbScheduled.IsChecked == true;
        var isCountdown = rbCountdown.IsChecked == true;
        panelScheduled.Visibility = isScheduled ? Visibility.Visible : Visibility.Collapsed;
        lblScheduled.Visibility = isScheduled ? Visibility.Visible : Visibility.Collapsed;
        panelCountdown.Visibility = isCountdown ? Visibility.Visible : Visibility.Collapsed;
        lblCountdown.Visibility = isCountdown ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        var content = txtContent.Text.Trim();
        if (string.IsNullOrEmpty(content))
        {
            MessageBox.Show("请输入备忘内容", "提示");
            txtContent.Focus();
            return;
        }

        var priority = cboPriority.SelectedIndex + 1;
        int remindType;
        DateTime? remindAt;
        int? countdownMinutes;

        if (rbScheduled.IsChecked == true)
        {
            remindType = 1;
            var date = dtpRemindDate.SelectedDate;
            if (date == null)
            {
                MessageBox.Show("请选择提醒日期", "提示");
                dtpRemindDate.Focus();
                return;
            }
            if (!DateTime.TryParse($"{date.Value:yyyy-MM-dd} {txtRemindTime.Text.Trim()}", out var at))
            {
                MessageBox.Show("提醒时间格式不正确，请使用 HH:mm（如 09:30）", "提示");
                txtRemindTime.Focus();
                return;
            }
            remindAt = at;
            countdownMinutes = null;
        }
        else if (rbCountdown.IsChecked == true)
        {
            remindType = 2;
            if (!int.TryParse(txtCountdown.Text.Trim(), out var minutes) || minutes <= 0)
            {
                MessageBox.Show("请输入有效的分钟数（大于 0）", "提示");
                txtCountdown.Focus();
                return;
            }
            countdownMinutes = minutes;
            remindAt = DateTime.Now.AddMinutes(minutes);
        }
        else
        {
            remindType = 0;
            remindAt = null;
            countdownMinutes = null;
        }

        _result = new Memo
        {
            Id = _source?.Id ?? 0,
            Operator = _source?.Operator,
            Content = content,
            Priority = priority,
            Done = _source?.Done ?? false,
            RemindType = remindType,
            RemindAt = remindAt,
            CountdownMinutes = countdownMinutes,
            // 设置提醒后重置弹窗标记，允许按新时间重新提醒
            Reminded = false,
            CreateTime = _source?.CreateTime
        };

        DialogResult = true;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}