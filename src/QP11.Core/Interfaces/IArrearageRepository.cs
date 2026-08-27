using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using QP11.Core.Entities;

namespace QP11.Core.Interfaces;

public interface IArrearageRepository : IRepository<Arrearage>
{
    Task<IEnumerable<Arrearage>> GetByClientAsync(string cid);
    Task<IEnumerable<Arrearage>> GetListAsync(int? type = null, DateTime? startDate = null, DateTime? endDate = null);
    Task<decimal> GetClientArrearTotalAsync(string cid);
    new Task<int> InsertAsync(Arrearage entity, IDbTransaction? transaction = null);
    Task<IEnumerable<dynamic>> GetClientArrearageListAsync(int type, string? keyword = null);
    Task<IEnumerable<dynamic>> GetArrearageDetailByBidAsync(string bid, int? type = null);
    Task<int> UpdateChargeAsync(long id, decimal delta, IDbTransaction? transaction = null);
    Task<int> UpdatePaymentAsync(long id, decimal amount, string payMethod, IDbTransaction? transaction = null);

    /// <summary>
    /// 获取指定客户指定年份的按月往来汇总（进货/出货/应收应付/已结清）
    /// </summary>
    Task<IEnumerable<dynamic>> GetMonthlyTransactionSummaryAsync(string cid, int year);

    /// <summary>
    /// 获取指定年份有进货记录的客户列表（含欠款合计）
    /// </summary>
    Task<IEnumerable<dynamic>> GetTransactionClientsAsync(int year, string? keyword = null);

    /// <summary>
    /// 按关联单号删除欠款记录
    /// </summary>
    Task<int> DeleteBySnAsync(string sn, IDbTransaction? transaction = null);

    /// <summary>
    /// 按类型和日期范围查询欠款列表（含退货取反和未付金额计算）
    /// </summary>
    Task<IEnumerable<dynamic>> GetListWithCalcAsync(int? type = null, DateTime? startDate = null, DateTime? endDate = null);
}
