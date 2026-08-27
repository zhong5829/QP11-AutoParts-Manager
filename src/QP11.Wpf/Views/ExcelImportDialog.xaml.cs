using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using QP11.Wpf.Helpers;
using QP11.Wpf.Services;

namespace QP11.Wpf.Views;

public class ImportRowResult
{
    public int RowIndex { get; set; }
    public string? PartNo { get; set; }
    public string? PartName { get; set; }
    public decimal Amount { get; set; }
    public decimal InPrice { get; set; }
    public string? CarName { get; set; }
    public string? Cartype { get; set; }
    public string? Unit { get; set; }
    public string? Place { get; set; }
    public string? Memo { get; set; }
    public ImportRowStatus Status { get; set; }
    public long? MatchedPartId { get; set; }
    public string? StatusText { get; set; }
    public bool IsDuplicate { get; set; }
    public int DuplicateCount { get; set; }
    public decimal? MatchedLsPrice { get; set; }
    public decimal? MatchedPfPrice { get; set; }
    public string? OriginalName { get; set; }
}

public enum ImportRowStatus
{
    Exists,
    NewPart,
    Duplicate,
    InvalidPlace,
    MissingRequired,
    NameDiff
}

public partial class ExcelImportDialog : Window
{
    private ExcelParseResult? _parseResult;
    private List<PartData> _allParts = new();
    private List<ImportRowResult> _previewData = new();

    public List<BuyDetailItem> ImportedDetails { get; private set; } = new();
    public bool ImportConfirmed { get; private set; }
    public bool UpdatePartNames { get; private set; }
    public List<NameDiffUpdate> NameDiffItems { get; private set; } = new();

    private static readonly Dictionary<string, List<string>> AliasMap = new()
    {
        ["PartNo"] = new() { "编号", "零件号", "配件编号", "件号", "货号", "编码", "partno", "零件编号", "图号" },
        ["PartName"] = new() { "名称", "品名", "配件名称", "零件名称", "商品名称", "name", "品名描述" },
        ["Amount"] = new() { "数量", "qty", "amount", "采购数量", "订货数量", "进货数量" },
        ["InPrice"] = new() { "进价", "单价", "采购价", "成本价", "price", "入库价", "含税价" },
        ["Cartype"] = new() { "车型", "适用车型", "车系", "cartype", "适配车型" },
        ["Place"] = new() { "仓位", "库位", "位置", "place", "货架" },
        ["Memo"] = new() { "备注", "说明", "memo", "描述", "备注说明" }
    };

    public ExcelImportDialog()
    {
        InitializeComponent();
        ShowLoading("正在加载配件数据...");
        _ = LoadPartsAsync();
    }

