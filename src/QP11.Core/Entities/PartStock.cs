using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

[Table("part_stock")]
public class PartStock
{
    [Key]
    [Column("partid")]
    public long Partid { get; set; }

    [Column("place")]
    public string? Place { get; set; }

    [Column("amount")]
    public long? Amount { get; set; }

    [Column("warning")]
    public long? Warning { get; set; }

    [Column("lsprice")]
    public decimal? Lsprice { get; set; }

    [Column("pfprice")]
    public decimal? Pfprice { get; set; }

    [Column("upflag")]
    public string? Upflag { get; set; }

    [Column("sell_use")]
    public decimal? SellUse { get; set; }

    [Column("buy_use")]
    public decimal? BuyUse { get; set; }

    [Column("rowversion")]
    [Timestamp]
    public byte[]? RowVersion { get; set; }
}
