using System.Collections.Generic;
using System.Threading.Tasks;
using QP11.Core.Entities;

namespace QP11.Core.Interfaces;

public interface ILogisticsRepository
{
    Task<IEnumerable<Logistics>> GetAllAsync();
    Task<Logistics?> GetByIdAsync(string sid);
    Task<int> InsertAsync(Logistics entity);
    Task<int> UpdateAsync(Logistics entity);
    Task<int> DeleteAsync(string sid);
}
