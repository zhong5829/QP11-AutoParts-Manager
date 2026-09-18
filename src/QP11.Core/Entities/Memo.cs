using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

/// <summary>备忘录条目（memo 表，按操作员隔离）</summary>
[Table("memo")]
public class Memo
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    /// <summary>操作员账号，用于数据隔离</summary>
    [Column("operator")]
    public string? Operator { get; set; }

    /// <summary>备忘内容</summary>
    [Column("content")]
    public string? Content { get; set; }

    /// <summary>优先级：1低 2中 3高</summary>
    [Column("priority")]
    public int Priority { get; set; } = 1;

    /// <summary>是否已完成（勾选）</summary>
    [Column("done")]
    public bool Done { get; set; }

    /// <summary>提醒类型：0不提醒 1定时 2倒计时</summary>
    [Column("remind_type")]
    public int RemindType { get; set; }

    /// <summary>目标提醒时间（定时=所选时刻；倒计时=录入时当前时间+指定分钟）</summary>
    [Column("remind_at")]
    public DateTime? RemindAt { get; set; }

    /// <summary>倒计时原始分钟数（用于回显）</summary>
    [Column("countdown_minutes")]
    public int? CountdownMinutes { get; set; }

    /// <summary>该条是否已弹过提醒（防重复，多机抢占后置位）</summary>
    [Column("reminded")]
    public bool Reminded { get; set; }

    /// <summary>创建时间</summary>
    [Column("create_time")]
    public DateTime? CreateTime { get; set; }

    /// <summary>完成时间</summary>
    [Column("done_time")]
    public DateTime? DoneTime { get; set; }
}