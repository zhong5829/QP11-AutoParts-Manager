using System.Collections.Generic;
using System.Threading.Tasks;
using QP11.Core.Entities;

namespace QP11.Core.Interfaces;

public interface IBorrowRepository : IRepository<Borrow>
{
    Task<IEnumerable<Borrow>> GetByStatusAsync(string status);
    Task<int> InsertAsync(Borrow entity);
    Task<int> UpdateStatusAsync(long id, string status);
}
