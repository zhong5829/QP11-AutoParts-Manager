using System.Collections.Generic;
using System.Text.Json;

namespace QP11.Core.Entities;

/// <summary>配件卡片 — 对应318car API /app/product/user/pageProduct 响应中的配件数据</summary>
public class VinPartCard
{
    // 318car平台字段
    public long Id { get; set; }
    public string? Name { get; set; }
    public string? Model { get; set; }
    public List<string> ImgUrlList { get; set; } = [];
    public string? TenantBrandName { get; set; }
    public string? TenantCategoryName { get; set; }
    public string? Notes { get; set; }
    public string? Unit { get; set; }
    public string? Producer { get; set; }
    public decimal Price { get; set; }
    public decimal PurchasePrice { get; set; }
    public decimal PurchaseGuidePrice { get; set; }
    public decimal GuidePrice { get; set; }
    public decimal CostPrice { get; set; }
    public int Stock { get; set; }
    public string? InstallationLocation { get; set; }
    public List<VinProductUnit> ProductUnitList { get; set; } = [];

    // 本地补充字段（匹配part_data表后填充）
    public long? LocalPartId { get; set; }
    public string? LocalPartNo { get; set; }
    public string? LocalName { get; set; }
    public decimal? LsPrice { get; set; }
    public decimal? PfPrice { get; set; }
    public int StockAmount { get; set; }
    public bool IsLocalMatched { get; set; }
    /// <summary>所有编码命中的本地候选（多条时让用户选择）</summary>
    public List<VinLocalMatch> LocalCandidates { get; set; } = [];

    // 多数据源字段
    /// <summary>配件数据来源（"318car"、"品秀"等），多来源时逗号分隔</summary>
    public string? SourceName { get; set; }

    /// <summary>同编码配件来自其他数据源的数据（用于价格对比）</summary>
    public List<VinPartCard> AlternateSources { get; set; } = [];

    /// <summary>OE号（品秀数据源特有，318car为空）</summary>
    public string? PartNumber { get; set; }

    /// <summary>车型备注（统一318car的notes和品秀的vehicleComment）</summary>
    public string? VehicleComment { get; set; }

    /// <summary>318car适配车型备注（vehicleNotes字段，可能是字符串或数组）</summary>
    [System.Text.Json.Serialization.JsonPropertyName("vehicleNotes")]
    public JsonElement? VehicleNotesRaw { get; set; }

    /// <summary>VehicleNotes的显示文本（自动从数组/字符串转换）</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public string? VehicleNotes
    {
        get
        {
            if (VehicleNotesRaw == null) return null;
            var el = VehicleNotesRaw.Value;
            return el.ValueKind switch
            {
                JsonValueKind.String => el.GetString(),
                JsonValueKind.Array => string.Join(", ", el.EnumerateArray().Select(e => e.GetString() ?? "")),
                JsonValueKind.Object => el.GetRawText(),
                _ => null
            };
        }
    }

    /// <summary>第一张图片URL（用于列表显示）</summary>
    public string? FirstImgUrl => ImgUrlList.Count > 0 ? ImgUrlList[0] : null;
}

/// <summary>本地匹配候选项</summary>
public class VinLocalMatch
{
    public long PartId { get; set; }
    public string? PartNo { get; set; }
    public string? Name { get; set; }
    public string? CarName { get; set; }
    public string? CarType { get; set; }
    public int StockAmount { get; set; }
    public decimal LsPrice { get; set; }
    public decimal PfPrice { get; set; }
    public int Score { get; set; }
}

/// <summary>配件单位换算</summary>
public class VinProductUnit
{
    public string? Unit { get; set; }
    public int Sort { get; set; }
    public decimal ConvertNum { get; set; }
}
