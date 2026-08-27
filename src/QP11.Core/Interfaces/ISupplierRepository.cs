using System.Collections.Generic;
using System.Threading.Tasks;
using QP11.Core.Entities;

namespace QP11.Core.Interfaces;

public interface ISupplierRepository : IRepository<SupplierInfor>
{
    Task<SupplierInfor?> GetByIdAsync(string sid);
    Task<IEnumerable<SupplierInfor>> SearchAsync(string keyword);
    Task<int> InsertAsync(SupplierInfor entity);
    Task<int> UpdateAsync(SupplierInfor entity);
    Task<string?> GetNameBySidAsync(string sid);
}
