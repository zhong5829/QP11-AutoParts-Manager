using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;

namespace QP11.Core.Interfaces;

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(object id);
    Task<IEnumerable<T>> GetAllAsync();
    Task<int> InsertAsync(T entity, IDbTransaction? transaction = null);
    Task<int> UpdateAsync(T entity, IDbTransaction? transaction = null);
    Task<int> DeleteAsync(object id, IDbTransaction? transaction = null);
    Task<int> CountAsync();
}
