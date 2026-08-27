using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

[Table("part_image")]
public class PartImage
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("partid")]
    public long? Partid { get; set; }

    [Column("image_path")]
    public string? ImagePath { get; set; }

    [Column("sort")]
    public int? Sort { get; set; }

    [Column("memo")]
    public string? Memo { get; set; }
}
