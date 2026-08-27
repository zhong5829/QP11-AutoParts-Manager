using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

[Table("account")]
public class Account
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("sn")]
    public string? Sn { get; set; }

    [Column("charge")]
    public decimal? Je { get; set; }

    [Column("type")]
    public string? Type { get; set; }

    [Column("operator")]
    public string? Operator { get; set; }

    [Column("flag")]
    public int? Flag { get; set; }

    [Column("btype")]
    public int? Btype { get; set; }

    [Column("memo")]
    public string? Memo { get; set; }

    [Column("bz")]
    public int? Bz { get; set; }

    [Column("datetime")]
    public DateTime? Datetime { get; set; }

    [Column("wuliu_sid")]
    public string? WuliuSid { get; set; }

    [Column("wuliu_danhao")]
    public string? WuliuDanhao { get; set; }
}
