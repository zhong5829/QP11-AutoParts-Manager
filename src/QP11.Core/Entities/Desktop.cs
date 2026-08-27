using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

[Table("desktop")]
public class Desktop
{
    [Key]
    [Column("code")]
    public string? Code { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("buildtime")]
    public DateTime? Buildtime { get; set; }

    [Column("memo")]
    public string? Memo { get; set; }

    [Column("username")]
    public string? Username { get; set; }
}
