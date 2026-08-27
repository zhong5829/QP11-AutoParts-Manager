using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using QP11.Core.Entities;

namespace QP11.Core.Interfaces;

public interface ICodeRuleRepository
{
    Task<IEnumerable<CodeRule>> GetAllAsync();
    Task<CodeRule?> GetByTableAsync(string tableName);
    Task<int> InsertAsync(CodeRule entity);
    Task<int> UpdateAsync(CodeRule entity);
    Task<int> DeleteAsync(long id);
    Task<int> GetNextSeqAsync(long id, IDbTransaction? transaction = null);
}
