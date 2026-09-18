using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using QP11.Core.Entities;
using QP11.Services;

namespace QP11.Wpf.Views;

/// <summary>备忘录列表行显示模型</summary>
public class MemoRow : INotifyPropertyChanged
{
    public Memo Base { get; }

    public long Id => Base.Id;
    public string Content => Base.Content ?? "";
    public DateTime? CreateTime => Base.CreateTime;

    public string PriorityText => Base.Priority switch
    {
        3 => "高",
        2 => "中",
        _ => "低"
    };

    public Brush PriorityBrush => Base.Priority switch
    {
        3 => new SolidColorBrush(Color.FromRgb(0xC0, 0x00, 0x00)),
        2 => new SolidColorBrush(Color.FromRgb(0xE6, 0x7E, 0x22)),
        _ => new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88))
    };

    public string RemindText
    {
        get
        {
            if (Base.RemindType == 0 || Base.RemindAt == null) return "—";
            var at = Base.RemindAt.Value.ToString("yyyy-MM-dd HH:mm");
            return Base.RemindType == 2
                ? $"倒计时 {Base.CountdownMinutes} 分钟（{at}）"
                : $"定时 {at}";
        }
    }

    public string CreateText => Base.CreateTime?.ToString("yyyy-MM-dd HH:mm") ?? "";

    /// <summary>到期且未完成（提醒行红色标注）</summary>
    public bool IsDue => Base.RemindType > 0 && !Base.Done && Base.RemindAt != null && Base.RemindAt <= DateTime.Now;

    private bool _done;
    public bool Done
    {
        get => _done;
        set
        {
            if (_done == value) return;
            _done = value;
            OnPropertyChanged(nameof(Done));
        }
    }

    public MemoRow(Memo memo)
    {
        Base = memo;
        _done = memo.Done;
    }

    /// <summary>完成状态等变化后刷新派生显示（行标注/红灯）</summary>
    public void RefreshDerived()
    {
        OnPropertyChanged(nameof(IsDue));
        OnPropertyChanged(nameof(PriorityText));
        OnPropertyChanged(nameof(RemindText));
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class MemoWindow : Window
{
    private readonly MemoService _service;
    private readonly string _operator;

    private readonly ObservableCollection<MemoRow> _rows = new();
    private bool _isWorking; // 防止勾选事件重入

    public MemoWindow(MemoService service)
    {
        InitializeComponent();
        _service = service;
        _operator = App.CurrentUser?.Username ?? "";
        dg.ItemsSource = _rows;
        // WindowHostControl 内嵌窗口不会触发 Loaded 事件，需构造后立即加载，否则首屏为空
        _ = LoadDataAsync();
    }

    private async System.Threading.Tasks.Task LoadDataAsync()
    {
        try
        {
            IsEnabled = false;
            var items = await _service.GetByOperatorAsync(_operator);
            _rows.Clear();
            foreach (var m in items)
                _rows.Add(new MemoRow(m));
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "加载备忘录失败");
            MessageBox.Show($"加载备忘录失败: {ex.Message}", "错误");
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private async void CheckDone_Changed(object sender, RoutedEventArgs e)
    {
        if (_isWorking || sender is not System.Windows.Controls.CheckBox cb) return;
        if (cb.DataContext is not MemoRow row) return;

        var done = cb.IsChecked == true;
        // 值未变化直接忽略：Items.Refresh 重建行时会重建 CheckBox 并再次触发事件，
        // 若不做此守卫将形成「勾选→写库→刷新→再触发」的无限循环（且用户并未点击）
        if (row.Base.Done == done) return;

        _isWorking = true;
        try
        {
            await _service.MarkDoneAsync(row.Id, done);
            row.Base.Done = done;
            // 勾选状态变化后刷新行样式（到期/完成标注）
            row.RefreshDerived();
            dg.Items.Refresh();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "更新备忘完成状态失败");
            MessageBox.Show($"更新失败: {ex.Message}", "错误");
            await LoadDataAsync();
        }
        finally
        {
            _isWorking = false;
        }
    }

    /// <summary>弹窗所属窗口：本窗口被 WindowHostControl 内嵌（未 Show 过），不能作 Owner，改用已显示的主窗口</summary>
    private Window? DialogOwner => Application.Current.MainWindow;

    private async void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new MemoEditDialog(null);
        if (DialogOwner != null) dlg.Owner = DialogOwner;
        if (dlg.ShowDialog() != true || dlg.ResultMemo == null) return;

        var memo = dlg.ResultMemo;
        memo.Operator = _operator;
        try
        {
            memo.Id = await _service.InsertAsync(memo);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "新增备忘失败");
            MessageBox.Show($"新增失败: {ex.Message}", "错误");
            return;
        }
        await LoadDataAsync();
    }

    private async void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (dg.SelectedItem is not MemoRow row) return;

        var dlg = new MemoEditDialog(row.Base);
        if (DialogOwner != null) dlg.Owner = DialogOwner;
        if (dlg.ShowDialog() != true || dlg.ResultMemo == null) return;

        var memo = dlg.ResultMemo;
        memo.Operator = _operator;
        try
        {
            await _service.UpdateAsync(memo);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "更新备忘失败");
            MessageBox.Show($"更新失败: {ex.Message}", "错误");
            return;
        }
        await LoadDataAsync();
    }

    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (dg.SelectedItem is not MemoRow row) return;
        if (MessageBox.Show("确定删除该备忘？", "确认删除",
                MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            await _service.DeleteAsync(row.Id);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "删除备忘失败");
            MessageBox.Show($"删除失败: {ex.Message}", "错误");
            return;
        }
        await LoadDataAsync();
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e) => await LoadDataAsync();

    private void Dg_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (dg.SelectedItem is MemoRow) BtnEdit_Click(dg, new RoutedEventArgs());
    }
}