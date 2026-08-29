using System;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace QP11.Wpf.Services.LabelPrint;

/// <summary>
/// Code128（Code B 子集）一维码渲染。
/// 从 BarcodeWindow 的私有实现抽取，供标签打印统一复用。
/// </summary>
public static class Code128Renderer
{
    // Code128 字符条码图案（宽度序列：1=窄条/空隙，2/3/4=宽条），索引 0-105 对应 ASCII 32..'a'-32，104=Start B，106=Stop
    private static readonly string[] Patterns =
    {
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

    /// <summary>
    /// 渲染 Code128 一维码为位图。
    /// </summary>
    /// <param name="value">编码内容</param>
    /// <param name="width">输出位图宽度（px）</param>
    /// <param name="height">输出位图高度（px）</param>
    /// <param name="barHeightRatio">条码条高占整体高度的比例（0-1）</param>
    public static RenderTargetBitmap Render(string value, double width = 200, double height = 60, double barHeightRatio = 0.75)
    {
        if (string.IsNullOrEmpty(value))
            return new RenderTargetBitmap(1, 1, 96, 96, PixelFormats.Pbgra32);

        var bars = new StringBuilder();
        bars.Append(Patterns[104]); // Start Code B

        int sum = 104;
        for (int i = 0; i < value.Length; i++)
        {
            // 仅支持 ASCII 32..127（对应码 0..95）；非可打印字符（中文/控制符等）用 '?' 兜底，
            // 防止字符码越界 Patterns 数组（如 partno 为 char 字段可能含特殊字符）
            int code = value[i] >= 32 && value[i] <= 127 ? value[i] - 32 : '?' - 32;
            bars.Append(Patterns[code]);
            sum += code * (i + 1);
        }

        int checksum = sum % 103;
        bars.Append(Patterns[checksum]);
        bars.Append(Patterns[106]); // Stop

        var barStr = bars.ToString();
        var totalUnits = 0;
        foreach (char c in barStr) totalUnits += c - '0';

        var safeW = (int)Math.Max(1, width);
        var safeH = (int)Math.Max(1, height);
        var bmp = new RenderTargetBitmap(safeW, safeH, 96, 96, PixelFormats.Pbgra32);
        var dv = new DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            double barWidth = safeW / (double)totalUnits;
            double x = 0;
            bool isBar = true;
            foreach (char c in barStr)
            {
                int w = c - '0';
                if (isBar)
                    dc.DrawRectangle(Brushes.Black, null, new Rect(x, 0, w * barWidth, safeH * barHeightRatio));
                x += w * barWidth;
                isBar = !isBar;
            }
        }
        bmp.Render(dv);
        bmp.Freeze();
        return bmp;
    }
}