using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using QP11.Core.Entities;

namespace QP11.Core.Interfaces;

public interface IAccountRepository : IRepository<Account>
{
    new Task<int> InsertAsync(Account entity, IDbTransaction? transaction = null);
    Task<int> UpdateAsync(Account entity);
    Task<int> UpdateBalanceAsync(long id, decimal amount, IDbTransaction? transaction = null);
    Task<IEnumerable<dynamic>> GetIncomeExpenseListAsync(DateTime? startDate = null, DateTime? endDate = null, int? flag = null);
}
