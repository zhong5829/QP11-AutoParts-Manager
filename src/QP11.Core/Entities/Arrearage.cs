using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

[Table("arrearage")]
public class Arrearage
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("bid")]
    public string? Bid { get; set; }

    [Column("sn")]
    public string? Sn { get; set; }

    [Column("total")]
    public decimal? Total { get; set; }

    [Column("charge")]
    public decimal? Charge { get; set; }

    [Column("operator")]
    public string? Operator { get; set; }

    [Column("type")]
    public int? Type { get; set; }

    [Column("btype")]
    public int? Btype { get; set; }

    [Column("datetime")]
    public DateTime? Datetime { get; set; }
}
