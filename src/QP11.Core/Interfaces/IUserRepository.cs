using System.Collections.Generic;
using System.Threading.Tasks;
using QP11.Core.Entities;

namespace QP11.Core.Interfaces;

public interface IUserRepository : IRepository<UserInfor>
{
    Task<IEnumerable<UserInfor>> GetAllIncludingDisabledAsync();
    Task<int> InsertAsync(UserInfor entity);
    Task<int> UpdateAsync(UserInfor entity);
    Task<int> UpdatePasswordAsync(string username, string newPassword);
    Task<int> DisableAsync(string username);
    Task<int> EnableAsync(string username);
}
