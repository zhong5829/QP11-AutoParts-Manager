using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using QP11.Core.Entities;

namespace QP11.Core.Interfaces;

public interface IMemberCardRepository : IRepository<MemberCard>
{
    Task<MemberCard?> GetByIdAsync(string kh);
    Task<IEnumerable<MemberCard>> SearchAsync(string keyword);
    new Task<int> InsertAsync(MemberCard entity, IDbTransaction? transaction = null);
    Task<int> UpdateAsync(MemberCard entity);
    Task<int> RechargeAsync(string kh, decimal amount, IDbTransaction? transaction = null);
    Task<int> ConsumeAsync(string kh, decimal amount, IDbTransaction? transaction = null);
    Task<int> LogicDeleteAsync(string kh);
}
