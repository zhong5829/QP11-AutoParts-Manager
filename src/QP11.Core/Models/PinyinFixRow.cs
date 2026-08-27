namespace QP11.Core.Models;

/// <summary>拼音缺失记录，用于拼音修复功能</summary>
public class PinyinFixRow
{
    public long PartId { get; set; }
    public string? Partno { get; set; }
    public string? Name { get; set; }
    public string? NamePy { get; set; }
    public string? Cartype { get; set; }
    public string? CartypePy { get; set; }
}
