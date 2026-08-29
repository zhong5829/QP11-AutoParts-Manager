namespace QP11.Core.Models;

/// <summary>
/// 应收/应付客户（供应商）列表行：带欠款合计
/// 用于替代 GetClientArrearageListAsync 的 dynamic 返回（解决 DapperRow 与匿名类型混用导致的
/// RuntimeBinderException，SQL 列别名 bid/name/total_je 映射到 Bid/Name/TotalJe）
/// </summary>
public class ArrearageSummaryItem
{
    public string? Bid { get; set; }
    public string? Name { get; set; }
    public decimal TotalJe { get; set; }
}