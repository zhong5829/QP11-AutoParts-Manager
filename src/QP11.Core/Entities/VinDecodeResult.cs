using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace QP11.Core.Entities;

/// <summary>VIN解码结果 — 对应318car API /app/product/getVehicleByVin 响应</summary>
public class VinDecodeResult
{
    public string Vin { get; set; } = "";
    public string Brand { get; set; } = "";
    public string Manufacturers { get; set; } = "";
    public string Series { get; set; } = "";
    public string Models { get; set; } = "";
    public string ChassisCode4 { get; set; } = "";
    public string DisplacementWithT { get; set; } = "";
    public string EngineModel { get; set; } = "";
    public string YearRange { get; set; } = "";
    public string Generation { get; set; } = "";
    public string VehicleAttributes { get; set; } = "";
    public string BrandImg { get; set; } = "";
    public string ProductTime { get; set; } = "";
    public List<string> VehicleIds { get; set; } = [];

    /// <summary>驱动方式原始值（318car字段名: driveModel，如"前置前驱"、"前置四驱"）</summary>
    [JsonPropertyName("driveModel")]
    public string DriveModel { get; set; } = "";

    /// <summary>变速箱描述原始值（318car字段名: transmissionDescription，如"自动变速器(AT)"）</summary>
    [JsonPropertyName("transmissionDescription")]
    public string TransmissionDescription { get; set; } = "";

    /// <summary>驱动方式简短标签（如"前驱"、"四驱"、"2驱"、"4驱"），供UI显示</summary>
    [JsonIgnore]
    public string DriveMode
    {
        get
        {
            var raw = DriveModel;
            if (string.IsNullOrEmpty(raw)) return "";
            // "前置前驱" → "前驱", "前置四驱" → "四驱", "前置后驱" → "后驱"
            if (raw.Contains("四驱") || raw.Contains("4WD") || raw.Contains("AWD")) return "四驱";
            if (raw.Contains("前驱")) return "前驱";
            if (raw.Contains("后驱")) return "后驱";
            if (raw.Contains("两驱")) return "两驱";
            return raw;
        }
    }

    /// <summary>变速箱类型简短标签（如"AT"、"MT"、"CVT"），供UI显示</summary>
    [JsonIgnore]
    public string GearboxType
    {
        get
        {
            var raw = TransmissionDescription;
            if (string.IsNullOrEmpty(raw)) return "";
            // 从"自动变速器(AT)"中提取括号内类型
            var start = raw.IndexOf('(');
            var end = raw.IndexOf(')');
            if (start >= 0 && end > start)
                return raw.Substring(start + 1, end - start - 1);
            if (raw.Contains("AT") || raw.Contains("自动")) return "AT";
            if (raw.Contains("MT") || raw.Contains("手动")) return "MT";
            if (raw.Contains("CVT")) return "CVT";
            if (raw.Contains("DCT") || raw.Contains("双离合")) return "DCT";
            return raw;
        }
    }
}
