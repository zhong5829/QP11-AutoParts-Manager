using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using QP11.Wpf.Helpers;

namespace QP11.Wpf.Views;

public class BasicDataItem : INotifyPropertyChanged
{
    public long? Partid { get; set; }
    public bool IsChanged { get; set; }

    private string? _partNo = "";
    public string? PartNo { get => _partNo; set { _partNo = value; IsChanged = true; OnPropertyChanged(nameof(PartNo)); } }

    private string? _name = "";
    public string? Name { get => _name; set { _name = value; IsChanged = true; OnPropertyChanged(nameof(Name)); } }

    private string? _cartype = "";
    public string? Cartype { get => _cartype; set { _cartype = value; IsChanged = true; OnPropertyChanged(nameof(Cartype)); } }

    /// <summary>车型拼音码（用于拼音搜索，非网格编辑列）</summary>
    public string? CartypePy { get; set; }

    private string? _pyCode = "";
    public string? PyCode { get => _pyCode; set { _pyCode = value; IsChanged = true; OnPropertyChanged(nameof(PyCode)); } }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class BasicDataControl : UserControl, ITabContent
{
    private List<BasicDataItem> _allItems = new();
    private readonly TextBox[] _searchBoxes;

    public ObservableCollection<BasicDataItem> Items { get; } = new();
    public string TabTitle => "基础数据";
    public bool HasUnsavedChanges => Items.Any(i => i.IsChanged);
    public event EventHandler? RequestClose;

    public BasicDataControl()
    {
        InitializeComponent();
        dgData.ItemsSource = Items;
        _searchBoxes = new[] { txtPartNo, txtName, txtCartype };
        LoadData();
    }

    private async void LoadData()
    {
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            var sql = "SELECT DISTINCT partid, partno, name, cartype, name_py, cartype_py FROM part_data WHERE (DEL IS NULL OR DEL = '0') ORDER BY partno";
            var rows = await db.QueryAsync(sql);

            Items.Clear();
            _allItems.Clear();
            foreach (IDictionary<string, object> row in rows)
            {
                var item = new BasicDataItem
                {
                    Partid = long.TryParse((row["partid"] ?? "0").ToString(), out var pid) ? pid : null,
                    PartNo = (row["partno"] ?? "").ToString(),
                    Name = (row["name"] ?? "").ToString(),
                    Cartype = (row["cartype"] ?? "").ToString(),
                    CartypePy = (row["cartype_py"] ?? "").ToString(),
                    PyCode = (row["name_py"] ?? "").ToString(),
                    IsChanged = false
                };
                Items.Add(item);
                _allItems.Add(item);
            }
            txtStatus.Text = $"共 {Items.Count} 条";
            ApplyFilter();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载数据失败: {ex.Message}", "错误");
        }
    }

    private void ApplyFilter()
    {
        var partNo = txtPartNo.Text.Trim();
        var name = txtName.Text.Trim();
        var cartype = txtCartype.Text.Trim();

        if (string.IsNullOrEmpty(partNo) && string.IsNullOrEmpty(name) && string.IsNullOrEmpty(cartype))
        {
            dgData.ItemsSource = Items;
            return;
        }

        var filtered = _allItems.Where(i =>
            (string.IsNullOrEmpty(partNo) || i.PartNo?.Contains(partNo, StringComparison.OrdinalIgnoreCase) == true) &&
            (string.IsNullOrEmpty(name) || i.Name?.Contains(name, StringComparison.OrdinalIgnoreCase) == true || i.PyCode?.Contains(name, StringComparison.OrdinalIgnoreCase) == true) &&
            (string.IsNullOrEmpty(cartype) || i.Cartype?.Contains(cartype, StringComparison.OrdinalIgnoreCase) == true ||
             (IsPureAscii(cartype) && i.CartypePy?.Contains(cartype, StringComparison.OrdinalIgnoreCase) == true))
        ).ToList();
        dgData.ItemsSource = filtered;
    }

