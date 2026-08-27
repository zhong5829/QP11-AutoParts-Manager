using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

/// <summary>
/// 报损单实体 - 映射到 bill_sell 表，通过 flag=3 区分报损记录
/// 原始 PB 系统中报损功能使用 bill_sell 表，flag=3 标识报损
/// </summary>
[Table("bill_sell")]
public class BillBaosun
{
    [Key]
    [Column("sn")]
    public string? Sn { get; set; }

    [Column("client")]
    public string? Client { get; set; }

    [Column("worker")]
    public string? Worker { get; set; }

    [Column("operator")]
    public string? Operator { get; set; }

    [Column("total")]
    public decimal? Total { get; set; }

    [Column("datetime")]
    public DateTime? Datetime { get; set; }

    [Column("memo")]
    public string? Memo { get; set; }

    [Column("flag")]
    public int? Flag { get; set; }

    [Column("type")]
    public int? Type { get; set; }
}

/// <summary>
/// 报损明细实体 - 映射到 detail_sell 表
/// 进价存储在 price 字段，小计存储在 stotal 字段
/// </summary>
[Table("detail_sell")]
public class DetailBaosun
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("sn")]
    public string? Sn { get; set; }

    [Column("partid")]
    public long? Partid { get; set; }

    [Column("partno")]
    public string? Partno { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("amount")]
    public long? Amount { get; set; }

    [Column("unit")]
    public string? Unit { get; set; }

    /// <summary>进价 - 映射到 detail_sell.price 列</summary>
    [Column("price")]
    public decimal? Inprice { get; set; }

    /// <summary>小计 - 映射到 detail_sell.stotal 列</summary>
    [Column("stotal")]
    public decimal? Intotal { get; set; }

    [Column("memo")]
    public string? Memo { get; set; }

    [Column("datetime")]
    public DateTime? Datetime { get; set; }

    /// <summary>车型 - 映射到 detail_sell.cartype 列</summary>
    [Column("cartype")]
    public string? Cartype { get; set; }

    /// <summary>成本 - 映射到 detail_sell.cb 列</summary>
    [Column("cb")]
    public decimal? Cb { get; set; }
}
