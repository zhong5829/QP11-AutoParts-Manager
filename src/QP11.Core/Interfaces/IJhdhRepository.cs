using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using QP11.Core.Entities;

namespace QP11.Core.Interfaces;

public interface IJhdhRepository : IRepository<BillJhdh>
{
    Task<BillJhdh?> GetBySnAsync(string sn, IDbTransaction? transaction = null);
    Task<IEnumerable<BillJhdh>> GetListAsync(DateTime? startDate = null, DateTime? endDate = null);
    Task<IEnumerable<BillJhdh>> GetListByFlagAsync(int flag);
    Task<IEnumerable<DetailJhdh>> GetDetailsAsync(string sn);
    Task<int> InsertBillAsync(BillJhdh bill, IDbTransaction? transaction = null);
    Task<int> InsertDetailsAsync(IEnumerable<DetailJhdh> details, IDbTransaction? transaction = null);
    Task<int> InsertDetailAsync(DetailJhdh detail, IDbTransaction? transaction = null);
    Task<int> UpdateAsync(BillJhdh bill);
    new Task<int> UpdateAsync(BillJhdh bill, IDbTransaction? transaction);
    Task<int> UpdateBillStatusAsync(string sn, int flag, IDbTransaction? transaction = null);
    Task<int> DeleteDetailsBySnAsync(string sn, IDbTransaction? transaction = null);
}
