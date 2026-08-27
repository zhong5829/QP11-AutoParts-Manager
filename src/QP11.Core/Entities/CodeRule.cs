using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

[Table("code_rule")]
public class CodeRule
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("table_name")]
    public string? TableName { get; set; }

    [Column("prefix")]
    public string? Prefix { get; set; }

    [Column("date_format")]
    public string? DateFormat { get; set; }

    [Column("seq_length")]
    public int? SeqLength { get; set; }

    [Column("current_seq")]
    public int? CurrentSeq { get; set; }

    [Column("reset_daily")]
    public string? ResetDaily { get; set; }

    [Column("memo")]
    public string? Memo { get; set; }
}
