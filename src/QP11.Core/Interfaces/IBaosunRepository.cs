using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QP11.Core.Entities;

namespace QP11.Core.Interfaces;

public interface IBaosunRepository
{
    Task<IEnumerable<BillBaosun>> GetListAsync(DateTime? startDate = null, DateTime? endDate = null);
    Task<BillBaosun?> GetBySnAsync(string sn);
    Task<IEnumerable<DetailBaosun>> GetDetailsAsync(string sn);
    Task<int> InsertBillAsync(BillBaosun bill);
    Task<int> InsertDetailAsync(DetailBaosun detail);
    Task<int> UpdateBillStatusAsync(string sn, int flag);
}
