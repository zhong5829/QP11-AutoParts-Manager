using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Interfaces;
using QP11.Wpf.Services;

namespace QP11.Wpf.Views;

public class PrintColumnViewModel : INotifyPropertyChanged, IPrintColumn
{
    public PrintColumnConfig Source { get; }
    public PrintColumnViewModel(PrintColumnConfig source) => Source = source;

    public string Key { get => Source.Key; set => Source.Key = value; }
    public string Header { get => Source.Header; set { Source.Header = value; OnPropertyChanged(nameof(Header)); } }
    public double Width { get => Source.Width; set { Source.Width = value; OnPropertyChanged(nameof(Width)); } }
    public bool Visible { get => Source.Visible; set { Source.Visible = value; OnPropertyChanged(nameof(Visible)); } }
    public int Order { get => Source.Order; set { Source.Order = value; OnPropertyChanged(nameof(Order)); } }
    public string? DataField { get => Source.DataField; set => Source.DataField = value; }
    public string? Format { get => Source.Format; set => Source.Format = value; }
    public string? Alignment { get => Source.Alignment; set => Source.Alignment = value; }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class BillPrintSettingsWindow : Window
{
    private PrintSettings _settings = new();
    private ObservableCollection<PrintColumnViewModel> _columns = new();
    private bool _loading;

    public BillPrintSettingsWindow()
    {
        _loading = true;
        InitializeComponent();
        LoadSettings();
    }

    private string CurrentBillType => (cboBillType.SelectedItem as ComboBoxItem)?.Content.ToString() switch
    {
        "采购单" => "采购",
        "退货单" => "退货",
        _ => "销售"
    };

    private void LoadSettings()
    {
        _loading = true;
        _settings = PrintSettingsService.Load();
        var bill = _settings.BillPrint;
        var page = _settings.PagePrint;

        rbStyle1.IsChecked = bill.PrintStyle == 1;
        rbStyle2.IsChecked = bill.PrintStyle == 2;
        rbStyle3.IsChecked = bill.PrintStyle == 3;

        var height = bill.PrintStyle switch
        {
            2 => bill.PrintHeight2,
            3 => bill.PrintHeight3,
            _ => bill.PrintHeight1
        };
        txtHeight.Text = height.ToString();

        txtMarginTop.Text = page.MarginTop.ToString();
        txtMarginBottom.Text = page.MarginBottom.ToString();
        txtMarginLeft.Text = page.MarginLeft.ToString();
        txtMarginRight.Text = page.MarginRight.ToString();

        txtDeclaration.Text = bill.Declaration;
        txtAdText.Text = bill.AdText;

        LoadColumns();
        _loading = false;
        UpdatePreview();
    }

    private void LoadColumns()
    {
        _columns.Clear();
        var cols = _settings.BillPrint.GetColumns(CurrentBillType)
            .OrderBy(c => c.Order)
            .Select(c => new PrintColumnViewModel(c));
        foreach (var col in cols)
        {
            col.PropertyChanged += Column_PropertyChanged;
            _columns.Add(col);
        }
        dgColumns.ItemsSource = _columns;
    }

    private void SaveCurrentColumns()
    {
        if (_settings?.BillPrint == null) return;
        var list = _columns.Select(c => c.Source).ToList();
        for (int i = 0; i < list.Count; i++)
            list[i].Order = i;
        _settings.BillPrint.SetColumns(CurrentBillType, list);
    }

    #region 事件处理

    private void BillType_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_loading) return;
        SaveCurrentColumns();
        LoadColumns();
        UpdatePreview();
    }

    private void Setting_Changed(object sender, RoutedEventArgs e)
    {
        if (_loading) return;
        var bill = _settings.BillPrint;
        bill.PrintStyle = rbStyle1.IsChecked == true ? 1 : rbStyle2.IsChecked == true ? 2 : 3;
        var height = bill.PrintStyle switch
        {
            2 => bill.PrintHeight2,
            3 => bill.PrintHeight3,
            _ => bill.PrintHeight1
        };
        _loading = true;
        txtHeight.Text = height.ToString();
        _loading = false;
        UpdatePreview();
    }

    private void Height_Changed(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        if (double.TryParse(txtHeight.Text, out var h) && h >= 0 && h <= 999)
        {
            var bill = _settings.BillPrint;
            switch (bill.PrintStyle)
            {
                case 1: bill.PrintHeight1 = h; break;
                case 2: bill.PrintHeight2 = h; break;
                case 3: bill.PrintHeight3 = h; break;
            }
            UpdatePreview();
        }
    }

    private void Margin_Changed(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        var page = _settings.PagePrint;
        if (double.TryParse(txtMarginTop.Text, out var mt)) page.MarginTop = mt;
        if (double.TryParse(txtMarginBottom.Text, out var mb)) page.MarginBottom = mb;
        if (double.TryParse(txtMarginLeft.Text, out var ml)) page.MarginLeft = ml;
        if (double.TryParse(txtMarginRight.Text, out var mr)) page.MarginRight = mr;
        UpdatePreview();
    }

    private void Declaration_Changed(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        _settings.BillPrint.Declaration = txtDeclaration.Text;
        UpdatePreview();
    }

    private void AdText_Changed(object sender, TextChangedEventArgs e)
    {
        if (_loading) return;
        _settings.BillPrint.AdText = txtAdText.Text;
        UpdatePreview();
    }

    private void Column_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (_loading) return;
        if (e.PropertyName is nameof(PrintColumnViewModel.Visible) or nameof(PrintColumnViewModel.Header) or nameof(PrintColumnViewModel.Width))
            UpdatePreview();
    }

    private void BtnMoveColumn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        var idx = dgColumns.SelectedIndex;
        if (idx < 0) return;
        var direction = btn.Tag?.ToString();
        if (direction == "up" && idx > 0)
        {
            _columns.Move(idx, idx - 1);
        }
        else if (direction == "down" && idx < _columns.Count - 1)
        {
            _columns.Move(idx, idx + 1);
        }
        // 移动后重新按集合顺序赋值Order，确保BuildCore按正确顺序渲染
        for (int i = 0; i < _columns.Count; i++)
            _columns[i].Source.Order = i;
        UpdatePreview();
    }

    private void BtnAddColumn_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new AddPrintColumnWindow { Owner = this };
        if (dlg.ShowDialog() == true)
        {
            var config = dlg.ResultConfig;
            config.Order = _columns.Count;
            _columns.Add(new PrintColumnViewModel(config));
            UpdatePreview();
        }
    }

    private void BtnDeleteColumn_Click(object sender, RoutedEventArgs e)
    {
        if (dgColumns.SelectedIndex < 0) return;
        _columns.RemoveAt(dgColumns.SelectedIndex);
        UpdatePreview();
    }

    private void BtnResetColumns_Click(object sender, RoutedEventArgs e)
    {
        var defaults = BillPrintSettings.DefaultColumns();
        _settings.BillPrint.SetColumns(CurrentBillType, defaults);
        LoadColumns();
        UpdatePreview();
    }

    private void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        SaveCurrentColumns();
        PrintSettingsService.Save(_settings);
        MessageBox.Show("打印设置已保存", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    #endregion

    #region 实时预览

    private void UpdatePreview()
    {
        if (_loading || _settings == null) return;
        var billData = CreateSampleBillData();
        var doc = BillDocumentBuilder.Build(billData, _columns.ToList(), _settings);
        docPreview.Document = doc;
    }

    private BillPrintData CreateSampleBillData()
    {
        var billType = CurrentBillType;
        // 从数据库加载公司抬头名称
        string companyName = "";
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = dbFactory.Create();
            db.Open();
            var row = db.QueryFirstOrDefault<dynamic>("SELECT TOP 1 qc FROM business_infor");
            companyName = row?.qc?.ToString()?.Trim() ?? "";
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "查询公司名称失败");
        }

        return billType switch
        {
            "采购" => new BillPrintData
            {
                BillType = "采购", Sn = "CG20240601001", DateText = "2024-06-01",
                CompanyName = companyName,
                PartnerName = "示例供应商", PartnerPhone = "021-12345678",
                PartnerContact = "赵六", PartnerAddress = "供应商路88号",
                WorkerName = "李四", CompanyAddress = "某某路100号", CompanyPhone = "0571-88888888",
                TotalAmount = 2350.00m, Cash = 1000, Weixin = 1350,
                Items = new()
                {
                    new() { Index=1, PartNo="P001", PartName="刹车片(前)", Cartype="大众朗逸", Unit="副", Price=250, Amount=4, Subtotal=1000, Place="A-01", Area="1号库", Brand="博世", DiscountRate=0 },
                    new() { Index=2, PartNo="P002", PartName="机油滤清器", Cartype="丰田卡罗拉", Unit="个", Price=35, Amount=10, Subtotal=350, Place="B-03", Area="2号库", Brand="曼牌", DiscountRate=95 },
                    new() { Index=3, PartNo="P003", PartName="空气滤芯", Cartype="本田思域", Unit="个", Price=50, Amount=20, Subtotal=1000, Place="C-02", Area="1号库", Brand="马勒", DiscountRate=0 },
                }
            },
            "退货" => new BillPrintData
            {
                BillType = "退货", Sn = "TH20240601001", DateText = "2024-06-01",
                CompanyName = companyName,
                PartnerName = "示例客户", PartnerPhone = "13800138000",
                PartnerContact = "钱七", PartnerAddress = "客户路66号",
                WorkerName = "王五", CompanyAddress = "某某路100号", CompanyPhone = "0571-88888888",
                TotalAmount = -500m, Cash = 500,
                Items = new()
                {
                    new() { Index=1, PartNo="P004", PartName="雨刮片", Cartype="大众朗逸", Unit="对", Price=120, Amount=2, Subtotal=240, Place="D-01" },
                    new() { Index=2, PartNo="P005", PartName="空调滤芯", Cartype="丰田卡罗拉", Unit="个", Price=65, Amount=4, Subtotal=260, Place="E-02" },
                }
            },
            _ => new BillPrintData
            {
                BillType = "销售", Sn = "XS20240601001", DateText = "2024-06-01",
                CompanyName = companyName,
                PartnerName = "示例客户", PartnerPhone = "13800138000",
                PartnerContact = "孙八", PartnerAddress = "客户路99号",
                WorkerName = "张三", CompanyAddress = "某某路100号", CompanyPhone = "0571-88888888",
                TotalAmount = 1250.00m, Cash = 500, Weixin = 750,
                Items = new()
                {
                    new() { Index=1, PartNo="P001", PartName="刹车片(前)", Cartype="大众朗逸", Unit="副", Price=250, Amount=2, Subtotal=500, Place="A-01", Area="1号库", Brand="博世", DiscountRate=0 },
                    new() { Index=2, PartNo="P002", PartName="机油滤清器", Cartype="丰田卡罗拉", Unit="个", Price=35, Amount=5, Subtotal=175, Place="B-03", Area="2号库", Brand="曼牌", DiscountRate=95 },
                    new() { Index=3, PartNo="P003", PartName="空气滤芯", Cartype="本田思域", Unit="个", Price=50, Amount=3, Subtotal=150, Place="C-02", Brand="马勒", DiscountRate=0 },
                    new() { Index=4, PartNo="P006", PartName="火花塞", Cartype="大众朗逸", Unit="支", Price=35, Amount=12, Subtotal=420, Place="F-01", Area="1号库", Brand="NGK", DiscountRate=90 },
                }
            }
        };
    }

    #endregion
}

