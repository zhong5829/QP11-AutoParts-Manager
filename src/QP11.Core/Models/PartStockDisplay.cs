using System;

namespace QP11.Core.Models;

public class PartStockDisplay
{
    public long PartId { get; set; }
    public string? PartNo { get; set; }
    public string? Name { get; set; }
    public string? CarType { get; set; }
    public string? CarName { get; set; }
    public string? Place { get; set; }
    public string? Unit { get; set; }
    public string? Class { get; set; }
    public string? Area { get; set; }
    public decimal? InPrice { get; set; }
    public long? Amount { get; set; }
    public decimal? LsPrice { get; set; }
    public decimal? PfPrice { get; set; }
    public decimal? SellUse { get; set; }
    public string? Memo { get; set; }
    public long? Isck { get; set; }
    public long? Warning { get; set; }
    public string? PartTh { get; set; }
    public string? PartGg { get; set; }
    public string? PartCclb { get; set; }
    public string? NamePy { get; set; }
    public string? CartypePy { get; set; }
    public string? PartBzq { get; set; }
    public DateTime? PartBzrq { get; set; }
}
