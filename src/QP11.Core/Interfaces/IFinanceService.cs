using System.Collections.Generic;
using System.Threading.Tasks;

namespace QP11.Core.Interfaces;

public interface IFinanceService
{
    Task<int> ReceivePaymentAsync(long accountId, decimal amount, string sn, string memo = "");
    Task<int> PaySupplierAsync(long accountId, decimal amount, string sn, string memo = "");
    Task<decimal> GetClientArrearTotalAsync(string clientId);

    /// <summary>
    /// 批量确认欠款到账：事务内更新arrearage.charge + 关联单据挂账 + 收支记录
    /// </summary>
    Task ConfirmArrearagePaymentAsync(decimal totalAmount, IEnumerable<(long Id, decimal Amount)> payments, string payMethod, string memo = "");
}
