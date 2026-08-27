using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

[Table("detail_sell")]
public class DetailSell
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

    [Column("unit")]
    public string? Unit { get; set; }

    [Column("place")]
    public string? Place { get; set; }

    [Column("amount")]
    public long? Amount { get; set; }

    [Column("amount2")]
    public long? Amount2 { get; set; }

    [Column("price")]
    public decimal? Price { get; set; }

    [Column("bill_price")]
    public decimal? BillPrice { get; set; }

    [Column("stotal")]
    public decimal? Stotal { get; set; }

    [Column("btotal")]
    public decimal? Btotal { get; set; }

    [Column("cartype")]
    public string? Cartype { get; set; }

    [Column("area")]
    public string? Area { get; set; }

    [Column("car_mark")]
    public string? CarMark { get; set; }

    [Column("memo")]
    public string? Memo { get; set; }

    [Column("tsn")]
    public string? Tsn { get; set; }

    [Column("type")]
    public int? Type { get; set; }

    [Column("flag")]
    public int? Flag { get; set; }

    [Column("datetime")]
    public DateTime? Datetime { get; set; }

    [Column("cb")]
    public decimal? Cb { get; set; }

    [Column("part_gg")]
    public string? PartGg { get; set; }

    [Column("part_th")]
    public string? PartTh { get; set; }

    [Column("part_cclb")]
    public string? PartCclb { get; set; }

    [Column("part_bzq")]
    public string? PartBzq { get; set; }

    [Column("part_bzrq")]
    public DateTime? PartBzrq { get; set; }

    [Column("discount_rate")]
    public decimal? DiscountRate { get; set; }

    /// <summary>
    /// 根据单据类型设置明细行 flag
    /// </summary>
    public void ApplyFlag(bool isReturn, bool isExchange = false)
    {
        if (isExchange)
            Flag = 3; // 换货
        else if (isReturn)
            Flag = 2; // 退货
        else
            Flag = 1; // 正常销售
    }
}
