using QP11.Core.Interfaces;

namespace QP11.Data.Infrastructure;

public class DatabaseInfoService : IDatabaseInfoService
{
    public string Provider => DatabaseFactory.Provider;
    public string ConnectionMode => DatabaseFactory.ConnectionMode;

    public bool TestConnection(out string message) => DatabaseFactory.TestConnection(out message);
}
