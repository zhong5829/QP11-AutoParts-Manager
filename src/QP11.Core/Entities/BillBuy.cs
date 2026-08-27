using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

[Table("bill_buy")]
public class BillBuy
{
    [Key]
    [Column("sn")]
    public string? Sn { get; set; }

    [Column("supplier")]
    public string? Supplier { get; set; }

    [Column("worker")]
    public string? Worker { get; set; }

    [Column("operator")]
    public string? Operator { get; set; }

    [Column("invoice")]
    public string? Invoice { get; set; }

    [Column("memo")]
    public string? Memo { get; set; }

    [Column("flag")]
    public int? Flag { get; set; }

    [Column("type")]
    public int? Type { get; set; }

    [Column("datetime")]
    public DateTime? Datetime { get; set; }

    [Column("total")]
    public decimal? Total { get; set; }

    [Column("cash")]
    public decimal? Cash { get; set; }

    [Column("checks")]
    public decimal? Checks { get; set; }

    [Column("arrear")]
    public decimal? Arrear { get; set; }

    [Column("zhifubao")]
    public decimal? Zhifubao { get; set; }

    [Column("weixin")]
    public decimal? Weixin { get; set; }

    [Column("yunfei")]
    public decimal? Yunfei { get; set; }

    [Column("bill_total")]
    public decimal? BillTotal { get; set; }
}
