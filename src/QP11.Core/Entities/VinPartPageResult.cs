using System.Collections.Generic;

namespace QP11.Core.Entities;

/// <summary>配件分页结果</summary>
public class VinPartPageResult
{
    public int Total { get; set; }
    public int Pages { get; set; }
    public int Current { get; set; }
    public List<VinPartCategoryGroup> Categories { get; set; } = [];
    public long AdaptQueryRecordId { get; set; }
}

/// <summary>按分类组织的配件组</summary>
public class VinPartCategoryGroup
{
    public long TenantCategoryId { get; set; }
    public string? CategoryName { get; set; }
    public List<VinPartCard> Products { get; set; } = [];
}
