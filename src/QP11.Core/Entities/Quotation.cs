using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

[Table("quotation")]
public class Quotation
{
    [Key]
    [Column("sn")]
    public string? Sn { get; set; }

    [Column("client")]
    public string? Client { get; set; }

    [Column("worker")]
    public string? Worker { get; set; }

    [Column("total")]
    public decimal? Total { get; set; }

    [Column("flag")]
    public int? Flag { get; set; }

    [Column("datetime")]
    public DateTime? Datetime { get; set; }

    [Column("memo")]
    public string? Memo { get; set; }

    [Column("del")]
    public string? Del { get; set; }
}

[Table("quotation_detail")]
public class QuotationDetail
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("sn")]
    public string? Sn { get; set; }

    [Column("partid")]
    public long? Partid { get; set; }

    [Column("amount")]
    public decimal? Amount { get; set; }

    [Column("price")]
    public decimal? Price { get; set; }

    [Column("stotal")]
    public decimal? Stotal { get; set; }

    [Column("memo")]
    public string? Memo { get; set; }
}
