using QP11.Core.Interfaces;

namespace QP11.Data.Infrastructure;

public class UnitOfWorkFactory : IUnitOfWorkFactory
{
    private readonly IDbConnectionFactory _dbFactory;

    public UnitOfWorkFactory(IDbConnectionFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public IUnitOfWork Create() => new UnitOfWork(_dbFactory);
}
