using System.Collections.Generic;
using System.Threading.Tasks;
using QP11.Core.Entities;

namespace QP11.Core.Interfaces;

public interface IClientRepository : IRepository<ClientInfor>
{
    Task<ClientInfor?> GetByIdAsync(string cid);
    Task<IEnumerable<ClientInfor>> SearchAsync(string keyword);
    Task<int> InsertAsync(ClientInfor entity);
    Task<int> UpdateAsync(ClientInfor entity);
}
