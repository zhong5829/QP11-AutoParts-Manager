using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

[Table("part_data")]
public class PartData
{
    [Key]
    [Column("partid")]
    public long Partid { get; set; }

    [Column("partno")]
    public string? Partno { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("carname")]
    public string? Carname { get; set; }

    [Column("cartype")]
    public string? Cartype { get; set; }

    [Column("unit")]
    public string? Unit { get; set; }

    [Column("class")]
    public string? ClassName { get; set; }

    [Column("area")]
    public string? Area { get; set; }

    [Column("place")]
    public string? Place { get; set; }

    [Column("inprice")]
    public decimal? Inprice { get; set; }

    [Column("isck")]
    public long? Isck { get; set; }

    [Column("name_py")]
    public string? NamePy { get; set; }

    [Column("carname_py")]
    public string? CarnamePy { get; set; }

    [Column("cartype_py")]
    public string? CartypePy { get; set; }

    [Column("unit_py")]
    public string? UnitPy { get; set; }

    [Column("area_py")]
    public string? AreaPy { get; set; }

    [Column("memo")]
    public string? Memo { get; set; }

    [Column("DEL")]
    public string? Del { get; set; }

    [Column("name_bs")]
    public string? NameBs { get; set; }

    [Column("carname_bs")]
    public string? CarnameBs { get; set; }

    [Column("cartype_bs")]
    public string? CartypeBs { get; set; }

    [Column("unit_bs")]
    public string? UnitBs { get; set; }

    [Column("area_bs")]
    public string? AreaBs { get; set; }

    [Column("picture")]
    public byte[]? Picture { get; set; }

    [Column("part_th")]
    public string? PartTh { get; set; }

    [Column("part_gg")]
    public string? PartGg { get; set; }

    [Column("part_tm")]
    public string? PartTm { get; set; }

    [Column("part_cclb")]
    public string? PartCclb { get; set; }

    [Column("lsprice")]
    public decimal? Lsprice { get; set; }

    [Column("pfprice")]
    public decimal? Pfprice { get; set; }

    [Column("part_bzq")]
    public string? PartBzq { get; set; }

    [Column("part_bzrq")]
    public DateTime? PartBzrq { get; set; }

    [NotMapped]
    public string DisplayName => $"{Partno} - {Name}";

    [NotMapped]
    public bool IsDeleted => Del != "0";

    /// <summary>
    /// 库存数量（非数据库字段，由库存查询填充）
    /// </summary>
    [NotMapped]
    public long? StockAmount { get; set; }

    /// <summary>
    /// 库存预警下限（非数据库字段，由 part_stock.warning 填充）
    /// </summary>
    [NotMapped]
    public long? StockWarning { get; set; }

    /// <summary>
    /// 库存低于预警值（用于界面红色标记）
    /// </summary>
    [NotMapped]
    public bool IsLowStock => StockWarning.HasValue && StockWarning.Value > 0
        && StockAmount.HasValue && StockAmount.Value < StockWarning.Value;
}
