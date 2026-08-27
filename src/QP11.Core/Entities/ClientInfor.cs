using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using QP11.Core.Exceptions;

namespace QP11.Core.Entities;

[Table("client_infor")]
public class ClientInfor
{
    [Key]
    [Column("cid")]
    public string? Cid { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("address")]
    public string? Address { get; set; }

    [Column("linkman")]
    public string? Linkman { get; set; }

    [Column("tel")]
    public string? Tel { get; set; }

    [Column("fax")]
    public string? Fax { get; set; }

    [Column("mobile")]
    public string? Mobile { get; set; }

    [Column("zip")]
    public string? Zip { get; set; }

    [Column("level")]
    public string? Level { get; set; }

    [Column("credit")]
    public decimal? Credit { get; set; }

    [Column("bank")]
    public string? Bank { get; set; }

    [Column("tax")]
    public string? Tax { get; set; }

    [Column("class")]
    public string? Class { get; set; }

    [Column("name_py")]
    public string? NamePy { get; set; }

    [Column("jyfw")]
    public string? Jyfw { get; set; }

    [Column("note")]
    public string? Note { get; set; }

    [Column("bank1")]
    public string? Bank1 { get; set; }

    [Column("bank2")]
    public string? Bank2 { get; set; }

    [Column("sell_use")]
    public decimal? SellUse { get; set; }

    /// <summary>
    /// 验证客户折扣率是否超出限制
    /// </summary>
    /// <exception cref="BusinessRuleException">超出折扣限制时抛出</exception>
    public void ValidateDiscount(decimal requestedDiscount)
    {
        decimal maxAllowed;
        switch (Level)
        {
            case "VIP": maxAllowed = 0.70m; break;
            case "普通": maxAllowed = 0.85m; break;
            default: maxAllowed = 0.95m; break;
        }

        if (requestedDiscount > maxAllowed)
        {
            throw new BusinessRuleException(
                $"超出{Level}客户的最大支付比例限制({maxAllowed:P0}，即最低{maxAllowed:P0}折)");
        }
    }
}