/// <summary>
/// 单据 FlowDocument 构建器，供设置窗口和打印预览窗口共用
/// </summary>
public static class BillDocumentBuilder
{
    public static FlowDocument Build(BillPrintData data, List<PrintColumnViewModel> columns, PrintSettings settings,
        bool addLabelActionColumn = false, Action<BillPrintItem>? onLabelAction = null)
    {
        var colConfigs = columns.Where(c => c.Visible).OrderBy(c => c.Order).Cast<IPrintColumn>().ToList();
        return BuildCore(data, colConfigs, settings, addLabelActionColumn, onLabelAction);
    }

    public static FlowDocument Build(BillPrintData data, List<PrintColumnConfig> columns, PrintSettings settings,
        bool addLabelActionColumn = false, Action<BillPrintItem>? onLabelAction = null)
    {
        var colConfigs = columns.Where(c => c.Visible).OrderBy(c => c.Order).Select(c => new PrintColumnConfigAdapter(c)).Cast<IPrintColumn>().ToList();
        return BuildCore(data, colConfigs, settings, addLabelActionColumn, onLabelAction);
    }

    private static FlowDocument BuildCore(BillPrintData data, List<IPrintColumn> columns, PrintSettings settings,
        bool addLabelActionColumn = false, Action<BillPrintItem>? onLabelAction = null)
    {
        var bill = settings.BillPrint;
        var page = settings.PagePrint;
        const double mmToDiu = 96.0 / 25.4;

        // 根据纸张尺寸设置页面宽高
        var (pw, ph) = ResolvePaperSize(page.PaperSize);

        var style = bill.PrintStyle;
        var fontSize = style == 1 ? 12 : 10;
        var smallSize = fontSize - 1;
        var titleSize = style == 1 ? 28 : 26;

        var doc = new FlowDocument
        {
            FontFamily = new FontFamily("SimSun"),
            FontSize = fontSize,
            PagePadding = new Thickness(
                page.MarginLeft * mmToDiu,
                page.MarginTop * mmToDiu,
                page.MarginRight * mmToDiu,
                page.MarginBottom * mmToDiu),
            ColumnWidth = 99999
        };

        doc.PageWidth = pw * mmToDiu;

        var printHeight = style switch
        {
            2 => bill.PrintHeight2,
            3 => bill.PrintHeight3,
            _ => bill.PrintHeight1
        };

        if (page.PaperSize == "241x93")
        {
            // 连续纸：根据内容动态计算页面高度，避免配件多时强制分页导致下方空白
            double headerMm = 35;   // 表头区域（公司名+地址+客户信息+分隔线+表头行）
            double rowMm = style == 1 ? 5.5 : 4.5;  // 每行数据高度
            double footerMm = 25;   // 表尾区域（制单+付款方式+声明+备注）
            double contentMm = headerMm + (data.Items.Count * rowMm) + footerMm;
            double totalMm = contentMm + page.MarginTop + page.MarginBottom;
            double minHeight = printHeight > 0 ? printHeight : ph;
            doc.PageHeight = Math.Max(minHeight, totalMm) * mmToDiu;
        }
        else
        {
            // 固定纸张：使用配置的打印高度
            if (printHeight > 0)
                doc.PageHeight = Math.Min(printHeight, ph) * mmToDiu;
        }

        // 分隔线辅助方法（极细）
        Paragraph MakeSeparator() => new(new Run(new string('─', 130)))
        {
            FontSize = 6,
            Foreground = Brushes.Black,
            Margin = new Thickness(0),
            LineHeight = 1
        };

        // 辅助：创建紧凑段落（统一 LineHeight=1, Margin=0）
        Paragraph MakePara(string text, double fs = 0, TextAlignment align = TextAlignment.Left, bool bold = false) => new(new Run(text))
        {
            FontSize = fs > 0 ? fs : fontSize,
            FontWeight = bold ? FontWeights.Bold : FontWeights.Normal,
            TextAlignment = align,
            Margin = new Thickness(0),
            LineHeight = 1
        };

        // ── 第1行：公司抬头 + 清单标题 ──
        var isReturnBill = data.Sn?.StartsWith("TH") == true || data.TotalAmount < 0;
        var billTitle = data.BillType switch
        {
            "采购" when isReturnBill => "采退清单",
            _ when isReturnBill => "退货清单",
            "采购" => "采购清单",
            _ => "销售清单"
        };
        var headerText = (data.CompanyName?.Trim() ?? "") + billTitle;
        doc.Blocks.Add(new BlockUIContainer(new System.Windows.Controls.TextBlock
        {
            Text = headerText,
            FontFamily = new FontFamily("SimSun"),
            FontSize = titleSize,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Margin = new Thickness(0)
        }));

        // ── 抬头下方广告文字（用户自定义，加粗加大） ──
        var adText = bill.AdText?.Trim() ?? "";
        if (!string.IsNullOrEmpty(adText))
        {
            var resolvedAd = adText.Replace("{Phone}", data.CompanyPhone ?? "")
                                    .Replace("{Address}", data.CompanyAddress ?? "");
            doc.Blocks.Add(new Paragraph(new Run(resolvedAd))
            {
                FontSize = 24,
                FontWeight = FontWeights.Bold,
                TextAlignment = TextAlignment.Center,
                Foreground = Brushes.Black,
                Margin = new Thickness(0),
                LineHeight = 1
            });
        }

        // ── 分隔线 ──
        doc.Blocks.Add(MakeSeparator());

        // ── 购货单位信息（紧凑两行） ──
        var partnerLabel = data.BillType == "采购" ? "供货单位" : "购货单位";
        doc.Blocks.Add(MakePara(
            partnerLabel + ":" + (data.PartnerName ?? "") +
            "    购货日期:" + (data.DateText ?? "") +
            "    单号:" + (data.Sn ?? ""),
            fontSize + 2));
        doc.Blocks.Add(MakePara(
            "地址:" + (data.PartnerAddress ?? "") +
            "    电话:" + (data.PartnerPhone ?? "") +
            "    打印时间:" + DateTime.Now.ToString("yyyy-MM-dd HH:mm"),
            smallSize + 2));

        // ── 分隔线 ──
        doc.Blocks.Add(MakeSeparator());

        // ── 明细表格 ──
        var table = new Table { CellSpacing = 0, BorderBrush = Brushes.Black, BorderThickness = new Thickness(0.5), Margin = new Thickness(0) };
        foreach (var col in columns)
            table.Columns.Add(new TableColumn { Width = new GridLength(col.Width) });

        // 标签打印操作列（仅预览模式添加，打印前由 PrintPreviewWindow 隐藏；列打 Tag 便于定位）
        if (addLabelActionColumn)
            table.Columns.Add(new TableColumn { Width = new GridLength(58), Tag = "label-action-col" });

        var rowGroup = new TableRowGroup();

        // 表头
        var headerRow = new TableRow();
        foreach (var col in columns)
        {
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run(col.Header))
            {
                FontWeight = FontWeights.Bold,
                FontSize = fontSize + 2,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(0.5),
                Padding = new Thickness(2, 0, 2, 0),
                TextAlignment = col.Alignment == "Right" ? TextAlignment.Right :
                                col.Alignment == "Center" ? TextAlignment.Center : TextAlignment.Left,
                Margin = new Thickness(0),
                LineHeight = 1
            }));
        }
        if (addLabelActionColumn)
        {
            headerRow.Cells.Add(new TableCell(new Paragraph(new Run("标签打印"))
            {
                FontWeight = FontWeights.Bold,
                FontSize = fontSize,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(0.5),
                Padding = new Thickness(2, 0, 2, 0),
                TextAlignment = TextAlignment.Center,
                Margin = new Thickness(0),
                LineHeight = 1
            }));
        }
        rowGroup.Rows.Add(headerRow);

        // 数据行（紧凑高度）
        var dataRowHeight = fontSize * 1.6;
        foreach (var item in data.Items)
        {
            var row = new TableRow();
            foreach (var col in columns)
            {
                var cellValue = GetCellValue(item, col, isReturnBill);
                var txt = new System.Windows.Controls.TextBlock
                {
                    Text = cellValue,
                    FontFamily = new FontFamily("SimSun"),
                    FontSize = fontSize + 3,
                    Height = dataRowHeight,
                    TextTrimming = System.Windows.TextTrimming.CharacterEllipsis,
                    TextWrapping = System.Windows.TextWrapping.NoWrap,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    Padding = new Thickness(1, 0, 1, 0),
                };
                if (col.Alignment == "Right")
                    txt.TextAlignment = System.Windows.TextAlignment.Right;
                else if (col.Alignment == "Center")
                    txt.TextAlignment = System.Windows.TextAlignment.Center;

                row.Cells.Add(new TableCell(new BlockUIContainer(txt))
                {
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(0.5),
                    Padding = new Thickness(0)
                });
            }

            // 标签打印按钮（预览交互；单元格打 Tag 供打印前隐藏）
            if (addLabelActionColumn)
            {
                var btn = new System.Windows.Controls.Button
                {
                    Content = "标签打印",
                    FontSize = fontSize - 1,
                    Padding = new Thickness(2, 0, 2, 0),
                    Margin = new Thickness(2, 0, 2, 0),
                    Tag = item
                };
                if (onLabelAction != null)
                    btn.Click += (_, __) => onLabelAction(item);
                row.Cells.Add(new TableCell(new BlockUIContainer(btn))
                {
                    BorderBrush = Brushes.Black,
                    BorderThickness = new Thickness(0.5),
                    Padding = new Thickness(0),
                    Tag = "label-action-cell"
                });
            }
            rowGroup.Rows.Add(row);
        }
        table.RowGroups.Add(rowGroup);
        doc.Blocks.Add(table);

        // ── 分隔线 ──
        doc.Blocks.Add(MakeSeparator());

        // ── 付款方式 + 合计 + 制单（同一行横排） ──
        var totalAmt = Math.Abs(data.TotalAmount);
        var totalQty = data.Items.Sum(i => Math.Abs(i.Amount));
        // 退货单：合计显示负数
        var amtPrefix = isReturnBill ? "-" : "";
        var qtyPrefix = isReturnBill ? "-" : "";
        var paymentParts = new List<string>();
        if (data.Cash > 0) paymentParts.Add("现金:" + data.Cash.ToString("N2"));
        if (data.Weixin > 0) paymentParts.Add("微信:" + data.Weixin.ToString("N2"));
        if (data.Zhifubao > 0) paymentParts.Add("支付宝:" + data.Zhifubao.ToString("N2"));
        if (data.Arrearage > 0) paymentParts.Add("欠款:" + data.Arrearage.ToString("N2"));
        var paymentText = paymentParts.Count > 0 ? string.Join(" ", paymentParts) : "未付款";
        var deliveryText = data.DeliveryMethod ?? "自提";

        doc.Blocks.Add(MakePara(
            "人民币大写：" + AmountToChinese(totalAmt) +
            "    【付款方式：" + paymentText + "】【发货方式：" + deliveryText + "】    " +
            "合计：" + qtyPrefix + totalQty + "件 ￥" + amtPrefix + totalAmt.ToString("N2") +
            "    制单:" + (data.WorkerName ?? ""),
            smallSize + 2));

        // ── 特别声明 ──
        if (!string.IsNullOrWhiteSpace(bill.Declaration))
        {
            doc.Blocks.Add(new Paragraph(new Run(bill.Declaration))
            {
                FontSize = smallSize,
                Foreground = Brushes.Gray,
                Margin = new Thickness(0),
                LineHeight = 1
            });
        }

        // ── 备注（有则显示，加粗线方框包裹） ──
        if (!string.IsNullOrWhiteSpace(data.Memo))
        {
            var memoPara = new Paragraph(new Run("备注：" + data.Memo))
            {
                FontSize = fontSize + 2,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 2, 0, 0),
                LineHeight = 1,
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(2),
                Padding = new Thickness(4, 2, 4, 2)
            };
            doc.Blocks.Add(memoPara);
        }

        return doc;
    }

    private static string GetCellValue(BillPrintItem item, IPrintColumn col, bool isReturn)
    {
        var field = col.DataField ?? col.Key;
        var format = col.Format;

        // 退货单：出库数、售价、金额类字段显示负数
        var neg = isReturn ? "-" : "";

        return field switch
        {
            "Index" or "index" => item.Index.ToString(),
            "PartNo" or "partno" => item.PartNo ?? "",
            "PartName" or "name" => item.PartName ?? "",
            "Cartype" or "cartype" => item.Cartype ?? "",
            "Price" or "price" => item.Price > 0 ? (format != null ? neg + item.Price.ToString(format) : neg + item.Price.ToString("N2")) : "",
            "PfPrice" or "pfprice" => item.PfPrice > 0 ? (format != null ? neg + item.PfPrice.ToString(format) : neg + item.PfPrice.ToString("N2")) : "",
            "BillPrice" or "billprice" => item.BillPrice > 0 ? (format != null ? neg + item.BillPrice.ToString(format) : neg + item.BillPrice.ToString("N2")) : "",
            "Unit" or "unit" => item.Unit ?? "",
            "Amount" or "amount" => isReturn ? ("-" + Math.Abs(item.Amount).ToString()) : Math.Abs(item.Amount).ToString(),
            "Subtotal" or "subtotal" => isReturn ? ("-" + Math.Abs(item.Subtotal).ToString(format ?? "N2")) : Math.Abs(item.Subtotal).ToString(format ?? "N2"),
            "Place" or "place" => item.Place ?? "",
            "Area" or "area" => item.Area ?? "",
            "Brand" or "brand" => item.Brand ?? "",
            "DiscountRate" or "discount" => item.DiscountRate > 0 ? item.DiscountRate.ToString("N0") + "%" : "",
            "Memo" or "memo" => item.Memo ?? "",
            _ => ""
        };
    }

    private static string AmountToChinese(decimal amount)
    {
        if (amount == 0) return "零元整";
        amount = Math.Abs(amount);

        var digits = new[] { "零", "壹", "贰", "叁", "肆", "伍", "陆", "柒", "捌", "玖" };
        var smallUnits = new[] { "", "拾", "佰", "仟" };
        var bigUnits = new[] { "", "万", "亿", "兆", "京" };

        var integral = (long)Math.Truncate(amount);
        var decimalPart = (int)Math.Round((decimal)(amount - integral) * 100);

        var result = "";

        if (integral == 0)
        {
            result = "零";
        }
        else
        {
            var str = integral.ToString();
            var len = str.Length;
            var zeroFlag = false;

            for (var i = 0; i < len; i++)
            {
                var digit = str[i] - '0';
                var pos = len - i - 1;
                var bigUnitIdx = pos / 4;
                var smallUnitIdx = pos % 4;

                if (digit == 0)
                {
                    zeroFlag = true;
                    if (smallUnitIdx == 0 && bigUnitIdx > 0)
                    {
                        var allZero = true;
                        for (var j = i + 1; j < len && j <= i + pos % 4 + 4; j++)
                        {
                            if (str[j] - '0' != 0) { allZero = false; break; }
                        }
                        if (allZero && bigUnitIdx < bigUnits.Length)
                            result += bigUnits[bigUnitIdx];
                        zeroFlag = false;
                    }
                }
                else
                {
                    if (zeroFlag) { result += "零"; zeroFlag = false; }
                    result += digits[digit] + smallUnits[smallUnitIdx];
                    if (smallUnitIdx == 0 && bigUnitIdx > 0 && bigUnitIdx < bigUnits.Length)
                        result += bigUnits[bigUnitIdx];
                }
            }
        }

        result += "元";

        if (decimalPart == 0)
        {
            result += "整";
        }
        else
        {
            var jiao = decimalPart / 10;
            var fen = decimalPart % 10;
            if (jiao > 0 && jiao < 10) result += digits[jiao] + "角";
            else if (integral > 0) result += "零";
            if (fen > 0 && fen < 10) result += digits[fen] + "分";
        }

        return result;
    }

    /// <summary>将纸张尺寸名称解析为宽高(mm)</summary>
    private static (double width, double height) ResolvePaperSize(string? paperSize)
    {
        return paperSize?.ToLowerInvariant() switch
        {
            "241x93" => (241, 93),
            "a3"     => (297, 420),
            "a4"     => (210, 297),
            "a5"     => (148, 210),
            "b4"     => (250, 353),
            "b5"     => (176, 250),
            "letter" => (216, 279),
            "legal"  => (216, 356),
            _        => (210, 297), // 默认A4
        };
    }
}

/// <summary>打印列接口，统一 ViewModel 和 Config</summary>
public interface IPrintColumn
{
    string Key { get; }
    string Header { get; }
    double Width { get; }
    int Order { get; }
    string? DataField { get; }
    string? Format { get; }
    string? Alignment { get; }
}

public class PrintColumnConfigAdapter : IPrintColumn
{
    private readonly PrintColumnConfig _c;
    public PrintColumnConfigAdapter(PrintColumnConfig c) => _c = c;
    public string Key => _c.Key;
    public string Header => _c.Header;
    public double Width => _c.Width;
    public int Order => _c.Order;
    public string? DataField => _c.DataField;
    public string? Format => _c.Format;
    public string? Alignment => _c.Alignment;
}
