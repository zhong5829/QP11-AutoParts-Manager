using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace QP11.Core.Entities;

[Table("shipping_order")]
public class ShippingOrder
{
    [Key]
    [Column("sn")]
    public string? Sn { get; set; }

    [Column("sell_sn")]
    public string? SellSn { get; set; }

    [Column("client")]
    public string? Client { get; set; }

    [Column("address")]
    public string? Address { get; set; }

    [Column("logistics")]
    public string? Logistics { get; set; }

    [Column("logistics_no")]
    public string? LogisticsNo { get; set; }

    [Column("worker")]
    public string? Worker { get; set; }

    [Column("flag")]
    public int? Flag { get; set; }

    [Column("datetime")]
    public DateTime? Datetime { get; set; }

    [Column("memo")]
    public string? Memo { get; set; }

    [Column("del")]
    public string? Del { get; set; }
}
