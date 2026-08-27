using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

public partial class PartClassWindow : Window
{
    public ObservableCollection<PartClass> Children { get; } = new();
    private PartClass? _selectedClass;

    public PartClassWindow()
    {
        InitializeComponent();
        dgChildren.ItemsSource = Children;
        LoadTree();
    }

    private async void LoadTree()
    {
        tvClass.Items.Clear();
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            var all = (await db.QueryAsync<PartClass>(
                "SELECT * FROM CLASSES ORDER BY CLASS_TYPE, CLASS_NO")).ToList();

            var groups = all.GroupBy(c => c.ClassId).Select(g => g.First()).ToList();
            foreach (var g in groups)
            {
                var item = new TreeViewItem { Header = $"{g.ClassTypeNm ?? g.ClassId} ({g.ClassId})", Tag = g };
                var children = all.Where(c => c.ClassId == g.ClassId).ToList();
                foreach (var c in children)
                {
                    item.Items.Add(new TreeViewItem { Header = $"{c.ClassNo} - {c.ClassName}", Tag = c });
                }
                tvClass.Items.Add(item);
            }
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "加载分类失败"); MessageBox.Show($"加载分类失败:{ex.Message}", "错误"); }
    }

    private async void TvClass_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is TreeViewItem item && item.Tag is PartClass cls)
        {
            _selectedClass = cls;
            txtClassName.Text = cls.ClassName;
            txtParentId.Text = cls.ClassId ?? "";
            try
            {
                var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
                var children = await db.QueryAsync<PartClass>(
                    "SELECT * FROM CLASSES WHERE CLASS_TYPE = @Type ORDER BY CLASS_NO",
                    new { Type = cls.ClassId });
                Children.Clear();
                foreach (var c in children) Children.Add(c);
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "加载子分类失败");
            }
        }
    }

    private async void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var owner = Window.GetWindow(this);
        var dlg = new InputBoxDialog("新增分类项", "分类名称", "");
        if (owner != null && owner.IsLoaded) dlg.Owner = owner;
        if (dlg.ShowDialog() == true && !string.IsNullOrEmpty(dlg.InputText))
        {
            try
            {
                var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
                await db.ExecuteAsync("INSERT INTO CLASSES (CLASS_TYPE, CLASS_NO, CLASS_NM, CLASS_EN) VALUES (@Type, @No, @Name, @Name)",
                    new { Type = "0001", No = dlg.InputText.Trim(), Name = dlg.InputText.Trim() });
                LoadTree();
            }
            catch (Exception ex) { Serilog.Log.Warning(ex, "新增分类失败"); MessageBox.Show($"新增失败: {ex.Message}", "错误"); }
        }
    }

    private async void BtnAddChild_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedClass == null) { MessageBox.Show("请先选择分类类型", "提示"); return; }
        var owner = Window.GetWindow(this);
        var dlg = new InputBoxDialog("新增子分类", "分类名称", "");
        if (owner != null && owner.IsLoaded) dlg.Owner = owner;
        if (dlg.ShowDialog() == true && !string.IsNullOrEmpty(dlg.InputText))
        {
            try
            {
                var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
                await db.ExecuteAsync("INSERT INTO CLASSES (CLASS_TYPE, CLASS_NO, CLASS_NM, CLASS_EN) VALUES (@Type, @No, @Name, @Name)",
                    new { Type = _selectedClass.ClassId, No = dlg.InputText.Trim(), Name = dlg.InputText.Trim() });
                LoadTree();
            }
            catch (Exception ex) { Serilog.Log.Warning(ex, "新增子分类失败"); MessageBox.Show($"新增失败: {ex.Message}", "错误"); }
        }
    }

    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedClass == null) { MessageBox.Show("请选择分类", "提示"); return; }
        if (MessageBox.Show($"确认删除分类 {_selectedClass.ClassName}?", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            await db.ExecuteAsync("DELETE FROM CLASSES WHERE CLASS_TYPE = @Type AND CLASS_NO = @No",
                new { Type = _selectedClass.ClassId, No = _selectedClass.ClassNo });
            LoadTree();
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "删除分类失败"); MessageBox.Show($"删除失败: {ex.Message}", "错误"); }
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedClass == null) return;
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            await db.ExecuteAsync("UPDATE CLASSES SET CLASS_NM=@Name WHERE CLASS_TYPE=@Type AND CLASS_NO=@No",
                new { Name = txtClassName.Text.Trim(), Type = _selectedClass.ClassId, No = _selectedClass.ClassNo });
            LoadTree();
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "保存分类失败"); MessageBox.Show($"保存失败: {ex.Message}", "错误"); }
    }
}
