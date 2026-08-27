using System;
using System.Data;
using System.Threading.Tasks;

namespace QP11.Core.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IDbTransaction Transaction { get; }
    IDbConnection Connection { get; }
    Task BeginTransactionAsync(IsolationLevel isolationLevel = IsolationLevel.ReadCommitted);
    Task CommitAsync();
    Task RollbackAsync();
}
