using System.Data;
using System.Data.Common;
using System.Threading.Tasks;

namespace QP11.Core.Interfaces;

/// <summary>
/// 数据库连接工厂接口 — 替代静态 DatabaseFactory，支持依赖注入和单元测试
/// </summary>
public interface IDbConnectionFactory
{
    /// <summary>
    /// 创建一个未打开的数据库连接（调用方需自行 await OpenAsync()）。
    /// 保留此方法仅为兼容旧代码，新代码应使用 CreateAsync()。
    /// </summary>
    DbConnection Create();

    /// <summary>
    /// 创建并异步打开数据库连接。
    /// 在线程池执行 Open()，避免 UI 线程同步阻塞（远程 SQL Server 2000 单次 Open 可达 1-3 秒）。
    /// </summary>
    Task<DbConnection> CreateAsync();

    /// <summary>
    /// 当前使用的数据库提供程序类型（Odbc/SqlClient/SqlClientLegacy）
    /// </summary>
    string Provider { get; }

    /// <summary>
    /// 当前连接模式描述
    /// </summary>
    string ConnectionMode { get; }
}
