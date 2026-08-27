using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

[Table("reminder")]
public class Reminder
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("type")]
    public string? Type { get; set; }

    [Column("target_id")]
    public string? TargetId { get; set; }

    [Column("target_name")]
    public string? TargetName { get; set; }

    [Column("content")]
    public string? Content { get; set; }

    [Column("remind_date")]
    public DateTime? RemindDate { get; set; }

    [Column("status")]
    public string? Status { get; set; }

    [Column("datetime")]
    public DateTime? Datetime { get; set; }

    [Column("memo")]
    public string? Memo { get; set; }
}
