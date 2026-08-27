using System;
using System.Collections.ObjectModel;
using System.Windows;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

public partial class CodeRuleWindow : Window
{
    private readonly ICodeRuleRepository _repo;
    public ObservableCollection<CodeRule> Items { get; } = new();

    public CodeRuleWindow(ICodeRuleRepository repo)
    {
        _repo = repo;
        InitializeComponent();
        dgList.ItemsSource = Items;
        LoadData();
    }

    private async void LoadData()
    {
        try
        {
            var data = await _repo.GetAllAsync();
            Items.Clear();
            foreach (var item in data) Items.Add(item);
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "加载编码规则失败"); MessageBox.Show($"加载失败: {ex.Message}", "错误"); }
    }

    private async void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new InputBoxDialog("新增编码规则", "前缀,说明", "") { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true)
        {
            var parts = dlg.InputText?.Split(',') ?? Array.Empty<string>();
            var prefix = parts.Length > 0 ? parts[0].Trim() : "";
            var memo = parts.Length > 1 ? parts[1].Trim() : "";
            if (string.IsNullOrEmpty(prefix)) { MessageBox.Show("前缀不能为空", "提示"); return; }
            try
            {
                await _repo.InsertAsync(new CodeRule
                {
                    Prefix = prefix,
                    Memo = memo,
                    SeqLength = 4,
                    CurrentSeq = 0,
                    ResetDaily = "Y"
                });
                LoadData();
            }
            catch (Exception ex) { Serilog.Log.Warning(ex, "新增编码规则失败"); MessageBox.Show($"新增失败: {ex.Message}", "错误"); }
        }
    }

    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (dgList.SelectedItem is not CodeRule item) { MessageBox.Show("请选择规则", "提示"); return; }
        if (MessageBox.Show($"确认删除规则 {item.TableName}?", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        try { await _repo.DeleteAsync(item.Id); LoadData(); }
        catch (Exception ex) { Serilog.Log.Warning(ex, "删除编码规则失败"); MessageBox.Show($"删除失败: {ex.Message}", "错误"); }
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            foreach (var item in Items)
                await _repo.UpdateAsync(item);
            MessageBox.Show("保存成功", "提示");
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "保存编码规则失败"); MessageBox.Show($"保存失败: {ex.Message}", "错误"); }
    }
}
