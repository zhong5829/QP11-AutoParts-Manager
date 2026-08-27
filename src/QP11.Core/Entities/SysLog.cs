using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

[Table("sys_log")]
public class SysLog
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("operator")]
    public string? Operator { get; set; }

    [Column("module")]
    public string? Module { get; set; }

    [Column("action")]
    public string? Action { get; set; }

    [Column("datetime")]
    public DateTime? Datetime { get; set; }
}
