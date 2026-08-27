using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

[Table("xl_gjgl")]
public class Borrow
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("gjbh")]
    public string? Gjbh { get; set; }

    [Column("gjmc")]
    public string? Gjmc { get; set; }

    [Column("bz")]
    public string? Bz { get; set; }

    [Column("jybz")]
    public string? Jybz { get; set; }

    [Column("jyr")]
    public string? Jyr { get; set; }

    [Column("jyrq")]
    public DateTime? Jyrq { get; set; }

    [Column("ghrq")]
    public DateTime? Ghrq { get; set; }

    [Column("zt")]
    public string? Zt { get; set; }

    [Column("gjjz")]
    public decimal? Gjjz { get; set; }

    [Column("gjmc_py")]
    public string? GjmcPy { get; set; }

    [Column("jybz_py")]
    public string? JybzPy { get; set; }

    [Column("jyr_py")]
    public string? JyrPy { get; set; }
}
