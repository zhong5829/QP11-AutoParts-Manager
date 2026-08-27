using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

[Table("CLASSES")]
public class PartClass
{
    [Key]
    [Column("CLASS_TYPE")]
    public string? ClassId { get; set; }

    [Column("CLASS_NO")]
    public string? ClassNo { get; set; }

    [Column("CLASS_NM")]
    public string? ClassName { get; set; }

    [Column("CLASS_EN")]
    public string? ClassEn { get; set; }

    [Column("CLASS_TYPE_NM")]
    public string? ClassTypeNm { get; set; }

    [Column("CLASS_NOTE")]
    public string? ClassNote { get; set; }

    [Column("NOUSE_MK")]
    public string? NouseMk { get; set; }
}
