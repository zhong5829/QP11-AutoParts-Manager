using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

[Table("car_mark")]
public class CarMark
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("carname")]
    public string? Carname { get; set; }

    [Column("cartype")]
    public string? Cartype { get; set; }

    [Column("engine")]
    public string? Engine { get; set; }

    [Column("carframe")]
    public string? Carframe { get; set; }

    [Column("picture")]
    public byte[]? Picture { get; set; }

    [Column("linkman")]
    public string? Linkman { get; set; }

    [Column("tel")]
    public string? Tel { get; set; }

    [Column("memo")]
    public string? Memo { get; set; }

    [Column("name_py")]
    public string? NamePy { get; set; }

    [Column("client_cid")]
    public string? ClientCid { get; set; }
}