    private async System.Threading.Tasks.Task LoadPartsAsync()
    {
        try
        {
            var repo = App.ServiceProvider.GetRequiredService<IPartRepository>();
            _allParts = (await repo.GetAllAsync()).ToList();
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "加载配件列表失败");
        }
        finally
        {
            HideLoading();
        }
    }

    private void ShowLoading(string message)
    {
        txtLoading.Text = message;
        loadingOverlay.Visibility = Visibility.Visible;
    }

    private void HideLoading()
    {
        loadingOverlay.Visibility = Visibility.Collapsed;
    }

    private async void BtnBrowse_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Excel文件|*.xls;*.xlsx|所有文件|*.*",
            Title = "选择采购清单文件"
        };
        if (dlg.ShowDialog() != true) return;

        txtFilePath.Text = dlg.FileName;
        ShowLoading("正在解析文件...");
        try
        {
            var parser = new ExcelParserService();
            // 异常在 Task.Run 内部处理，绝不逃逸到线程池
            Exception? parseError = null;
            _parseResult = await System.Threading.Tasks.Task.Run(() =>
            {
                try { return parser.Parse(dlg.FileName); }
                catch (System.IO.IOException) { parseError = new System.IO.IOException("FILE_BUSY"); return new ExcelParseResult(); }
                catch (Exception ex) { parseError = ex; return new ExcelParseResult(); }
            });

            if (parseError != null)
            {
                if (parseError is System.IO.IOException ioEx && ioEx.Message == "FILE_BUSY")
                    MessageBox.Show($"文件被占用，无法读取。\n\n请关闭正在使用该文件的程序（如 Excel）后重新导入。\n\n文件: {dlg.FileName}",
                        "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                else
                    MessageBox.Show($"解析文件失败:\n{parseError.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            cmbSheet.Items.Clear();
            foreach (var name in _parseResult.SheetNames)
                cmbSheet.Items.Add(name);
            if (cmbSheet.Items.Count > 0)
                cmbSheet.SelectedIndex = 0;

            RefreshMappingAndPreview();
        }
        finally
        {
            HideLoading();
        }
    }

    private async void CmbSheet_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_parseResult == null || cmbSheet.SelectedIndex < 0) return;
        ShowLoading("正在重新解析...");
        try
        {
            var parser = new ExcelParserService();
            var startRow = int.TryParse(txtStartRow.Text, out var sr) ? sr : 2;
            var filePath = txtFilePath.Text;
            var sheetIndex = cmbSheet.SelectedIndex;
            Exception? parseError = null;
            _parseResult = await System.Threading.Tasks.Task.Run(() =>
            {
                try { return parser.Parse(filePath, sheetIndex, 0, startRow - 1); }
                catch (Exception ex) { parseError = ex; return new ExcelParseResult(); }
            });
            if (parseError != null) return;
            RefreshMappingAndPreview();
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "解析Excel文件失败");
        }
        finally
        {
            HideLoading();
        }
    }

    private async void TxtStartRow_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_parseResult == null || cmbSheet.SelectedIndex < 0) return;
        ShowLoading("正在重新解析...");
        try
        {
            var parser = new ExcelParserService();
            var startRow = int.TryParse(txtStartRow.Text, out var sr) ? sr : 2;
            var filePath = txtFilePath.Text;
            var sheetIndex = cmbSheet.SelectedIndex;
            Exception? parseError = null;
            _parseResult = await System.Threading.Tasks.Task.Run(() =>
            {
                try { return parser.Parse(filePath, sheetIndex, 0, startRow - 1); }
                catch (Exception ex) { parseError = ex; return new ExcelParseResult(); }
            });
            if (parseError != null) return;
            RefreshMappingAndPreview();
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "重新解析Excel文件失败");
        }
        finally
        {
            HideLoading();
        }
    }

    private void RefreshMappingAndPreview()
    {
        if (_parseResult == null) return;

        var headers = _parseResult.Headers;
        var comboBoxes = new[] { mapPartNo, mapPartName, mapAmount, mapInPrice, mapCartype, mapPlace, mapMemo };
        var fieldKeys = new[] { "PartNo", "PartName", "Amount", "InPrice", "Cartype", "Place", "Memo" };

        for (int i = 0; i < comboBoxes.Length; i++)
        {
            var cb = comboBoxes[i];
            cb.Items.Clear();
            cb.Items.Add("(不导入)");
            foreach (var h in headers)
                cb.Items.Add(h);

            var autoIdx = AutoMatchHeader(headers, fieldKeys[i]);
            cb.SelectedIndex = autoIdx >= 0 ? autoIdx + 1 : 0;
        }

        RefreshPreview();
    }

    private int AutoMatchHeader(List<string> headers, string fieldKey)
    {
        if (!AliasMap.TryGetValue(fieldKey, out var aliases)) return -1;

        for (int i = 0; i < headers.Count; i++)
        {
            var h = headers[i].Trim();
            foreach (var alias in aliases)
            {
                if (h.Equals(alias, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
        }

        for (int i = 0; i < headers.Count; i++)
        {
            var h = headers[i].Trim();
            foreach (var alias in aliases)
            {
                if (h.Contains(alias, StringComparison.OrdinalIgnoreCase))
                    return i;
            }
        }

        return -1;
    }

    private void Mapping_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        RefreshPreview();
    }

    private void RefreshPreview()
    {
        if (_parseResult == null || _parseResult.Headers.Count == 0) return;

        _previewData.Clear();

        var mapPartNoIdx = mapPartNo.SelectedIndex - 1;
        var mapPartNameIdx = mapPartName.SelectedIndex - 1;
        var mapAmountIdx = mapAmount.SelectedIndex - 1;
        var mapInPriceIdx = mapInPrice.SelectedIndex - 1;
        var mapCartypeIdx = mapCartype.SelectedIndex - 1;
        var mapPlaceIdx = mapPlace.SelectedIndex - 1;
        var mapMemoIdx = mapMemo.SelectedIndex - 1;

        var seenPartNos = new Dictionary<string, int>();

        for (int r = 0; r < _parseResult.Rows.Count; r++)
        {
            var row = _parseResult.Rows[r];
            var item = new ImportRowResult { RowIndex = r + 1 };

            item.PartNo = mapPartNoIdx >= 0 && mapPartNoIdx < row.Count ? row[mapPartNoIdx] : null;
            item.PartName = mapPartNameIdx >= 0 && mapPartNameIdx < row.Count ? row[mapPartNameIdx] : null;
            item.Cartype = mapCartypeIdx >= 0 && mapCartypeIdx < row.Count ? row[mapCartypeIdx] : null;
            item.Place = mapPlaceIdx >= 0 && mapPlaceIdx < row.Count ? row[mapPlaceIdx] : null;
            item.Memo = mapMemoIdx >= 0 && mapMemoIdx < row.Count ? row[mapMemoIdx] : null;

            var amountStr = mapAmountIdx >= 0 && mapAmountIdx < row.Count ? row[mapAmountIdx] : null;
            item.Amount = decimal.TryParse(amountStr, out var amt) ? amt : 1;

            var priceStr = mapInPriceIdx >= 0 && mapInPriceIdx < row.Count ? row[mapInPriceIdx] : null;
            item.InPrice = decimal.TryParse(priceStr, out var price) ? price : 0;

            if (string.IsNullOrWhiteSpace(item.PartNo))
            {
                item.Status = ImportRowStatus.MissingRequired;
                item.StatusText = "✗ 缺少编号";
                _previewData.Add(item);
                continue;
            }

            if (item.Amount <= 0)
            {
                item.Status = ImportRowStatus.MissingRequired;
                item.StatusText = "✗ 数量无效";
                _previewData.Add(item);
                continue;
            }

            if (!string.IsNullOrEmpty(item.Place) && item.Place.Trim() == "废品仓")
            {
                item.Status = ImportRowStatus.InvalidPlace;
                item.StatusText = "✗ 废品仓";
                _previewData.Add(item);
                continue;
            }

            var partNoKey = item.PartNo.Trim().ToUpper();
            if (seenPartNos.ContainsKey(partNoKey))
            {
                item.Status = ImportRowStatus.Duplicate;
                item.IsDuplicate = true;
                seenPartNos[partNoKey]++;
                item.DuplicateCount = seenPartNos[partNoKey];
                item.StatusText = $"⚠ 重复×{item.DuplicateCount}";
                _previewData.Add(item);
                continue;
            }
            seenPartNos[partNoKey] = 1;

            var matched = _allParts.FirstOrDefault(p =>
                string.Equals(p.Partno?.Trim(), item.PartNo?.Trim(), StringComparison.OrdinalIgnoreCase));

            if (matched != null)
            {
                item.MatchedPartId = matched.Partid;
                item.MatchedLsPrice = matched.Lsprice == null ? 0m : Convert.ToDecimal(matched.Lsprice);
                item.MatchedPfPrice = matched.Pfprice == null ? 0m : Convert.ToDecimal(matched.Pfprice);
                item.OriginalName = matched.Name;

                var nameDiff = !string.IsNullOrEmpty(item.PartName)
                    && !string.IsNullOrEmpty(matched.Name)
                    && !string.Equals(item.PartName.Trim(), matched.Name.Trim(), StringComparison.OrdinalIgnoreCase);

                if (string.IsNullOrEmpty(item.PartName))
                    item.PartName = matched.Name;
                if (string.IsNullOrEmpty(item.CarName))
                    item.CarName = matched.Carname;
                if (string.IsNullOrEmpty(item.Cartype))
                    item.Cartype = matched.Cartype;
                if (string.IsNullOrEmpty(item.Unit))
                    item.Unit = matched.Unit;
                if (string.IsNullOrEmpty(item.Place))
                    item.Place = matched.Place;

                if (nameDiff)
                {
                    item.Status = ImportRowStatus.NameDiff;
                    item.StatusText = $"⚠ 名称不同(库:{matched.Name})";
                }
                else
                {
                    item.Status = ImportRowStatus.Exists;
                    item.StatusText = "✓ 已存在";
                }
            }
            else
            {
                item.Status = ImportRowStatus.NewPart;
                item.StatusText = "✗ 新配件";
            }

            _previewData.Add(item);
        }

        dgPreview.ItemsSource = null;
        dgPreview.ItemsSource = _previewData;

        var existsCount = _previewData.Count(r => r.Status == ImportRowStatus.Exists);
        var nameDiffCount = _previewData.Count(r => r.Status == ImportRowStatus.NameDiff);
        var dupCount = _previewData.Count(r => r.Status == ImportRowStatus.Duplicate);
        var newCount = _previewData.Count(r => r.Status == ImportRowStatus.NewPart);
        var invalidCount = _previewData.Count(r => r.Status == ImportRowStatus.InvalidPlace);
        var missingCount = _previewData.Count(r => r.Status == ImportRowStatus.MissingRequired);

        txtStats.Text = $"总计 {_previewData.Count} 行 | ✓已存在 {existsCount} 个 | ⚠名称不同 {nameDiffCount} 个 | ⚠重复 {dupCount} 个 | ✗新配件 {newCount} 个(自动新增) | ✗废品仓 {invalidCount} 个 | ✗无效 {missingCount} 个";

        btnImport.IsEnabled = _previewData.Any(r => r.Status == ImportRowStatus.Exists || r.Status == ImportRowStatus.NewPart || r.Status == ImportRowStatus.Duplicate || r.Status == ImportRowStatus.NameDiff);
    }

    private void DgPreview_LoadingRow(object sender, DataGridRowEventArgs e)
    {
        if (e.Row.Item is not ImportRowResult item) return;
        switch (item.Status)
        {
            case ImportRowStatus.Exists:
                e.Row.Background = new SolidColorBrush(Color.FromArgb(30, 0, 200, 0));
                break;
            case ImportRowStatus.NameDiff:
                e.Row.Background = new SolidColorBrush(Color.FromArgb(40, 255, 200, 0));
                break;
            case ImportRowStatus.NewPart:
                e.Row.Background = new SolidColorBrush(Color.FromArgb(40, 0, 100, 255));
                break;
            case ImportRowStatus.Duplicate:
                e.Row.Background = new SolidColorBrush(Color.FromArgb(40, 255, 165, 0));
                break;
            case ImportRowStatus.InvalidPlace:
                e.Row.Background = new SolidColorBrush(Color.FromArgb(50, 255, 0, 0));
                break;
            case ImportRowStatus.MissingRequired:
                e.Row.Background = new SolidColorBrush(Color.FromArgb(50, 255, 0, 0));
                break;
        }
    }

    private void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        var validItems = _previewData
            .Where(r => r.Status == ImportRowStatus.Exists || r.Status == ImportRowStatus.NewPart || r.Status == ImportRowStatus.NameDiff)
            .ToList();

        var duplicates = _previewData.Where(r => r.Status == ImportRowStatus.Duplicate).ToList();

        if (chkMerge.IsChecked == true && duplicates.Count > 0)
        {
            foreach (var dup in duplicates)
            {
                var existing = validItems.FirstOrDefault(v =>
                    string.Equals(v.PartNo?.Trim(), dup.PartNo?.Trim(), StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.Amount += dup.Amount;
                }
                else
                {
                    dup.Status = ImportRowStatus.Exists;
                    dup.StatusText = "✓ 已存在";
                    dup.IsDuplicate = false;
                    validItems.Add(dup);
                }
            }
        }
        else if (duplicates.Count > 0)
        {
            foreach (var dup in duplicates)
            {
                dup.Status = ImportRowStatus.Exists;
                dup.StatusText = "✓ 已存在";
                dup.IsDuplicate = false;
                validItems.Add(dup);
            }
        }

        var updateName = chkUpdateName.IsChecked == true;
        foreach (var item in validItems.Where(r => r.Status == ImportRowStatus.NameDiff))
        {
            if (updateName)
            {
                item.StatusText = "✓ 将更新名称";
            }
            else
            {
                item.PartName = item.OriginalName;
                item.StatusText = "✓ 保持原名称";
            }
        }

        var invalidItems = _previewData.Where(r =>
            r.Status == ImportRowStatus.InvalidPlace || r.Status == ImportRowStatus.MissingRequired).ToList();

        var nameDiffCount = validItems.Count(r => r.Status == ImportRowStatus.NameDiff);
        if (invalidItems.Count > 0 || nameDiffCount > 0)
        {
            var msg = "";
            if (nameDiffCount > 0)
            {
                msg += updateName
                    ? $"有 {nameDiffCount} 条配件编号相同但名称不同，将用表格名称更新数据库。\n\n"
                    : $"有 {nameDiffCount} 条配件编号相同但名称不同，将保持数据库原有名称。\n\n";
            }
            if (invalidItems.Count > 0)
            {
                msg += $"有 {invalidItems.Count} 行数据无效（废品仓或缺少必填字段），将跳过这些行。\n\n";
            }
            msg += $"确认导入 {validItems.Count} 条有效数据？";
            if (MessageBox.Show(msg, "确认", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;
        }

        ImportedDetails = validItems.Select(r => new BuyDetailItem
        {
            PartId = r.MatchedPartId ?? 0,
            PartNo = r.PartNo,
            PartName = r.PartName,
            CarName = r.CarName ?? "",
            Cartype = r.Cartype,
            Unit = r.Unit ?? "",
            Amount = r.Amount,
            InPrice = r.InPrice,
            LsPrice = r.MatchedLsPrice ?? 0m,
            PfPrice = r.MatchedPfPrice ?? 0m,
            Place = r.Place ?? "",
            Memo = r.Memo ?? ""
        }).ToList();

        UpdatePartNames = updateName;
        NameDiffItems = validItems
            .Where(r => r.Status == ImportRowStatus.NameDiff && r.MatchedPartId.HasValue)
            .Select(r => new NameDiffUpdate { PartId = r.MatchedPartId!.Value, NewName = r.PartName?.Trim() })
            .Where(x => !string.IsNullOrEmpty(x.NewName))
            .ToList();

        ImportConfirmed = true;
        DialogResult = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }
}

public class NameDiffUpdate
{
    public long PartId { get; set; }
    public string? NewName { get; set; }
}
