using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using QP11.Core.Entities;
using QP11.Core.Models;

namespace QP11.Core.Interfaces;

public interface ISellRepository : IRepository<BillSell>
{
    Task<BillSell?> GetBySnAsync(string sn);
    Task<IEnumerable<BillSell>> GetListAsync(DateTime? startDate = null, DateTime? endDate = null, string? client = null);
    Task<IEnumerable<DetailSell>> GetDetailsAsync(string sn);
    Task<int> InsertBillAsync(BillSell bill, IDbTransaction? transaction = null);
    Task<int> InsertDetailAsync(DetailSell detail, IDbTransaction? transaction = null);
    Task<int> InsertDetailsAsync(IEnumerable<DetailSell> details, IDbTransaction? transaction = null);
    Task<int> UpdateAsync(BillSell bill);
    new Task<int> UpdateAsync(BillSell bill, IDbTransaction? transaction);
    Task<int> UpdateBillStatusAsync(string sn, int flag, IDbTransaction? transaction = null);
    Task<int> UpdateMemoAsync(string sn, string memo);
    Task<int> LogicDeleteBillAsync(string sn);
    /// <summary>物理删除销售单头（作废单据时与明细、欠款在同一事务内删除）</summary>
    Task<int> DeleteBillAsync(string sn, IDbTransaction? transaction = null);
    Task<int> DeleteDetailsAsync(string sn);
    Task<int> DeleteDetailsAsync(string sn, IDbTransaction? transaction);
    Task<IEnumerable<dynamic>> GetDetailListAsync(DateTime? startDate = null, DateTime? endDate = null, string? client = null, string? worker = null);
    /// <summary>获取今日配件销售排行（按销量降序，top<=0 显示全部，含实时库存）</summary>
    Task<IEnumerable<dynamic>> GetTodayPartsRankingAsync(DateTime today, int top = 0);
    Task<IEnumerable<ArrearBillInfo>> GetArrearBillsAsync(IEnumerable<string> sns);
    Task<IEnumerable<ArrearBillInfo>> GetArrearBillsAllAsync(IEnumerable<string> sns);
    Task<int> BatchSettleArrearAsync(IEnumerable<string> sns, string payMethod);
    Task<(IEnumerable<dynamic> Data, int Total)> GetPagedOrdersAsync(DateTime? start, DateTime? end, string? client, int page, int pageSize);
}
