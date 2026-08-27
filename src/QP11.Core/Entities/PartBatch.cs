using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

[Table("part_batch")]
public class PartBatch
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("partid")]
    public long? Partid { get; set; }

    [Column("batch_no")]
    public string? BatchNo { get; set; }

    [Column("supplier")]
    public string? Supplier { get; set; }

    [Column("amount")]
    public decimal? Amount { get; set; }

    [Column("remain")]
    public decimal? Remain { get; set; }

    [Column("inprice")]
    public decimal? Inprice { get; set; }

    [Column("produce_date")]
    public DateTime? ProduceDate { get; set; }

    [Column("expire_date")]
    public DateTime? ExpireDate { get; set; }

    [Column("datetime")]
    public DateTime? Datetime { get; set; }

    [Column("memo")]
    public string? Memo { get; set; }

    [Column("del")]
    public string? Del { get; set; }
}
