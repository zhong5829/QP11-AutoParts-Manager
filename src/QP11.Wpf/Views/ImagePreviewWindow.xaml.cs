using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using SD = System.Drawing;
using SD2D = System.Drawing.Drawing2D;
using SDT = System.Drawing.Text;

namespace QP11.Wpf.Views;

public partial class ImagePreviewWindow : Window
{
    private readonly List<string> _tempFiles = new();
    private readonly List<BitmapSource> _displayImages = new();
    private const int MaxDisplaySize = 4096;

    public ImagePreviewWindow(DataGrid dataGrid, string title = "导出预览")
        : this(new[] { (dataGrid, title) })
    {
    }

    public ImagePreviewWindow((DataGrid dg, string title)[] grids)
    {
        InitializeComponent();
        foreach (var (dg, title) in grids)
        {
            Title = title;
            RenderDataGridToImages(dg, title);
        }
        ShowThumbnails();
        txtCount.Text = _displayImages.Count.ToString();
        txtStatus.Text = $"共 {_displayImages.Count} 张图片，点击图片可查看大图并复制";
        Closed += (s, e) => CleanupTempFiles();
    }

    private void RenderDataGridToImages(DataGrid dg, string title = "销售明细")
    {
        var columns = dg.Columns.ToList();
        if (columns.Count == 0) return;

        var headers = columns.Select(c => c.Header?.ToString() ?? "").ToArray();
        var bindings = columns.Select(c =>
        {
            if (c is DataGridTextColumn tc && tc.Binding is System.Windows.Data.Binding b)
                return b.Path.Path;
            return null;
        }).ToArray();

        var rows = new List<string[]>();
        var rowFlags = new List<int>();
        var itemsSource = dg.ItemsSource as System.Collections.IEnumerable;
        if (itemsSource != null)
        {
            foreach (var item in itemsSource)
            {
                var row = new string[columns.Count];
                for (int i = 0; i < columns.Count; i++)
                {
                    string cellValue = "";
                    
                    if (!string.IsNullOrEmpty(bindings[i]))
                    {
                        cellValue = GetPropertyValue(item, bindings[i]!) ?? "";
                    }
                    
                    if (string.IsNullOrEmpty(cellValue))
                    {
                        cellValue = TryGetDynamicValue(item, bindings[i]) ?? "";
                    }
                    
                    row[i] = cellValue;
                }
                rows.Add(row);

                var flagVal = GetPropertyValue(item, "bill_flag") ?? GetPropertyValue(item, "Flag") ?? TryGetDynamicValue(item, "bill_flag") ?? TryGetDynamicValue(item, "Flag") ?? "0";
                rowFlags.Add(int.TryParse(flagVal, out var f) ? f : 0);
            }
        }

        var colWidths = CalculateColumnWidths(columns, headers, rows);
        RenderPage(headers, colWidths, rows, 1, 1, title, rowFlags);
    }

    private double[] CalculateColumnWidths(List<DataGridColumn> columns, string[] headers, List<string[]> rows)
    {
        var widths = new double[columns.Count];
        var fontName = "Microsoft YaHei";
        var fontSize = 13.0;

        for (int i = 0; i < columns.Count; i++)
        {
            var headerWidth = MeasureTextWidth(headers[i], fontName, fontSize);
            var maxContentWidth = 0.0;
            foreach (var row in rows.Take(200))
            {
                if (i < row.Length)
                    maxContentWidth = Math.Max(maxContentWidth, MeasureTextWidth(row[i], fontName, fontSize));
            }
            widths[i] = Math.Max(headerWidth, maxContentWidth) + 16;
        }

        var totalWidth = widths.Sum();
        var targetWidth = 1100.0;
        if (totalWidth < targetWidth)
        {
            var scale = targetWidth / totalWidth;
            for (int i = 0; i < widths.Length; i++)
                widths[i] *= scale;
        }

        return widths;
    }

