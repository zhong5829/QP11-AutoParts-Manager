using System;
using System.Collections.ObjectModel;
using System.Windows;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

public partial class LogisticsWindow : Window
{
    private readonly ILogisticsRepository _repo;
    public ObservableCollection<Logistics> Items { get; } = new();

    public LogisticsWindow(ILogisticsRepository repo)
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
        catch (Exception ex) { Serilog.Log.Warning(ex, "加载物流列表失败"); MessageBox.Show($"加载失败: {ex.Message}", "错误"); }
    }

    private async void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        // 自动生成编号
        string? newSid = null;
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            newSid = (await db.ExecuteScalarAsync<string>("SELECT MAX(sid) FROM wuliu_infor")) ?? "000";
            int num = 0;
            foreach (var c in newSid) if (char.IsDigit(c)) num = num * 10 + (c - '0'); else num = 0;
            newSid = (num + 1).ToString("D3");
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "生成物流编号失败"); newSid = "001"; }

        var entity = new Logistics { Sid = newSid };
        var owner = Window.GetWindow(this);
        var dlg = new LogisticsEditWindow(entity);
        if (owner != null && owner.IsLoaded) dlg.Owner = owner;

        if (dlg.ShowDialog() == true)
        {
            try
            {
                await _repo.InsertAsync(dlg.Entity);
                LoadData();
            }
            catch (Exception ex) { Serilog.Log.Warning(ex, "新增物流失败"); MessageBox.Show($"新增失败: {ex.Message}", "错误"); }
        }
    }

    private async void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (dgList.SelectedItem is not Logistics item) { MessageBox.Show("请选择记录", "提示"); return; }
        var owner = Window.GetWindow(this);
        var dlg = new LogisticsEditWindow(item);
        if (owner != null && owner.IsLoaded) dlg.Owner = owner;

        if (dlg.ShowDialog() == true)
        {
            try
            {
                await _repo.UpdateAsync(dlg.Entity);
                LoadData();
            }
            catch (Exception ex) { Serilog.Log.Warning(ex, "编辑物流失败"); MessageBox.Show($"编辑失败: {ex.Message}", "错误"); }
        }
    }

    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (dgList.SelectedItem is not Logistics item) { MessageBox.Show("请选择记录", "提示"); return; }
        if (MessageBox.Show("确认删除?", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        try { await _repo.DeleteAsync(item.Sid!); LoadData(); }
        catch (Exception ex) { Serilog.Log.Warning(ex, "删除物流失败"); MessageBox.Show($"删除失败: {ex.Message}", "错误"); }
    }
}
