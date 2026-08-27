using System.Collections.Generic;
using System.Threading.Tasks;
using QP11.Core.Entities;

namespace QP11.Core.Interfaces;

public interface IDesktopRepository
{
    Task<IEnumerable<Desktop>> GetByUsernameAsync(string username);
    Task<int> InsertAsync(Desktop entity);
    Task<int> DeleteAsync(string code, string username);
}
