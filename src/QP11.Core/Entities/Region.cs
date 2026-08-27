using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

[Table("region")]
public class Region
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("parent_id")]
    public long? ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("code")]
    public string? Code { get; set; }

    [Column("sort")]
    public int? Sort { get; set; }
}
