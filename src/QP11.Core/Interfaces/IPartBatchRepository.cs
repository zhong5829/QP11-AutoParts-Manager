using System.Collections.Generic;
using System.Threading.Tasks;
using QP11.Core.Entities;

namespace QP11.Core.Interfaces;

public interface IPartBatchRepository
{
    Task<IEnumerable<PartBatch>> GetByPartIdAsync(long partid);
    Task<IEnumerable<PartBatch>> GetExpiringAsync(int days = 30);
    Task<int> InsertAsync(PartBatch entity);
    Task<int> UpdateRemainAsync(long id, decimal remain);
    Task<int> LogicDeleteAsync(long id);
}
