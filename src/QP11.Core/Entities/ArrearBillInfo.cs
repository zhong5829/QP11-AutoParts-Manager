namespace QP11.Core.Entities;

/// <summary>
/// 挂账单据信息（用于一键做账）
/// </summary>
public class ArrearBillInfo
{
    public string Sn { get; set; } = "";
    public decimal Arrear { get; set; }
    public string ClientId { get; set; } = "";
}
