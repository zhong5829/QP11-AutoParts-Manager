using System.Collections.Generic;
using System.Threading.Tasks;
using QP11.Core.Entities;

namespace QP11.Core.Interfaces;

public interface ISellService
{
    Task<string> CreateSellOrderAsync(BillSell bill, List<DetailSell> details, decimal cash, decimal weixin, decimal zhifubao, decimal memberPay, string? memberCardNo = null);
    Task VoidSellOrderAsync(string sn, List<DetailSell> details);
}
