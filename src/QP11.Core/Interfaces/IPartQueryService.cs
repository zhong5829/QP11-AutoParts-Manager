using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace QP11.Core.Interfaces;

public interface IPartQueryService
{
    Task<IReadOnlyList<SellHistoryItem>> GetSellHistoryAsync(long partId, string? clientName = null, int top = 20, CancellationToken ct = default);

    Task<IReadOnlyList<BuyHistoryItem>> GetBuyHistoryAsync(long partId, int top = 20, CancellationToken ct = default);

    Task<PriceRangeResult> GetPriceRangeAsync(long partId, CancellationToken ct = default);
}

public sealed class SellHistoryItem
{
    public string? Sn { get; set; }
    public decimal Amount { get; set; }
    public decimal Price { get; set; }
    public decimal BillPrice { get; set; }
    public System.DateTime? Datetime { get; set; }
    public string? ClientName { get; set; }
}

public sealed class BuyHistoryItem
{
    public string? Sn { get; set; }
    public decimal Amount { get; set; }
    public decimal Inprice { get; set; }
    public System.DateTime? Datetime { get; set; }
    public string? SupplierName { get; set; }
}

public sealed class PriceRangeResult
{
    public decimal MaxPrice { get; set; }
    public decimal MinPrice { get; set; }
}
