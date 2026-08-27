using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using QP11.Core.Entities;

namespace QP11.Core.Interfaces;

public interface IPaysRepository : IRepository<Pays>
{
    Task<IEnumerable<Pays>> GetByAccountAsync(long accountId, DateTime? startDate = null, DateTime? endDate = null);
    new Task<int> InsertAsync(Pays entity, IDbTransaction? transaction = null);
}
