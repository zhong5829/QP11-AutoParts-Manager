using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

public partial class LocationWindow : Window
{
    private readonly IPartLocationRepository _repo;
    public ObservableCollection<PartLocation> Items { get; } = new();

    public LocationWindow(IPartLocationRepository repo)
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
            txtCount.Text = $"共 {Items.Count} 条";
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "加载仓位失败");
            MessageBox.Show($"加载失败: {ex.Message}", "错误");
        }
    }

    private async void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        // 自动生成编号
        string? newPlace = null;
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            newPlace = (await db.ExecuteScalarAsync<string>("SELECT MAX(place) FROM part_place")) ?? "000";
            int num = 0;
            foreach (var c in newPlace) if (char.IsDigit(c)) num = num * 10 + (c - '0'); else num = 0;
            newPlace = (num + 1).ToString("D3");
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "生成仓位编号失败"); newPlace = "001"; }

        var owner = Window.GetWindow(this);
        var dlg = new InputBoxDialog("新增仓位", "名称,负责人,类型,区域,备注", "");
        if (owner != null && owner.IsLoaded) dlg.Owner = owner;
        if (dlg.ShowDialog() == true)
        {
            var parts = dlg.InputText?.Split(',') ?? Array.Empty<string>();
            try
            {
                await _repo.InsertAsync(new PartLocation
                {
                    Place = newPlace,
                    PlaceNm = parts.Length > 0 ? parts[0].Trim() : "",
                    PlaceUser = parts.Length > 1 ? parts[1].Trim() : "",
                    PlaceType = parts.Length > 2 ? parts[2].Trim() : "",
                    PlaceArea = parts.Length > 3 ? parts[3].Trim() : "",
                    PlaceNote = parts.Length > 4 ? parts[4].Trim() : ""
                });
                LoadData();
            }
            catch (Exception ex) { Serilog.Log.Warning(ex, "新增仓位失败"); MessageBox.Show($"新增失败: {ex.Message}", "错误"); }
        }
    }

    private async void BtnEdit_Click(object sender, RoutedEventArgs e)
    {
        if (dgList.SelectedItem is not PartLocation item) { MessageBox.Show("请选择记录", "提示"); return; }
        var owner = Window.GetWindow(this);
        var dlg = new InputBoxDialog("编辑仓位", "名称,负责人,类型,区域,备注",
            $"{item.PlaceNm},{item.PlaceUser},{item.PlaceType},{item.PlaceArea},{item.PlaceNote}");
        if (owner != null && owner.IsLoaded) dlg.Owner = owner;
        if (dlg.ShowDialog() == true)
        {
            var parts = dlg.InputText?.Split(',') ?? Array.Empty<string>();
            try
            {
                item.PlaceNm = parts.Length > 0 ? parts[0].Trim() : item.PlaceNm;
                item.PlaceUser = parts.Length > 1 ? parts[1].Trim() : item.PlaceUser;
                item.PlaceType = parts.Length > 2 ? parts[2].Trim() : item.PlaceType;
                item.PlaceArea = parts.Length > 3 ? parts[3].Trim() : item.PlaceArea;
                item.PlaceNote = parts.Length > 4 ? parts[4].Trim() : item.PlaceNote;
                await _repo.UpdateAsync(item);
                LoadData();
            }
            catch (Exception ex) { Serilog.Log.Warning(ex, "编辑仓位失败"); MessageBox.Show($"编辑失败: {ex.Message}", "错误"); }
        }
    }

    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (dgList.SelectedItem is not PartLocation item) { MessageBox.Show("请选择记录", "提示"); return; }
        if (MessageBox.Show("确认删除?", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        try { await _repo.DeleteAsync(item.Place!); LoadData(); }
        catch (Exception ex) { Serilog.Log.Warning(ex, "删除仓位失败"); MessageBox.Show($"删除失败: {ex.Message}", "错误"); }
    }

    private async void BtnFilter_Click(object sender, RoutedEventArgs e)
    {
        var keyword = txtFilter.Text.Trim();
        try
        {
            var data = await _repo.GetAllAsync();
            Items.Clear();
            IEnumerable<PartLocation> filtered = data;
            if (!string.IsNullOrEmpty(keyword))
                filtered = data.Where(d =>
                    (d.PlaceNm?.Contains(keyword) == true) ||
                    (d.PlaceType?.Contains(keyword) == true) ||
                    (d.PlaceArea?.Contains(keyword) == true));
            foreach (var item in filtered) Items.Add(item);
            txtCount.Text = $"共 {Items.Count} 条";
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "筛选仓位失败"); MessageBox.Show($"筛选失败: {ex.Message}", "错误"); }
    }
}
