using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

[Table("pays")]
public class Pays
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("bid")]
    public string? Bid { get; set; }

    [Column("sn")]
    public string? Sn { get; set; }

    [Column("pay")]
    public decimal? Je { get; set; }

    [Column("operator")]
    public string? Worker { get; set; }

    [Column("flag")]
    public int? Flag { get; set; }

    [Column("btype")]
    public int? Btype { get; set; }

    [Column("bz")]
    public int? Bz { get; set; }

    [Column("datetime")]
    public DateTime? Datetime { get; set; }

    [Column("account_id")]
    public long? AccountId { get; set; }

    [Column("type")]
    public string? Type { get; set; }

    [Column("memo")]
    public string? Memo { get; set; }
}
