using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using QP11.Core.Interfaces;
using QP11.Core.Models;
using QP11.Wpf.Helpers;

namespace QP11.Wpf.Views;

public class PinyinFixDisplay
{
    public long PartId { get; set; }
    public string? Partno { get; set; }
    public string? Name { get; set; }
    public string? NamePyOld { get; set; }
    public string? NamePyNew { get; set; }
    public string? Cartype { get; set; }
    public string? CartypePyOld { get; set; }
    public string? CartypePyNew { get; set; }
}

public partial class PinyinFixWindow : Window
{
    private readonly IPartRepository _partRepo;
    private List<PinyinFixDisplay> _fixItems = [];

    public PinyinFixWindow(IPartRepository partRepo)
    {
        InitializeComponent();
        _partRepo = partRepo;
    }

    private async void BtnScan_Click(object sender, RoutedEventArgs e)
    {
        btnScan.IsEnabled = false;
        txtStatus.Text = "扫描中...";

        try
        {
            var rows = await _partRepo.GetMissingPinyinAsync();
            // 全量校正：对每条记录重新生成拼音，与原值不一致（忽略大小写）即纳入修复列表
            _fixItems = rows.Select(r =>
            {
                var namePyNew = PinyinHelper.GetPinyinInitials(r.Name ?? "");
                var cartypePyNew = PinyinHelper.GetPinyinInitials(r.Cartype ?? "");
                var nameNeedsFix = !string.Equals(r.NamePy ?? "", namePyNew, StringComparison.OrdinalIgnoreCase);
                var cartypeNeedsFix = !string.Equals(r.CartypePy ?? "", cartypePyNew, StringComparison.OrdinalIgnoreCase);
                if (!nameNeedsFix && !cartypeNeedsFix) return null;
                return new PinyinFixDisplay
                {
                    PartId = r.PartId,
                    Partno = r.Partno,
                    Name = r.Name,
                    NamePyOld = r.NamePy ?? "",
                    NamePyNew = nameNeedsFix ? namePyNew : (r.NamePy ?? ""),
                    Cartype = r.Cartype,
                    CartypePyOld = r.CartypePy ?? "",
                    CartypePyNew = cartypeNeedsFix ? cartypePyNew : (r.CartypePy ?? "")
                };
            }).Where(x => x != null).Select(x => x!).ToList();

            dgResult.ItemsSource = new ObservableCollection<PinyinFixDisplay>(_fixItems);
            btnFixAll.IsEnabled = _fixItems.Count > 0;

            var nameMissing = _fixItems.Count(i => !string.IsNullOrEmpty(i.Name) && !string.Equals(i.NamePyOld, i.NamePyNew, StringComparison.OrdinalIgnoreCase));
            var cartypeMissing = _fixItems.Count(i => !string.IsNullOrEmpty(i.Cartype) && !string.Equals(i.CartypePyOld, i.CartypePyNew, StringComparison.OrdinalIgnoreCase));
            txtStatus.Text = $"扫描完成，发现 {_fixItems.Count} 条需修复记录";
            txtInfo.Text = _fixItems.Count > 0
                ? $"名称拼音不一致: {nameMissing} 条，车型拼音不一致: {cartypeMissing} 条"
                : "所有配件拼音码完整，无需修复";
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "扫描拼音缺失失败");
            MessageBox.Show($"扫描失败: {ex.Message}", "错误");
        }
        finally
        {
            btnScan.IsEnabled = true;
        }
    }

    private async void BtnFixAll_Click(object sender, RoutedEventArgs e)
    {
        if (_fixItems.Count == 0) return;

        var result = MessageBox.Show(
            $"即将修复 {_fixItems.Count} 条记录的拼音码，是否继续？",
            "确认修复", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        btnFixAll.IsEnabled = false;
        txtStatus.Text = "修复中...";

        try
        {
            int fixedCount = 0;
            foreach (var item in _fixItems)
            {
                await _partRepo.UpdatePinyinAsync(item.PartId, item.NamePyNew, item.CartypePyNew);
                fixedCount++;
            }

            txtStatus.Text = $"修复完成，共更新 {fixedCount} 条记录";
            txtInfo.Text = "修复完成，可重新扫描确认";
            dgResult.ItemsSource = null;
            _fixItems.Clear();
            btnFixAll.IsEnabled = false;
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "修复拼音码失败");
            MessageBox.Show($"修复失败: {ex.Message}", "错误");
        }
        finally
        {
            btnFixAll.IsEnabled = _fixItems.Count > 0;
        }
    }
}
