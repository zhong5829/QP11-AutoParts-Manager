using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

[Table("detail_buy")]
public class DetailBuy
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

    [Column("carname")]
    public string? Carname { get; set; }

    [Column("cartype")]
    public string? Cartype { get; set; }

    [Column("inprice")]
    public decimal? Inprice { get; set; }

    [Column("intotal")]
    public decimal? Stotal { get; set; }

    [Column("pfprice")]
    public decimal? Pfprice { get; set; }

    [Column("lsprice")]
    public decimal? Lsprice { get; set; }

    [Column("place")]
    public string? Place { get; set; }

    [Column("class")]
    public string? Class { get; set; }

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
}
