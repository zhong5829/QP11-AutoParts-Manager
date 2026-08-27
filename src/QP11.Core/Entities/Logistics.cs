using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

[Table("wuliu_infor")]
public class Logistics
{
    [Key]
    [Column("sid")]
    public string? Sid { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("address")]
    public string? Address { get; set; }

    [Column("linkman")]
    public string? Linkman { get; set; }

    [Column("tel")]
    public string? Tel { get; set; }

    [Column("fax")]
    public string? Fax { get; set; }

    [Column("mobile")]
    public string? Mobile { get; set; }

    [Column("zip")]
    public string? Zip { get; set; }

    [Column("level")]
    public string? Level { get; set; }

    [Column("credit")]
    public decimal? Credit { get; set; }

    [Column("bank")]
    public string? Bank { get; set; }

    [Column("tax")]
    public string? Tax { get; set; }

    [Column("class")]
    public string? Class { get; set; }

    [Column("name_py")]
    public string? NamePy { get; set; }
}
