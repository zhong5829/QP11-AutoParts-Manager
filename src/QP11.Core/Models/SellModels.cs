using System;
using System.Collections.Generic;

namespace QP11.Core.Models;

public class SellOrderSummary
{
    public decimal OriginalTotal { get; set; }
    public decimal DiscountedTotal { get; set; }
    public decimal Yunfei { get; set; }
    public decimal TotalPayment { get; set; }
}

public class PaymentInfo
{
    public decimal Cash { get; set; }
    public decimal Checks { get; set; }
    public decimal CardPay { get; set; }
    public decimal Zhifubao { get; set; }
    public decimal Weixin { get; set; }
}

public class PagedResult<T>
{
    public IEnumerable<T> Data { get; set; } = new List<T>();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);

    public PagedResult(IEnumerable<T> data, int total, int page, int pageSize)
    {
        Data = data;
        Total = total;
        Page = page;
        PageSize = pageSize;
    }
}

public class PartQueryCriteria
{
    public string? Keyword { get; set; }
    public string? ClassId { get; set; }
}

public enum MemberCardStatus
{
    Active = 0,
    Lost = 1,
    Expired = 2,
    Renew = 3,
    Cancelled = 4
}
