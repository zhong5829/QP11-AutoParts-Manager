using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using QP11.Core.Entities;

namespace QP11.Core.Interfaces;

public interface IBuyRepository : IRepository<BillBuy>
{
    Task<BillBuy?> GetBySnAsync(string sn, IDbTransaction? transaction = null);
    Task<IEnumerable<BillBuy>> GetListAsync(DateTime? startDate = null, DateTime? endDate = null);
    Task<IEnumerable<BillBuy>> GetListByFlagAsync(int flag);
    Task<IEnumerable<DetailBuy>> GetDetailsAsync(string sn);
    Task<int> InsertBillAsync(BillBuy bill, IDbTransaction? transaction = null);
    Task<int> InsertDetailsAsync(IEnumerable<DetailBuy> details, IDbTransaction? transaction = null);
    Task<int> InsertDetailAsync(DetailBuy detail, IDbTransaction? transaction = null);
    Task<int> UpdateAsync(BillBuy bill);
    new Task<int> UpdateAsync(BillBuy bill, IDbTransaction? transaction);
    Task<int> UpdateBillStatusAsync(string sn, int flag, IDbTransaction? transaction = null);
    /// <summary>仅更新采购单头备注（明细清单窗口回车保存用）</summary>
    Task<int> UpdateBillMemoAsync(string sn, string? memo);
    /// <summary>仅更新采购明细行备注（明细清单窗口回车保存用）</summary>
    Task<int> UpdateDetailMemoAsync(long id, string? memo);
    Task<IEnumerable<dynamic>> GetBuyDetailsByPartIdAsync(long partid);
    Task<IEnumerable<dynamic>> GetDetailListAsync(DateTime? startDate = null, DateTime? endDate = null, string? supplier = null, string? worker = null);
    Task<IEnumerable<dynamic>> GetBillListAsync(DateTime? startDate = null, DateTime? endDate = null, string? supplier = null, string? worker = null);
    /// <summary>物理删除采购单头（作废单据时与明细、欠款在同一事务内删除）</summary>
    Task<int> DeleteBillAsync(string sn, IDbTransaction? transaction = null);
    Task<int> DeleteDetailsBySnAsync(string sn, IDbTransaction? transaction = null);
    Task<string?> GetWorkerNameAsync(string workid);
    Task<string> ResolveWorkerIdAsync(string workerName);
}
