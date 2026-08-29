using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using QP11.Wpf.Services.LabelPrint;

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
            var barcodeImage = Code128Renderer.Render(label.Partno ?? "");

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
}
