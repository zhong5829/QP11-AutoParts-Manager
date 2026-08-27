using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using QP11.Core.Entities;

namespace QP11.Core.Interfaces;

public interface ISysLogRepository : IRepository<SysLog>
{
    Task<(IEnumerable<SysLog> Data, int Total)> GetListAsync(int page = 1, int pageSize = 50, string? keyword = null, DateTime? startDate = null, DateTime? endDate = null, string? operatorName = null, string? action = null);
    Task<int> InsertAsync(SysLog log);
    Task<int> DeleteBeforeAsync(DateTime date);
}
