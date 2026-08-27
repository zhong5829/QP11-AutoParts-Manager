using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

public class BarcodePartItem
{
    public long Partid { get; set; }
    public string? Partno { get; set; }
    public string? Name { get; set; }
    public decimal? Lsprice { get; set; }
    public bool IsSelected { get; set; }
}

public class BarcodeLabelItem
{
    public string? Partno { get; set; }
    public string? Name { get; set; }
    public string? BarcodeText { get; set; }
}

public partial class BarcodeWindow : Window
{
    private readonly IPartRepository _partRepo;
    public ObservableCollection<BarcodePartItem> Items { get; } = new();
    public ObservableCollection<BarcodeLabelItem> Labels { get; } = new();

    public BarcodeWindow(IPartRepository partRepo)
    {
        _partRepo = partRepo;
        InitializeComponent();
        dgList.ItemsSource = Items;
        dgLabels.ItemsSource = Labels;
    }

    private async void BtnQuery_Click(object sender, RoutedEventArgs e)
    {
        var keyword = txtPartNo.Text.Trim();
        if (string.IsNullOrEmpty(keyword)) return;

        try
        {
            var parts = await _partRepo.SearchAsync(keyword);
            Items.Clear();
            foreach (var p in parts)
                Items.Add(new BarcodePartItem { Partid = p.Partid, Partno = p.Partno, Name = p.Name, Lsprice = p.Lsprice, IsSelected = true });
            txtCount.Text = $"共 {Items.Count} 条";
        }
        catch (Exception ex) { Serilog.Log.Warning(ex, "查询配件失败"); MessageBox.Show($"查询失败: {ex.Message}", "错误"); }
    }

    private void BtnGenerate_Click(object sender, RoutedEventArgs e)
    {
        var selected = Items.Where(i => i.IsSelected).ToList();
        if (selected.Count == 0) { MessageBox.Show("请选择配件", "提示"); return; }

        foreach (var item in selected)
        {
            var barcodeValue = item.Partno ?? "";
        if (!Labels.Any(l => l.Partno == item.Partno))
            {
                Labels.Add(new BarcodeLabelItem
                {
                    Partno = item.Partno,
                    Name = item.Name,
                    BarcodeText = barcodeValue
                });
            }
        }
        txtCount.Text = $"已生成 {Labels.Count} 个条码标签";
    }

    private void BtnPrint_Click(object sender, RoutedEventArgs e)
    {
        if (Labels.Count == 0) { MessageBox.Show("请先生成条码标签", "提示"); return; }

        var printDialog = new PrintDialog();
        if (printDialog.ShowDialog() != true) return;

        var doc = new FlowDocument
        {
            PageWidth = printDialog.PrintableAreaWidth,
            PageHeight = printDialog.PrintableAreaHeight,
            PagePadding = new Thickness(10),
            ColumnWidth = double.MaxValue
        };

        var table = new Table { CellSpacing = 5, BorderBrush = Brushes.Black, BorderThickness = new Thickness(1) };
        table.Columns.Add(new TableColumn { Width = new GridLength(240) });
        table.Columns.Add(new TableColumn { Width = new GridLength(240) });

        var rowGroup = new TableRowGroup();
        var currentRow = new TableRow();

        for (int i = 0; i < Labels.Count; i++)
        {
            var label = Labels[i];
            var barcodeImage = RenderCode128Barcode(label.Partno ?? "");

            var stackPanel = new StackPanel { Margin = new Thickness(2) };
            stackPanel.Children.Add(new TextBlock { Text = $"编号: {label.Partno}", FontSize = 10 });
            stackPanel.Children.Add(new TextBlock { Text = $"名称: {label.Name}", FontSize = 10 });
            stackPanel.Children.Add(new Image { Source = barcodeImage, Width = 180, Height = 50 });

            var cell = new TableCell(new BlockUIContainer(stackPanel))
            {
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(5)
            };
            currentRow.Cells.Add(cell);

            if (currentRow.Cells.Count == 2 || i == Labels.Count - 1)
            {
                while (currentRow.Cells.Count < 2)
                    currentRow.Cells.Add(new TableCell(new Paragraph(new Run(""))));
                rowGroup.Rows.Add(currentRow);
                currentRow = new TableRow();
            }
        }

        table.RowGroups.Add(rowGroup);
        doc.Blocks.Add(table);

        printDialog.PrintDocument(((IDocumentPaginatorSource)doc).DocumentPaginator, "条码标签打印");
    }

    private static RenderTargetBitmap RenderCode128Barcode(string value, double width = 200, double height = 60)
    {
        if (string.IsNullOrEmpty(value)) return new RenderTargetBitmap(1, 1, 96, 96, PixelFormats.Pbgra32);

        var patterns = new[] {
            "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213",
            "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132",
            "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211",
            "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
            "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331",
            "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111",
            "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214",
            "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
            "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141",
            "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141",
            "114131", "311141", "411131", "211412", "211214", "211232",
            "2331112"
        };

        var bars = new StringBuilder();
        bars.Append(patterns[104]); // Start Code B

        int sum = 104;
        for (int i = 0; i < value.Length; i++)
        {
            int code = value[i] >= 32 && value[i] <= 127 ? value[i] - 32 : value[i];
            bars.Append(patterns[code]);
            sum += code * (i + 1);
        }

        int checksum = sum % 103;
        bars.Append(patterns[checksum]);
        bars.Append(patterns[106]); // Stop

        var barStr = bars.ToString();
        var totalUnits = 0;
        foreach (char c in barStr) totalUnits += c - '0';

        var bmp = new RenderTargetBitmap((int)width, (int)height, 96, 96, PixelFormats.Pbgra32);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            double barWidth = width / totalUnits;
            double x = 0;
            bool isBar = true;
            foreach (char c in barStr)
            {
                int w = c - '0';
                if (isBar)
                {
                    dc.DrawRectangle(Brushes.Black, null, new Rect(x, 0, w * barWidth, height * 0.7));
                }
                x += w * barWidth;
                isBar = !isBar;
            }
        }
        bmp.Render(dv);
        bmp.Freeze();
        return bmp;
    }
}
