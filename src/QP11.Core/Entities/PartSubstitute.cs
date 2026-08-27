using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

[Table("part_substitute")]
public class PartSubstitute
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("partid")]
    public long? Partid { get; set; }

    [Column("sub_partid")]
    public long? SubPartid { get; set; }

    [Column("memo")]
    public string? Memo { get; set; }
}
