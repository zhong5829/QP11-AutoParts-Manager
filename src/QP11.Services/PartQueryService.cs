using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using QP11.Core.Interfaces;

namespace QP11.Services;

public sealed class PartQueryService : IPartQueryService
{
    private readonly IDbConnectionFactory _dbFactory;

    public PartQueryService(IDbConnectionFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IReadOnlyList<SellHistoryItem>> GetSellHistoryAsync(
        long partId, string? clientName = null, int top = 20, CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateAsync();

        string sql;
        object param;
        if (!string.IsNullOrEmpty(clientName))
        {
            sql = $@"SELECT TOP {top} detail_sell.sn AS Sn, detail_sell.amount AS Amount,
                    ISNULL(detail_sell.price, 0) AS Price,
                    ISNULL(detail_sell.bill_price, detail_sell.price) AS BillPrice,
                    detail_sell.datetime AS Datetime,
                    CASE WHEN ISNULL(bill_sell.flag, 0) = 3 THEN '配件报损' ELSE client_infor.name END AS ClientName
                    FROM detail_sell
                    LEFT JOIN bill_sell ON bill_sell.sn = detail_sell.sn
                    LEFT JOIN client_infor ON client_infor.cid = bill_sell.client
                    WHERE detail_sell.partid = @PartId AND client_infor.name = @ClientName
                    ORDER BY detail_sell.datetime DESC";
            param = new { PartId = partId, ClientName = clientName };
        }
        else
        {
            sql = $@"SELECT TOP {top} detail_sell.sn AS Sn, detail_sell.amount AS Amount,
                    ISNULL(detail_sell.price, 0) AS Price,
                    ISNULL(detail_sell.bill_price, detail_sell.price) AS BillPrice,
                    detail_sell.datetime AS Datetime,
                    CASE WHEN ISNULL(bill_sell.flag, 0) = 3 THEN '配件报损' ELSE client_infor.name END AS ClientName
                    FROM detail_sell
                    LEFT JOIN bill_sell ON bill_sell.sn = detail_sell.sn
                    LEFT JOIN client_infor ON client_infor.cid = bill_sell.client
                    WHERE detail_sell.partid = @PartId
                    ORDER BY detail_sell.datetime DESC";
            param = new { PartId = partId };
        }

        var rows = await db.QueryAsync<SellHistoryItem>(sql, param);
        return rows.AsList();
    }

    public async Task<IReadOnlyList<BuyHistoryItem>> GetBuyHistoryAsync(
        long partId, int top = 20, CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateAsync();
        var sql = $@"SELECT TOP {top} detail_buy.sn AS Sn, detail_buy.amount AS Amount, detail_buy.inprice AS Inprice,
                    detail_buy.datetime AS Datetime,
                    supplier_infor.name AS SupplierName
                    FROM detail_buy
                    LEFT JOIN bill_buy ON bill_buy.sn = detail_buy.sn
                    LEFT JOIN supplier_infor ON supplier_infor.sid = bill_buy.supplier
                    WHERE detail_buy.partid = @PartId
                    ORDER BY detail_buy.datetime DESC";
        var rows = await db.QueryAsync<BuyHistoryItem>(sql, new { PartId = partId });
        return rows.AsList();
    }

    public async Task<PriceRangeResult> GetPriceRangeAsync(long partId, CancellationToken ct = default)
    {
        using var db = await _dbFactory.CreateAsync();
        var row = await db.QueryFirstOrDefaultAsync<dynamic>(
            @"SELECT MAX(detail_sell.price) AS MaxPrice, MIN(detail_sell.price) AS MinPrice
              FROM detail_sell WHERE detail_sell.partid = @PartId",
            new { PartId = partId });

        if (row == null) return new PriceRangeResult();

        return new PriceRangeResult
        {
            MaxPrice = row.MaxPrice is null ? 0m : (decimal)row.MaxPrice,
            MinPrice = row.MinPrice is null ? 0m : (decimal)row.MinPrice
        };
    }
}