    private void TxtSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilter();
    }

    /// <summary>输入框获得焦点时自动全选文本</summary>
    private void TxtSearch_GotFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox tb)
        {
            tb.Dispatcher.BeginInvoke(new Action(() => tb.SelectAll()));
        }
    }

    /// <summary>左右方向键切换搜索框</summary>
    private void TxtSearch_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (sender is not TextBox tb) return;

        if (e.Key == System.Windows.Input.Key.Left && tb.CaretIndex == 0)
        {
            // 光标在最左时按左键 → 跳到上一个输入框
            MoveFocusTo(tb, -1);
            e.Handled = true;
        }
        else if (e.Key == System.Windows.Input.Key.Right && tb.CaretIndex == tb.Text.Length)
        {
            // 光标在最右时按右键 → 跳到下一个输入框
            MoveFocusTo(tb, 1);
            e.Handled = true;
        }
    }

    private void MoveFocusTo(TextBox current, int direction)
    {
        for (int i = 0; i < _searchBoxes.Length; i++)
        {
            if (_searchBoxes[i] == current)
            {
                int next = i + direction;
                if (next >= 0 && next < _searchBoxes.Length)
                {
                    _searchBoxes[next].Focus();
                }
                break;
            }
        }
    }

    /// <summary>判断字符串是否为纯ASCII字符（拼音搜索意图）</summary>
    private static bool IsPureAscii(string text)
    {
        foreach (char c in text)
            if (c > 127) return false;
        return true;
    }

    private void DgData_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
    {
        if (e.Column.Header?.ToString() == "名称" && e.EditingElement is TextBox tb)
        {
            var item = e.Row.Item as BasicDataItem;
            if (item != null && !string.IsNullOrEmpty(tb.Text))
            {
                item.PyCode = PinyinHelper.GetPinyinInitials(tb.Text);
            }
        }
    }

    /// <summary>添加配件：弹出新增窗口</summary>
    private void BtnAddPart_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new PartEditWindow { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true)
            LoadData();
    }

    /// <summary>双击配件行弹出编辑窗口</summary>
    private async void DgData_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (dgData.SelectedItem is not BasicDataItem item || item.Partid == null) return;
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            var part = await db.QueryFirstOrDefaultAsync<PartData>(
                "SELECT * FROM part_data WHERE partid=@Id", new { Id = item.Partid });
            if (part != null)
            {
                var dlg = new PartEditWindow(part);
                dlg.ShowDialog();
                LoadData();
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"打开配件编辑失败: {ex.Message}", "错误");
        }
    }

    private async void SaveChanges()
    {
        var changed = Items.Where(i => i.IsChanged).ToList();
        if (changed.Count == 0)
        {
            MessageBox.Show("没有需要保存的更改", "提示");
            return;
        }

        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            int count = 0;
            foreach (var item in changed)
            {
                count += await db.ExecuteAsync(
                    "UPDATE part_data SET partno=@PartNo, name=@Name, cartype=@Cartype, name_py=@PyCode WHERE partid=@Partid",
                    new { PartNo = item.PartNo, Name = item.Name, Cartype = item.Cartype, PyCode = item.PyCode, Partid = item.Partid });
                item.IsChanged = false;
            }
            txtStatus.Text = $"已保存 {count} 条更改";
            MessageBox.Show($"保存成功，共更新 {count} 条记录", "提示");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败: {ex.Message}", "错误");
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => OnClose();

    public void OnAdd() { }
    public void OnEdit() { }
    public void OnQuery() => LoadData();
    public void OnDelete() { }
    public void OnSave() => SaveChanges();
    public void OnSettle() { }
    public void OnPrint() { }
    public void OnReturn() { }
    public void OnCancel() { }
    public void OnHistory() { }
    public void OnClose()
    {
        if (Items.Any(i => i.IsChanged))
        {
            if (MessageBox.Show("有未保存的更改，确定关闭？", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;
        }
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}
