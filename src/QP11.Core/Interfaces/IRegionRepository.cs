using System.Collections.Generic;
using System.Threading.Tasks;
using QP11.Core.Entities;

namespace QP11.Core.Interfaces;

public interface IRegionRepository
{
    Task<IEnumerable<Region>> GetChildrenAsync(long? parentId);
    Task<int> InsertAsync(Region entity);
    Task<int> UpdateAsync(Region entity);
    Task<int> DeleteAsync(long id);
}
