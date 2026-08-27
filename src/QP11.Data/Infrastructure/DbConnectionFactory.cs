using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using QP11.Core.Interfaces;

namespace QP11.Data.Infrastructure;

/// <summary>
/// IDbConnectionFactory 的实现 — 委托给现有 DatabaseFactory，逐步替换静态调用
/// </summary>
public class DbConnectionFactory : IDbConnectionFactory
{
    private readonly IConfiguration _configuration;

    public DbConnectionFactory(IConfiguration configuration)
    {
        _configuration = configuration;
        // 确保 DatabaseFactory 已初始化
        DatabaseFactory.Initialize(configuration);
    }

    /// <inheritdoc/>
    public DbConnection Create()
    {
        return DatabaseFactory.Create();
    }

    /// <inheritdoc/>
    public async Task<DbConnection> CreateAsync()
    {
        var db = DatabaseFactory.Create();
        if (db.State != ConnectionState.Open)
        {
            await db.OpenAsync();
        }
        return db;
    }

    /// <inheritdoc/>
    public string Provider => DatabaseFactory.Provider;

    /// <inheritdoc/>
    public string ConnectionMode => DatabaseFactory.ConnectionMode;
}
