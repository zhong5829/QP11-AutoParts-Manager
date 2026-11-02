using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QP11.Wpf.Helpers;

/// <summary>
/// 配件图片压缩工具：统一限制 100KB（part_data.picture），超出自动压缩。
/// 策略：长边先等比限到 1024px，再逐步降低 JPEG 质量直至达标。
/// 配件编辑窗口（PartEditWindow）与入库窗口（BuyEditDialog）共用。
/// </summary>
public static class ImageCompressor
{
    /// <summary>图片大小上限：100KB</summary>
    public const int SizeLimit = 100 * 1024;

    /// <summary>
    /// 将图片压缩到 100KB 以内；原始字节已达标则原样返回。
    /// </summary>
    /// <returns>最终字节数组</returns>
    /// <param name="raw">原始图片字节</param>
    /// <param name="originalCompressed">输出：是否执行了压缩</param>
    public static byte[] CompressToLimit(byte[] raw, out bool originalCompressed)
    {
        originalCompressed = false;
        if (raw.Length <= SizeLimit) return raw;

        originalCompressed = true;
        var bitmap = LoadBitmap(raw);

        // 长边超过 1024px 先等比缩小，避免超大图靠纯质量压缩失真
        double scale = 1.0;
        int maxEdge = Math.Max(bitmap.PixelWidth, bitmap.PixelHeight);
        if (maxEdge > 1024) scale = 1024.0 / maxEdge;

        int quality = 90;
        byte[] lastResult;
        while (true)
        {
            lastResult = EncodeJpeg(bitmap, scale, quality);
            if (lastResult.Length <= SizeLimit) return lastResult;

            quality -= 10;
            if (quality >= 30) continue;

            // 质量到底仍超标：缩小尺寸后重置质量继续
            scale /= 1.4;
            if (scale < 0.08) return lastResult; // 极限兜底：不再缩小
            quality = 85;
        }
    }

    private static byte[] EncodeJpeg(BitmapSource source, double scale, int quality)
    {
        BitmapSource frame = scale >= 1.0
            ? source
            : new TransformedBitmap(source, new ScaleTransform(scale, scale));
        var encoder = new JpegBitmapEncoder { QualityLevel = quality };
        encoder.Frames.Add(BitmapFrame.Create(frame));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    /// <summary>从字节数组解码可冻结的位图（OnLoad 后即可脱离流使用）</summary>
    public static BitmapImage LoadBitmap(byte[] bytes)
    {
        var bitmap = new BitmapImage();
        using var ms = new MemoryStream(bytes);
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = ms;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }

    public static string FormatSize(int bytes) =>
        bytes >= 1024 ? $"{bytes / 1024.0:F1} KB" : $"{bytes} B";
}
