using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

[Table("xl_hygl")]
public class MemberCard
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("id")]
    public long Id { get; set; }

    [Column("kh")]
    public string? Kh { get; set; }

    [Column("klb")]
    public string? Klb { get; set; }

    [Column("kmm")]
    public string? Kmm { get; set; }

    [Column("khmc")]
    public string? Khmc { get; set; }

    [Column("lxr")]
    public string? Lxr { get; set; }

    [Column("tel")]
    public string? Tel { get; set; }

    [Column("kzsr")]
    public DateTime? Kzsr { get; set; }

    [Column("cp")]
    public string? Cp { get; set; }

    [Column("carname")]
    public string? Carname { get; set; }

    [Column("cartype")]
    public string? Cartype { get; set; }

    [Column("fdjh")]
    public string? Fdjh { get; set; }

    [Column("cjh")]
    public string? Cjh { get; set; }

    [Column("sprq")]
    public DateTime? Sprq { get; set; }

    [Column("bxrq")]
    public DateTime? Bxrq { get; set; }

    [Column("hyqx")]
    public DateTime? Hyqx { get; set; }

    [Column("je")]
    public decimal? Je { get; set; }

    [Column("khmc_py")]
    public string? KhmcPy { get; set; }

    [Column("zkl")]
    public decimal? Zkl { get; set; }

    [Column("zt")]
    public string? Zt { get; set; }

    [Column("nsrq")]
    public DateTime? Nsrq { get; set; }

    [Column("bd")]
    public long? Bd { get; set; }

    [Column("ykcs")]
    public decimal? Ykcs { get; set; }

    [Column("jyghrq")]
    public byte[]? Jyghrq { get; set; }
}
