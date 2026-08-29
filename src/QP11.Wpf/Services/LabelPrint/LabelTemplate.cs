using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace QP11.Wpf.Services.LabelPrint;

/// <summary>标签上的一个布局元素（编码/条码/名称/车型），位置单位 mm，字号单位 px（条码字段 FontSize 表示条码高度 mm）</summary>
public class LabelField
{
    public string Key { get; set; } = "";
    public double XMm { get; set; } = 2;
    public double YMm { get; set; } = 2;
    public double FontSize { get; set; } = 12;
    public bool Visible { get; set; } = true;
}

/// <summary>标签模板定义（尺寸单位均为毫米，字号单位 px）</summary>
public class LabelTemplate
{
    /// <summary>字段 Key</summary>
    public const string FieldCode = "Code";
    public const string FieldBarcode = "Barcode";
    public const string FieldName = "Name";
    public const string FieldCarType = "CarType";

    public string Name { get; set; } = "50×30";

    /// <summary>单张标签宽（mm）</summary>
    public double LabelWidthMm { get; set; } = 50;

    /// <summary>单张标签高（mm）</summary>
    public double LabelHeightMm { get; set; } = 30;

    /// <summary>每排标签个数</summary>
    public int ColsPerRow { get; set; } = 1;

    /// <summary>页边距（mm）</summary>
    public double MarginTopMm { get; set; } = 4;
    public double MarginBottomMm { get; set; } = 4;
    public double MarginLeftMm { get; set; } = 3;
    public double MarginRightMm { get; set; } = 3;

    /// <summary>标签间距（mm）</summary>
    public double GapMm { get; set; } = 3;

    /// <summary>内容字号（px，96dpi）——旧字段，运行时仅用于生成默认布局</summary>
    public double FontSizeCode { get; set; } = 14;
    public double FontSizeName { get; set; } = 11;
    public double FontSizeCarType { get; set; } = 9;

    /// <summary>内容显隐——旧字段，运行时仅用于生成默认布局</summary>
    public bool ShowName { get; set; } = true;
    public bool ShowCarType { get; set; } = true;

    /// <summary>布局元素（可拖动调整位置/字号/显隐）</summary>
    public List<LabelField> Fields { get; set; } = new();

    /// <summary>是否为内置模板（内置不可删除，仅运行时标记，不持久化）</summary>
    [JsonIgnore]
    public bool IsBuiltIn { get; set; }

    /// <summary>补齐元素布局：无 Fields 或字段不完整时，按旧字号字段与标签尺寸生成默认布局</summary>
    public void EnsureFields()
    {
        if (Fields == null) Fields = new List<LabelField>();
        var keys = Fields.Where(f => f != null).Select(f => f.Key).ToList();
        if (new[] { FieldCode, FieldBarcode, FieldName, FieldCarType }.All(k => keys.Contains(k)))
            return;

        double mmOf(double px) => px * 25.4 / 96.0;   // 字号 px → 高度 mm
        const double pad = 2.0;
        var codeH = mmOf(FontSizeCode);
        var barcodeY = pad + codeH + 1.0;
        var nameY = barcodeY + 12.0 + 1.0;
        var carTypeY = nameY + mmOf(FontSizeName) + 1.0;

        Fields.Clear();
        Fields.Add(new LabelField { Key = FieldCode, XMm = pad, YMm = pad, FontSize = FontSizeCode });
        Fields.Add(new LabelField { Key = FieldBarcode, XMm = pad, YMm = barcodeY, FontSize = 12 });
        Fields.Add(new LabelField { Key = FieldName, XMm = pad, YMm = nameY, FontSize = FontSizeName });
        Fields.Add(new LabelField { Key = FieldCarType, XMm = pad, YMm = carTypeY, FontSize = FontSizeCarType });
    }

    public LabelTemplate Clone()
    {
        var copy = (LabelTemplate)MemberwiseClone();
        copy.Fields = Fields?.Select(f => new LabelField
        {
            Key = f.Key, XMm = f.XMm, YMm = f.YMm, FontSize = f.FontSize, Visible = f.Visible
        }).ToList() ?? new List<LabelField>();
        return copy;
    }
}