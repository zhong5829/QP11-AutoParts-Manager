using System.Collections.Generic;
using System.Threading.Tasks;
using QP11.Core.Entities;

namespace QP11.Core.Interfaces;

public interface IPartLocationRepository
{
    Task<IEnumerable<PartLocation>> GetAllAsync();
    Task<PartLocation?> GetByPlaceAsync(string place);
    Task<int> InsertAsync(PartLocation entity);
    Task<int> UpdateAsync(PartLocation entity);
    Task<int> DeleteAsync(string place);
}
