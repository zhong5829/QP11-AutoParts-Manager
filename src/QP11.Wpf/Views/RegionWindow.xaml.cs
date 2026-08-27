using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

public partial class RegionWindow : Window
{
    private readonly IRegionRepository _repo;
    public ObservableCollection<Region> Children { get; } = new();
    private Region? _selectedRegion;

    public RegionWindow(IRegionRepository repo)
    {
        _repo = repo;
        InitializeComponent();
        dgChildren.ItemsSource = Children;
        LoadTree();
    }

    private async void LoadTree()
    {
        tvRegion.Items.Clear();
        try
        {
            var roots = await _repo.GetChildrenAsync(null);
            foreach (var r in roots)
            {
                var item = new TreeViewItem { Header = r.Name, Tag = r };
                tvRegion.Items.Add(item);
            }
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "加载地区失败"); MessageBox.Show($"加载失败: {ex.Message}", "错误"); }
    }

    private async void TvRegion_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeViewItem item && item.Tag is Region region)
        {
            _selectedRegion = region;
            txtName.Text = region.Name;
            txtCode.Text = region.Code;
            try
            {
                var children = await _repo.GetChildrenAsync(region.Id);
                Children.Clear();
                foreach (var c in children) Children.Add(c);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "加载子地区失败");
            }
        }
    }

    private async void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new InputBoxDialog("新增地区", "名称,编码", "") { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true)
        {
            var parts = dlg.InputText?.Split(',') ?? Array.Empty<string>();
            try
            {
                await _repo.InsertAsync(new Region
                {
                    ParentId = _selectedRegion?.Id,
                    Name = parts.Length > 0 ? parts[0].Trim() : "",
                    Code = parts.Length > 1 ? parts[1].Trim() : ""
                });
                if (_selectedRegion != null) TvRegion_SelectedItemChanged(this, new RoutedPropertyChangedEventArgs<object>(null!, tvRegion.SelectedItem));
                else LoadTree();
            }
            catch (Exception ex) { Serilog.Log.Warning(ex, "新增地区失败"); MessageBox.Show($"新增失败: {ex.Message}", "错误"); }
        }
    }

    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (dgChildren.SelectedItem is not Region item) { MessageBox.Show("请选择子地区", "提示"); return; }
        if (MessageBox.Show("确认删除?", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        try { await _repo.DeleteAsync(item.Id); TvRegion_SelectedItemChanged(this, new RoutedPropertyChangedEventArgs<object>(null!, tvRegion.SelectedItem)); }
        catch (Exception ex) { Serilog.Log.Warning(ex, "删除地区失败"); MessageBox.Show($"删除失败: {ex.Message}", "错误"); }
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedRegion == null) return;
        try
        {
            _selectedRegion.Name = txtName.Text.Trim();
            _selectedRegion.Code = txtCode.Text.Trim();
            await _repo.UpdateAsync(_selectedRegion);
            LoadTree();
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "保存地区失败"); MessageBox.Show($"保存失败: {ex.Message}", "错误"); }
    }
}
