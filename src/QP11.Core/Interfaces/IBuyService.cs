using System.Collections.Generic;
using System.Threading.Tasks;
using QP11.Core.Entities;

namespace QP11.Core.Interfaces;

public interface IBuyService
{
    Task<string> CreateBuyOrderAsync(BillBuy bill, List<DetailBuy> details, decimal credit = 0);
    Task ConfirmStockInAsync(string sn, List<DetailBuy> details);
    Task<string> CreateBuyReturnAsync(string supplierId, string? supplierName, List<BuyReturnDetail> returnDetails);
}

/// <summary>
/// 采购退货明细传输对象
/// </summary>
public class BuyReturnDetail
{
    public long? PartId { get; set; }
    public string? PartNo { get; set; }
    public string? PartName { get; set; }
    public string? Cartype { get; set; }
    public decimal InPrice { get; set; }
    public long ReturnAmount { get; set; }
    public string? SourceSn { get; set; }
}