    private static double MeasureTextWidth(string text, string fontName, double fontSize)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        using (var bmp = new SD.Bitmap(1, 1))
        using (var g = SD.Graphics.FromImage(bmp))
        {
            var size = g.MeasureString(text, new SD.Font(fontName, (float)fontSize));
            return size.Width;
        }
    }

    private static string? GetPropertyValue(object item, string propertyPath)
    {
        if (item == null || string.IsNullOrEmpty(propertyPath))
            return null;

        try
        {
            var dapperDict = item as System.Collections.Generic.IDictionary<string, object>;
            if (dapperDict != null)
            {
                if (dapperDict.TryGetValue(propertyPath, out var value))
                    return value?.ToString();
                return null;
            }

            var current = item;
            var parts = propertyPath.Split('.');
            foreach (var part in parts)
            {
                if (current == null)
                    return null;

                var type = current.GetType();
                var propInfo = type.GetProperty(part, 
                    System.Reflection.BindingFlags.Public | 
                    System.Reflection.BindingFlags.Instance |
                    System.Reflection.BindingFlags.IgnoreCase);

                if (propInfo != null)
                {
                    current = propInfo.GetValue(current);
                }
                else
                {
                    var fieldInfo = type.GetField(part, 
                        System.Reflection.BindingFlags.Public | 
                        System.Reflection.BindingFlags.Instance |
                        System.Reflection.BindingFlags.IgnoreCase);

                    if (fieldInfo != null)
                    {
                        current = fieldInfo.GetValue(current);
                    }
                    else
                    {
                        return null;
                    }
                }
            }

            return current?.ToString();
        }
        catch
        {
            return null;
        }
    }

    private static string? TryGetDynamicValue(object item, string? propertyPath)
    {
        if (item == null || string.IsNullOrEmpty(propertyPath))
            return null;

        try
        {
            var expando = item as System.Dynamic.ExpandoObject;
            if (expando != null)
            {
                var dict = expando as System.Collections.Generic.IDictionary<string, object>;
                if (dict != null && dict.TryGetValue(propertyPath, out var value))
                    return value?.ToString();
                return null;
            }

            var dictionary = item as System.Collections.IDictionary;
            if (dictionary != null)
            {
                if (dictionary.Contains(propertyPath))
                    return dictionary[propertyPath]?.ToString();
                return null;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    private void RenderPage(string[] headers, double[] colWidths, List<string[]> dataRows, int pageNum, int totalPages, string pageTitle = "销售明细", List<int>? rowFlags = null)
    {
        var skipCols = new HashSet<int>();
        for (int i = 0; i < headers.Length; i++)
            if (headers[i] == "备注") skipCols.Add(i);
        var validIdx = Enumerable.Range(0, headers.Length).Where(i => !skipCols.Contains(i)).ToArray();
        var validHeaders = validIdx.Select(i => headers[i]).ToArray();
        var validWidths = validIdx.Select(i => colWidths[i]).ToArray();
        var validRows = dataRows.Select(r => validIdx.Select(i => i < r.Length ? r[i] : "").ToArray()).ToList();

        var rowH = 22;
        var headerH = 28;
        var titleH = 42;
        var footerH = 24;
        var margin = 16;
        var tableW = (int)validWidths.Sum(w => w);
        var contentH = headerH + validRows.Count * rowH;
        var width = tableW + margin * 2;
        var height = titleH + contentH + footerH + margin * 2;

        var tempFile = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            $"QP11_Export_{Guid.NewGuid():N}.png");

        using (var fullBmp = new SD.Bitmap(width, height, SD.Imaging.PixelFormat.Format32bppArgb))
        {
            fullBmp.SetResolution(96, 96);
            using (var g = SD.Graphics.FromImage(fullBmp))
            {
                g.SmoothingMode = SD2D.SmoothingMode.AntiAlias;
                g.TextRenderingHint = SDT.TextRenderingHint.AntiAliasGridFit;
                g.Clear(SD.Color.White);

                using (var titleBrush = new SD.SolidBrush(SD.Color.FromArgb(230, 70, 130, 180)))
                    g.FillRectangle(titleBrush, margin, margin, width - margin * 2, titleH - 4);
                using (var titleFont = new SD.Font("Microsoft YaHei", 18, SD.FontStyle.Bold))
                using (var titleBrush2 = new SD.SolidBrush(SD.Color.White))
                {
                    var titleSize = g.MeasureString(pageTitle, titleFont);
                    g.DrawString(pageTitle, titleFont, titleBrush2, (width - titleSize.Width) / 2, margin + 8);
                }
                using (var pageFont = new SD.Font("Microsoft YaHei", 9))
                using (var grayBrush = new SD.SolidBrush(SD.Color.Gray))
                    g.DrawString($"第 {pageNum} / {totalPages} 页", pageFont, grayBrush, width - margin - 72, margin + 14);

                var y = margin + titleH;
                using (var headerBgBrush = new SD.SolidBrush(SD.Color.FromArgb(255, 70, 130, 180)))
                    g.FillRectangle(headerBgBrush, margin, y, tableW, headerH);
                using (var headerFont = new SD.Font("Microsoft YaHei", 11, SD.FontStyle.Bold))
                using (var whiteBrush = new SD.SolidBrush(SD.Color.White))
                {
                    var x = margin;
                    for (int i = 0; i < validHeaders.Length && i < validWidths.Length; i++)
                    {
                        g.DrawString(validHeaders[i], headerFont, whiteBrush, x + 6, y + 6);
                        x += (int)validWidths[i];
                    }
                }

                y += headerH;
                using (var altBrush = new SD.SolidBrush(SD.Color.FromArgb(12, 0, 0, 0)))
                using (var borderPen = new SD.Pen(SD.Color.FromArgb(35, 200, 200, 200), 0.5f))
                {
                    for (int r = 0; r < validRows.Count; r++)
                    {
                        if (r % 2 == 1)
                            g.FillRectangle(altBrush, margin, y, tableW, rowH);
                        else
                            g.DrawRectangle(borderPen, margin, y, tableW, rowH);
                        y += rowH;
                    }
                }
            }

            fullBmp.Save(tempFile, SD.Imaging.ImageFormat.Png);
        }

        const int batchSize = 120;
        var totalBatches = (validRows.Count + batchSize - 1) / batchSize;

        for (int batch = 0; batch < totalBatches; batch++)
        {
            var startRow = batch * batchSize;
            var endRow = Math.Min(startRow + batchSize, validRows.Count);
            var startY = margin + titleH + headerH + startRow * rowH;

            using (var bmp = SD.Image.FromFile(tempFile) as SD.Bitmap)
            {
                if (bmp == null) return;
                using (var g = SD.Graphics.FromImage(bmp))
                {
                    g.TextRenderingHint = SDT.TextRenderingHint.AntiAliasGridFit;

                    var cellFont = new SD.Font("Microsoft YaHei", 10);
                    var blackBrush = new SD.SolidBrush(SD.Color.Black);
                    var redBrush = new SD.SolidBrush(SD.Color.FromArgb(200, 30, 30));
                    try
                    {
                        var y = startY;
                        for (int r = startRow; r < endRow; r++)
                        {
                            using (var rowBmp = new SD.Bitmap(tableW, rowH, SD.Imaging.PixelFormat.Format32bppArgb))
                            {
                                rowBmp.SetResolution(96, 96);
                                using (var rg = SD.Graphics.FromImage(rowBmp))
                                {
                                    rg.TextRenderingHint = SDT.TextRenderingHint.AntiAliasGridFit;
                                    rg.Clear(SD.Color.Transparent);

                                    var row = validRows[r];
                                    var isReturn = row.Length > 0 && row[0].StartsWith("TH");
                                    var flagIdx = Array.IndexOf(validHeaders, "状态");
                                    if (!isReturn && flagIdx >= 0 && flagIdx < row.Length && row[flagIdx] == "退货")
                                        isReturn = true;
                                    if (!isReturn && rowFlags != null && r < rowFlags.Count && rowFlags[r] == 2)
                                        isReturn = true;
                                    var fore = isReturn ? redBrush : blackBrush;
                                    var x = 0;
                                    for (int i = 0; i < validHeaders.Length && i < validWidths.Length; i++)
                                    {
                                        var text = i < row.Length ? row[i] : "";
                                        // 数值列统一保留2位小数（排除单号、编号等文本型ID列）
                                        if (!string.IsNullOrEmpty(text) && decimal.TryParse(text, out var numVal)
                                            && validHeaders[i] != "单号" && validHeaders[i] != "编号"
                                            && validHeaders[i] != "配件编号")
                                            text = numVal.ToString("N2");
                                        rg.DrawString(text, cellFont, fore, x + 5, 5);
                                        x += (int)validWidths[i];
                                    }
                                }
                                g.DrawImage(rowBmp, margin, y);
                            }
                            y += rowH;
                        }
                    }
                    finally
                    {
                        cellFont.Dispose();
                        blackBrush.Dispose();
                        redBrush.Dispose();
                    }

                    if (batch == totalBatches - 1)
                    {
                        var footerY = margin + titleH + headerH + validRows.Count * rowH;
                        var amtIdx = Array.IndexOf(validHeaders, "数量");
                        var stotalIdx = Array.IndexOf(validHeaders, "小计");
                        var btotalIdx = Array.IndexOf(validHeaders, "开票总额");
                        using (var sumBgBrush = new SD.SolidBrush(SD.Color.FromArgb(255, 70, 130, 180)))
                            g.FillRectangle(sumBgBrush, margin, footerY, tableW, rowH + 3);
                        using (var sumFont = new SD.Font("Microsoft YaHei", 11, SD.FontStyle.Bold))
                        using (var whiteBrush = new SD.SolidBrush(SD.Color.White))
                        {
                            var x = margin;
                            for (int i = 0; i < validHeaders.Length; i++)
                            {
                                string sumText = "";
                                if (i == amtIdx)
                                    sumText = validRows.Sum(r => int.TryParse(r.Length > i ? r[i] : "", out var v) ? v : 0).ToString("N2");
                                else if (i == stotalIdx)
                                    sumText = validRows.Sum(r => decimal.TryParse(r.Length > i ? r[i] : "", out var v) ? v : 0m).ToString("N2");
                                else if (i == btotalIdx)
                                    sumText = validRows.Sum(r => decimal.TryParse(r.Length > i ? r[i] : "", out var v) ? v : 0m).ToString("N2");
                                else if (i == 0)
                                    sumText = "合 计";
                                g.DrawString(sumText, sumFont, whiteBrush, x + 5, footerY + 5);
                                x += (int)validWidths[i];
                            }
                        }
                        var lineY = footerY + rowH + 3;
                        using (var linePen = new SD.Pen(SD.Color.LightGray, 1))
                            g.DrawLine(linePen, margin, lineY, margin + tableW, lineY);
                        using (var footerFont = new SD.Font("Microsoft YaHei", 9))
                        using (var grayBrush = new SD.SolidBrush(SD.Color.Gray))
                        {
                            g.DrawString($"共 {validRows.Count} 条记录", footerFont, grayBrush, margin, lineY + 5);
                            g.DrawString(DateTime.Now.ToString("yyyy-MM-dd HH:mm"), footerFont, grayBrush, width - margin - 90, lineY + 5);
                        }
                    }

                    var newFile = System.IO.Path.Combine(
                        System.IO.Path.GetTempPath(),
                        $"QP11_Export_{Guid.NewGuid():N}.png");
                    bmp.Save(newFile, SD.Imaging.ImageFormat.Png);
                    try { System.IO.File.Delete(tempFile); } catch (Exception ex) { Serilog.Log.Warning(ex, "删除临时文件失败"); }
                    tempFile = newFile;
                }
            }

            // 注：移除 GC.Collect() — 让 CLR 自行管理内存，显式GC会导致代际提升和暂停
        }

        _tempFiles.Add(tempFile);

        SD.Bitmap displayBmp;
        if (width > MaxDisplaySize || height > MaxDisplaySize)
        {
            var ratio = Math.Min((double)MaxDisplaySize / width, (double)MaxDisplaySize / height);
            var newW = Math.Max(1, (int)(width * ratio));
            var newH = Math.Max(1, (int)(height * ratio));
            displayBmp = new SD.Bitmap(newW, newH, SD.Imaging.PixelFormat.Format32bppArgb);
            displayBmp.SetResolution(96, 96);
            using (var dg = SD.Graphics.FromImage(displayBmp))
            {
                dg.InterpolationMode = SD2D.InterpolationMode.HighQualityBilinear;
                dg.SmoothingMode = SD2D.SmoothingMode.AntiAlias;
                using (var src = SD.Image.FromFile(tempFile) as SD.Bitmap)
                {
                    if (src != null)
                        dg.DrawImage(src, new SD.Rectangle(0, 0, newW, newH));
                }
            }
        }
        else
        {
            displayBmp = SD.Image.FromFile(tempFile) as SD.Bitmap ?? new SD.Bitmap(width, height);
        }

        using (displayBmp)
        {
            _displayImages.Add(GdiBitmapToWpf(displayBmp));
        }
    }

    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    private static BitmapSource GdiBitmapToWpf(SD.Bitmap bmp)
    {
        IntPtr hBitmap = bmp.GetHbitmap();
        try
        {
            return System.Windows.Interop.Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap, IntPtr.Zero, System.Windows.Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }

    private void ShowThumbnails()
    {
        var items = new List<object>();
        for (int i = 0; i < _displayImages.Count; i++)
        {
            var thumb = CreateThumbnail(_displayImages[i], 200, 150);
            items.Add(new { Image = thumb, Label = $"第 {i + 1} 页" });
        }
        lstThumbs.ItemsSource = items;
        if (_displayImages.Count > 0)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                lstThumbs.SelectedIndex = 0;
                imgPreview.Source = _displayImages[0];
                UpdatePageSizeText(0);
                lstThumbs.ScrollIntoView(lstThumbs.SelectedItem);
            }), System.Windows.Threading.DispatcherPriority.Loaded);
        }
    }

    private static BitmapSource CreateThumbnail(BitmapSource source, int maxWidth, int maxHeight)
    {
        var ratioX = (double)maxWidth / source.PixelWidth;
        var ratioY = (double)maxHeight / source.PixelHeight;
        var ratio = Math.Min(ratioX, ratioY);
        var scaled = new TransformedBitmap(source, new ScaleTransform(ratio, ratio));
        return BitmapFrame.Create(scaled);
    }

    private void LstThumbs_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var idx = lstThumbs.SelectedIndex;
        if (idx >= 0 && idx < _displayImages.Count)
        {
            imgPreview.Source = _displayImages[idx];
            UpdatePageSizeText(idx);
        }
    }

    private void UpdatePageSizeText(int idx)
    {
        if (idx >= 0 && idx < _tempFiles.Count)
        {
            using (var bmp = SD.Image.FromFile(_tempFiles[idx]) as SD.Bitmap)
            {
                if (bmp != null)
                    txtPageSize.Text = $"原始尺寸: {bmp.Width} × {bmp.Height}";
            }
        }
    }

    private async void BtnCopyAll_Click(object sender, RoutedEventArgs e)
    {
        btnCopyAll.IsEnabled = false;
        txtStatus.Text = "正在合并图片...";
        try
        {
            await Dispatcher.InvokeAsync(() => CopyAllImagesToClipboard(), DispatcherPriority.Background);
            txtStatus.Text = "已复制全部图片到剪贴板";
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, $"复制失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            txtStatus.Text = "复制失败";
        }
        finally
        {
            btnCopyAll.IsEnabled = true;
        }
    }

    private void CopyAllImagesToClipboard()
    {
        if (_tempFiles.Count == 0) return;

        for (int retry = 0; retry < 10; retry++)
        {
            try
            {
                Clipboard.Clear();
                var dataObj = new DataObject();

                dataObj.SetData(DataFormats.FileDrop, _tempFiles.ToArray());

                var images = new List<SD.Bitmap>();
                try
                {
                    foreach (var f in _tempFiles)
                    {
                        var bmp = SD.Image.FromFile(f) as SD.Bitmap;
                        if (bmp != null) images.Add(bmp);
                    }

                    if (images.Count > 0)
                    {
                        var gap = 8;
                        var totalWidth = images.Max(b => b.Width);
                        var totalHeight = images.Sum(b => b.Height) + gap * (images.Count - 1);

                        using var combined = new SD.Bitmap(totalWidth, totalHeight, SD.Imaging.PixelFormat.Format32bppArgb);
                        combined.SetResolution(96, 96);
                        using (var g = SD.Graphics.FromImage(combined))
                        {
                            g.Clear(SD.Color.White);
                            var y = 0;
                            foreach (var img in images)
                            {
                                g.DrawImage(img, 0, y, img.Width, img.Height);
                                y += img.Height + gap;
                            }
                        }
                        dataObj.SetImage(GdiBitmapToWpf(combined));
                    }
                }
                finally
                {
                    foreach (var img in images)
                        img.Dispose();
                }

                Clipboard.SetDataObject(dataObj, true);
                return;
            }
            catch { System.Threading.Thread.Sleep(100); }
        }
        throw new Exception("无法访问剪贴板");
    }

    private void CopyImageToClipboard(int index)
    {
        if (index < 0 || index >= _tempFiles.Count) return;
        var filePath = _tempFiles[index];

        for (int retry = 0; retry < 10; retry++)
        {
            try
            {
                Clipboard.Clear();
                var dataObj = new DataObject();
                dataObj.SetData(DataFormats.FileDrop, new[] { filePath });

                using (var fs = new System.IO.FileStream(filePath, System.IO.FileMode.Open, System.IO.FileAccess.Read))
                {
                    dataObj.SetData("PNG", fs);
                }

                using (var bmp = SD.Image.FromFile(filePath) as SD.Bitmap)
                {
                    if (bmp != null)
                        dataObj.SetImage(GdiBitmapToWpf(bmp));
                }

                Clipboard.SetDataObject(dataObj, true);
                return;
            }
            catch { System.Threading.Thread.Sleep(100); }
        }
        throw new Exception("无法访问剪贴板");
    }

    public void CopySingleImage(int index)
    {
        if (index < 0 || index >= _tempFiles.Count) return;
        try
        {
            CopyImageToClipboard(index);
            txtStatus.Text = $"已复制第 {index + 1} 张图片";
            Close();
        }
        catch
        {
            MessageBox.Show(this, "无法访问剪贴板，请稍后重试", "错误", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void CleanupTempFiles()
    {
        foreach (var f in _tempFiles)
        {
            try { System.IO.File.Delete(f); } catch (Exception ex) { Serilog.Log.Warning(ex, "清理临时文件失败"); }
        }
    }

    private void BtnCopyImg1_Click(object sender, RoutedEventArgs e)
    {
        if (_tempFiles.Count >= 1)
            CopySingleImage(0);
        else
            MessageBox.Show(this, "没有第1张图片", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnCopyImg2_Click(object sender, RoutedEventArgs e)
    {
        if (_tempFiles.Count >= 2)
            CopySingleImage(1);
        else
            MessageBox.Show(this, "没有第2张图片", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnMouseDoubleClick(MouseButtonEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        CopySingleImage(lstThumbs.SelectedIndex);
    }
}
