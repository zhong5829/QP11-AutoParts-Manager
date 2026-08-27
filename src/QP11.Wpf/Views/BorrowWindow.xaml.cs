using System;
using System.Collections.ObjectModel;
using System.Windows;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace QP11.Wpf.Views;

public partial class BorrowWindow : Window
{
    private readonly IBorrowRepository _borrowRepo = App.ServiceProvider.GetRequiredService<IBorrowRepository>();
    public ObservableCollection<Borrow> Borrows { get; } = new();

    public BorrowWindow()
    {
        InitializeComponent();
        dgBorrows.ItemsSource = Borrows;
        LoadBorrows();
    }

    private async void LoadBorrows(string? status = null)
    {
        try
        {
            Borrows.Clear();
            var data = string.IsNullOrEmpty(status)
                ? await _borrowRepo.GetAllAsync()
                : await _borrowRepo.GetByStatusAsync(status);
            foreach (var b in data) Borrows.Add(b);
            txtCount.Text = $"共 {Borrows.Count} 条记录";
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "加载借还记录失败"); MessageBox.Show($"加载借还记录失败: {ex.Message}", "错误"); }
    }

    private void CboStatus_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        var status = cboStatus.SelectedIndex switch
        {
            1 => "借出",
            2 => "已还",
            _ => null
        };
        LoadBorrows(status);
    }

    private async void BtnAdd_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new InputBoxDialog("新增借出", "工具编号,工具名称,借用人,备注", "") { Owner = Window.GetWindow(this) };
        if (dlg.ShowDialog() == true)
        {
            var parts = dlg.InputText?.Split(',') ?? Array.Empty<string>();
            try
            {
                var entity = new Borrow
                {
                    Gjbh = parts.Length > 0 ? parts[0].Trim() : "",
                    Gjmc = parts.Length > 1 ? parts[1].Trim() : "",
                    Jyr = parts.Length > 2 ? parts[2].Trim() : "",
                    Bz = parts.Length > 3 ? parts[3].Trim() : "",
                    Jybz = parts.Length > 2 ? parts[2].Trim() : "",
                    Zt = "借出",
                    Gjjz = 0
                };
                await _borrowRepo.InsertAsync(entity);
                LoadBorrows();
            }
            catch (Exception ex) { Serilog.Log.Warning(ex, "新增借出失败"); MessageBox.Show($"新增借出失败: {ex.Message}", "错误"); }
        }
    }

    private async void BtnReturn_Click(object sender, RoutedEventArgs e)
    {
        if (dgBorrows.SelectedItem is not Borrow borrow) return;
        if (borrow.Zt != "借出")
        {
            MessageBox.Show("只能归还状态为'借出'的记录", "提示");
            return;
        }
        if (MessageBox.Show($"确认归还此记录?", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
        try
        {
            await _borrowRepo.UpdateStatusAsync(borrow.Id, "已还");
            LoadBorrows();
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "归还失败"); MessageBox.Show($"归还失败: {ex.Message}", "错误"); }
    }
}
