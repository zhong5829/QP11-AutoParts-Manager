using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace QP11.Core.Entities;

[Table("bill_sell")]
public class BillSell
{
    [Key]
    [Column("sn")]
    public string? Sn { get; set; }

    [Column("client")]
    public string? Client { get; set; }

    /// <summary>客户名称（来自 client_infor.name），仅查询时填充，不持久化到 bill_sell</summary>
    [NotMapped]
    public string? ClientName { get; set; }

    [Column("worker")]
    public string? Worker { get; set; }

    [Column("operator")]
    public string? Operator { get; set; }

    [Column("checkno")]
    public string? Checkno { get; set; }

    [Column("total")]
    public decimal? Total { get; set; }

    [Column("bill_total")]
    public decimal? BillTotal { get; set; }

    [Column("discount_rate")]
    public decimal? DiscountRate { get; set; }

    [Column("total_payment")]
    public decimal? TotalPayment { get; set; }

    [Column("bill_payment")]
    public decimal? BillPayment { get; set; }

    [Column("cash")]
    public decimal? Cash { get; set; }

    [Column("collection")]
    public decimal? Collection { get; set; }

    [Column("checks")]
    public decimal? Checks { get; set; }

    [Column("arrear")]
    public decimal? Arrear { get; set; }

    [Column("memo")]
    public string? Memo { get; set; }

    [Column("flag")]
    public int? Flag { get; set; }

    [Column("type")]
    public int? Type { get; set; }

    [Column("datetime")]
    public DateTime? Datetime { get; set; }

    [Column("cardpay")]
    public decimal? Cardpay { get; set; }

    [Column("cardID")]
    public string? CardId { get; set; }

    [Column("zhifubao")]
    public decimal? Zhifubao { get; set; }

    [Column("weixin")]
    public decimal? Weixin { get; set; }

    [Column("yunfei")]
    public decimal? Yunfei { get; set; }

    /// <summary>
    /// 计算销售单原价总额、折后总额和应收金额
    /// </summary>
    public void CalculateTotal(List<DetailSell> details, decimal discountRate, decimal yunfei = 0)
    {
        Total = details.Sum(d => (d.Price ?? 0m) * (d.Amount ?? 0));
        BillTotal = Math.Round(Total.Value * discountRate, 2);
        TotalPayment = BillTotal + yunfei;
        BillPayment = BillTotal;
        DiscountRate = discountRate;
    }
}
