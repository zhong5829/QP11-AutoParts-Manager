using System.Collections.Generic;

namespace QP11.Core.Entities;

/// <summary>配件适配车型的品牌分组 — 对应318car /product/listProductAdaptVehicle 响应中 vehicleBrandList 项</summary>
public class VinAdaptVehicleBrand
{
    public long BrandId { get; set; }
    public string BrandName { get; set; } = "";
    public List<VinAdaptVehicleItem> Vehicles { get; set; } = [];
}

/// <summary>适配车型条目 — 品牌下的一款具体车型</summary>
public class VinAdaptVehicleItem
{
    public string Brand { get; set; } = "";
    public string Models { get; set; } = "";
    public string DisplacementWithT { get; set; } = "";
    public string YearRange { get; set; } = "";

    /// <summary>显示文本（如"思域 1.5T 2016-2022"）</summary>
    public string DisplayText
    {
        get
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(Models)) parts.Add(Models);
            if (!string.IsNullOrEmpty(DisplacementWithT)) parts.Add(DisplacementWithT);
            if (!string.IsNullOrEmpty(YearRange)) parts.Add(YearRange);
            return string.Join(" ", parts);
        }
    }
}