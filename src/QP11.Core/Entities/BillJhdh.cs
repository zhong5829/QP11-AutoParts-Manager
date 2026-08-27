using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

[Table("bill_jhdh")]
public class BillJhdh
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

    [Column("total")]
    public decimal? Total { get; set; }

    [Column("datetime")]
    public DateTime? Datetime { get; set; }

    [Column("memo")]
    public string? Memo { get; set; }

    [Column("flag")]
    public int? Flag { get; set; }
}

[Table("detail_jhdh")]
public class DetailJhdh
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

    [Column("carname")]
    public string? Carname { get; set; }

    [Column("cartype")]
    public string? Cartype { get; set; }

    [Column("unit")]
    public string? Unit { get; set; }

    [Column("amount")]
    public long? Amount { get; set; }

    [Column("price")]
    public decimal? Price { get; set; }

    [Column("total")]
    public decimal? Total { get; set; }

    [Column("wayed")]
    public long? Wayed { get; set; }

    [Column("waying")]
    public long? Waying { get; set; }

    [Column("lsprice")]
    public decimal? Lsprice { get; set; }

    [Column("pfprice")]
    public decimal? Pfprice { get; set; }

    [Column("flag")]
    public int? Flag { get; set; }

    [Column("memo")]
    public string? Memo { get; set; }

    [Column("datetime")]
    public DateTime? Datetime { get; set; }
}
