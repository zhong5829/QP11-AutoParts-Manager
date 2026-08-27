using System;
using System.Data;
using System.Data.Common;
using System.Data.Odbc;
using System.Runtime.Versioning;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Configuration;

namespace QP11.Data.Infrastructure;

public static class DatabaseFactory
{
    private static string _connectionString = "";
    private static string _provider = "Odbc";
    private static int _initialized;
    private static string _lastError = "";
    private static string _connectionMode = "";

    // 连接池监控：活跃连接计数与上限
    private static int _activeConnections;
    private static int _maxConcurrentConnections = 50;
    private static int _peakConnections;
    private static long _totalConnectionsCreated;

    // 从配置中读取的服务器/数据库/认证信息，用于动态构建连接串
    private static string _server = "";
    private static string _database = "qipei";
    private static string _uid = "sa";
    private static string _pwd = "";

    /// <summary>
    /// 已知的SQL Server ODBC驱动名，按优先级排列。
    /// {SQL Server} 是Windows内置的旧版驱动（兼容SQL2000），17/18是微软新版ODBC驱动。
    /// </summary>
    private static readonly string[] _knownOdbcDrivers =
    {
        "SQL Server",                      // Windows内置，兼容SQL2000
        "ODBC Driver 17 for SQL Server",   // 微软新版v17
        "ODBC Driver 18 for SQL Server",   // 微软新版v18
    };

    /// <summary>缓存已探测到的可用ODBC驱动名（避免每次连接都遍历注册表）</summary>
    private static string? _detectedDriver;

    public static void Initialize(IConfiguration configuration)
    {
        if (Interlocked.Exchange(ref _initialized, 1) != 0) return;

        _provider = configuration["ConnectionStrings:Provider"] ?? "OleDb";

        var maxConnStr = configuration["DatabaseSettings:MaxConcurrentConnections"];
        if (int.TryParse(maxConnStr, out var maxConn) && maxConn > 0)
            _maxConcurrentConnections = maxConn;

        // 解析服务器/数据库/认证信息（供动态构建连接串使用）
        ParseServerInfo(configuration);

        if (_provider.Equals("OleDb", StringComparison.OrdinalIgnoreCase))
        {
            // OleDb (SQLOLEDB) — Windows内置，最稳定，默认首选
            _connectionString = configuration["ConnectionStrings:QipeiDb_OleDb"]
                ?? BuildOleDbConnectionString();
            _connectionMode = "OleDb";
        }
        else if (_provider.Equals("Odbc", StringComparison.OrdinalIgnoreCase))
        {
            var driverConnStr = configuration["ConnectionStrings:QipeiDb_ODBC_Driver"];
            if (!string.IsNullOrEmpty(driverConnStr))
            {
                _connectionString = driverConnStr;
                _connectionMode = "ODBC(Driver)";
            }
            else
            {
                _connectionString = configuration["ConnectionStrings:QipeiDb_ODBC_DSN"]
                    ?? configuration["ConnectionStrings:QipeiDb_ODBC"]
                    ?? "DSN=qipei;Uid=sa;Pwd=;";
                _connectionMode = "ODBC(DSN)";
            }
        }
        else
        {
            _connectionString = configuration["ConnectionStrings:QipeiDb_SqlClient"]
                ?? "Server=localhost;Database=qipei;User Id=sa;Password=;TrustServerCertificate=True;";
            _connectionMode = _provider;
        }
    }

    /// <summary>构建 SQLOLEDB OleDb 连接串</summary>
    private static string BuildOleDbConnectionString()
    {
        return $"Provider=SQLOLEDB;Data Source={_server};Initial Catalog={_database};User ID={_uid};Password={_pwd};";
    }

    /// <summary>从配置连接串中解析服务器、数据库、认证信息</summary>
    private static void ParseServerInfo(IConfiguration configuration)
    {
        // 尝试从SqlClient连接串中解析（信息最完整）
        var sqlClientStr = configuration["ConnectionStrings:QipeiDb_SqlClient"] ?? "";
        if (!string.IsNullOrEmpty(sqlClientStr))
        {
            _server = ExtractKeyValue(sqlClientStr, "Server") ?? "";
            _database = ExtractKeyValue(sqlClientStr, "Database") ?? "qipei";
            _uid = ExtractKeyValue(sqlClientStr, "User Id") ?? "sa";
            _pwd = ExtractKeyValue(sqlClientStr, "Password") ?? "";
        }

        // 如果Server为空，尝试从ODBC Driver串解析
        if (string.IsNullOrEmpty(_server))
        {
            var driverStr = configuration["ConnectionStrings:QipeiDb_ODBC_Driver"] ?? "";
            _server = ExtractKeyValue(driverStr, "Server") ?? "192.168.1.86,1433";
            _database = ExtractKeyValue(driverStr, "Database") ?? "qipei";
            _uid = ExtractKeyValue(driverStr, "Uid") ?? ExtractKeyValue(driverStr, "UID") ?? "sa";
            _pwd = ExtractKeyValue(driverStr, "Pwd") ?? ExtractKeyValue(driverStr, "PWD") ?? "";
        }
    }

    private static string? ExtractKeyValue(string connStr, string key)
    {
        var prefix = key + "=";
        var idx = connStr.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        var start = idx + prefix.Length;
        var end = connStr.IndexOf(';', start);
        if (end < 0) end = connStr.Length;
        return connStr.Substring(start, end - start);
    }

    public static DbConnection Create()
    {
        if (_initialized == 0)
        {
            AutoInitialize();
        }

        if (string.IsNullOrEmpty(_connectionString))
            throw new InvalidOperationException("DatabaseFactory 未初始化，请检查 appsettings.json 是否存在");

        var current = Interlocked.Increment(ref _activeConnections);
        Interlocked.Increment(ref _totalConnectionsCreated);
        var peak = Volatile.Read(ref _peakConnections);
        while (current > peak)
        {
            if (Interlocked.CompareExchange(ref _peakConnections, current, peak) == peak)
                break;
            peak = Volatile.Read(ref _peakConnections);
        }

        if (current > _maxConcurrentConnections)
        {
            Interlocked.Decrement(ref _activeConnections);
            throw new InvalidOperationException(
                $"数据库并发连接数超限: 当前{current} > 上限{_maxConcurrentConnections}。" +
                $"峰值={_peakConnections}。请在DatabaseSettings:MaxConcurrentConnections中调整上限，或排查连接泄漏。");
        }

        DbConnection? connection = null;
        try
        {
            // 注意：不再在此同步调用 connection.Open()。
            // 连接对象以"未打开"状态返回，由调用方使用 await db.OpenAsync() 异步打开，
            // 避免在 UI 线程上同步阻塞（远程 SQL Server 2000 单次 Open 可达 1-3 秒）。
            if (_provider.Equals("OleDb", StringComparison.OrdinalIgnoreCase))
            {
#pragma warning disable CA1416
                connection = new OleDbCompatConnection(_connectionString);
#pragma warning restore CA1416
                _lastError = "";
                _connectionMode = "OleDb";
            }
            else if (_provider.Equals("Odbc", StringComparison.OrdinalIgnoreCase))
            {
                connection = new OdbcCompatConnection(_connectionString);
                _lastError = "";
                _connectionMode = _connectionString.StartsWith("DSN=", StringComparison.OrdinalIgnoreCase)
                    ? "ODBC(DSN)" : "ODBC(Driver)";
            }
            else
            {
                connection = new Microsoft.Data.SqlClient.SqlConnection(_connectionString);
                _lastError = "";
                _connectionMode = "SqlClient";
            }

            var wrappedConnection = new TrackedConnection(connection, () =>
                Interlocked.Decrement(ref _activeConnections));

            return wrappedConnection;
        }
        catch (Exception ex)
        {
            Interlocked.Decrement(ref _activeConnections);
            _lastError = ex.InnerException?.Message ?? ex.Message;
            connection?.Dispose();

            // 多路回退：OleDb → ODBC Driver → ODBC自动探测 → SqlClient
            var fallbackResult = TryFallback(ex);
            if (fallbackResult != null) return fallbackResult;

            throw new InvalidOperationException(BuildErrorMessage(ex), ex);
        }
    }

    private static void AutoInitialize()
    {
        try
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            var config = new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .Build();
            Initialize(config);
        }
        catch { }
    }

    /// <summary>
    /// 连接失败时的统一多路回退策略：
    /// OleDb失败 → ODBC Driver → ODBC DSN → ODBC自动探测 → SqlClient
    /// ODBC失败 → 尝试OleDb → ODBC自动探测 → SqlClient
    /// </summary>
    private static DbConnection? TryFallback(Exception originalEx)
    {
        var config = LoadConfiguration();

        // 如果当前不是OleDb，先尝试OleDb（最稳定）
        if (!_provider.Equals("OleDb", StringComparison.OrdinalIgnoreCase))
        {
            var oleDbConnStr = config?["ConnectionStrings:QipeiDb_OleDb"]
                ?? BuildOleDbConnectionString();
#pragma warning disable CA1416
            var oleDbConn = TryOpenOleDb(oleDbConnStr, "OleDb(回退)");
#pragma warning restore CA1416
            if (oleDbConn != null)
            {
                _connectionString = oleDbConnStr;
                _provider = "OleDb";
                return oleDbConn;
            }
        }

        // 如果当前不是ODBC，尝试ODBC
        if (!_provider.Equals("Odbc", StringComparison.OrdinalIgnoreCase))
        {
            var driverConnStr = config?["ConnectionStrings:QipeiDb_ODBC_Driver"];
            if (!string.IsNullOrEmpty(driverConnStr))
            {
                var conn = TryOpenOdbc(driverConnStr, "ODBC(Driver回退)");
                if (conn != null)
                {
                    _connectionString = driverConnStr;
                    _provider = "Odbc";
                    return conn;
                }
            }

            var dsnConnStr = config?["ConnectionStrings:QipeiDb_ODBC_DSN"];
            if (!string.IsNullOrEmpty(dsnConnStr))
            {
                var conn = TryOpenOdbc(dsnConnStr, "ODBC(DSN回退)");
                if (conn != null)
                {
                    _connectionString = dsnConnStr;
                    _provider = "Odbc";
                    return conn;
                }
            }
        }

        // ODBC Driver自动探测
        if (!string.IsNullOrEmpty(_server))
        {
            var detectedConn = TryAutoDetectDriver();
            if (detectedConn != null) return detectedConn;
        }

        // 最后手段 — SqlClient直连
        var sqlClientConnStr = config?["ConnectionStrings:QipeiDb_SqlClient"];
        if (!string.IsNullOrEmpty(sqlClientConnStr))
        {
            try
            {
                var sqlConn = new Microsoft.Data.SqlClient.SqlConnection(sqlClientConnStr);
                sqlConn.Open();
                sqlConn.Close(); // 仅验证连通性，返回未打开连接交由调用方异步 OpenAsync
                _connectionString = sqlClientConnStr;
                _provider = "SqlClient";
                _connectionMode = "SqlClient(自动回退)";
                _lastError = "";
                Serilog.Log.Warning("OleDb/ODBC连接全部失败，自动回退到SqlClient模式");
                return sqlConn;
            }
            catch { }
        }

        return null;
    }

    /// <summary>尝试用指定连接串验证OleDb连接可用性（验证后关闭，由调用方异步打开）</summary>
    private static DbConnection? TryOpenOleDb(string connStr, string mode)
    {
        try
        {
#pragma warning disable CA1416
            var conn = new OleDbCompatConnection(connStr);
            conn.Open();
            conn.Close(); // 仅验证连通性，返回未打开连接交由调用方异步 OpenAsync
#pragma warning restore CA1416
            _connectionMode = mode;
            _lastError = "";
            Serilog.Log.Information("回退到OleDb模式成功: {Mode}", mode);
            return conn;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>尝试用指定连接串验证ODBC连接可用性（验证后关闭，由调用方异步打开）</summary>
    private static DbConnection? TryOpenOdbc(string connStr, string mode)
    {
        try
        {
            var conn = new OdbcCompatConnection(connStr);
            conn.Open();
            conn.Close(); // 仅验证连通性，返回未打开连接交由调用方异步 OpenAsync
            _connectionMode = mode;
            _lastError = "";
            return conn;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>自动探测系统中可用的SQL Server ODBC驱动，构建Driver连接串并尝试连接</summary>
    private static DbConnection? TryAutoDetectDriver()
    {
        // 如果已经探测过，直接用缓存结果
        if (_detectedDriver != null)
        {
            var connStr = BuildDriverConnectionString(_detectedDriver);
            var conn = TryOpenOdbc(connStr, $"ODBC(Driver自动:{_detectedDriver})");
            if (conn != null)
            {
                _connectionString = connStr;
                return conn;
            }
            // 缓存的驱动也失败了，清除缓存重试
            _detectedDriver = null;
        }

        // 遍历已知驱动名，逐个尝试
        foreach (var driver in _knownOdbcDrivers)
        {
            if (string.IsNullOrEmpty(_server)) continue;

            var connStr = BuildDriverConnectionString(driver);
            var conn = TryOpenOdbc(connStr, $"ODBC(Driver探测:{driver})");
            if (conn != null)
            {
                _detectedDriver = driver;
                _connectionString = connStr;
                Serilog.Log.Information("自动探测到可用ODBC驱动: {Driver}", driver);
                return conn;
            }
        }

        return null;
    }

    /// <summary>根据驱动名和已解析的服务器信息构建ODBC Driver连接串</summary>
    private static string BuildDriverConnectionString(string driverName)
    {
        return $"Driver={{{driverName}}};Server={_server};Database={_database};Uid={_uid};Pwd={_pwd};";
    }

    private static IConfiguration? LoadConfiguration()
    {
        try
        {
            var basePath = AppDomain.CurrentDomain.BaseDirectory;
            return new ConfigurationBuilder()
                .SetBasePath(basePath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .Build();
        }
        catch { return null; }
    }

    private static string BuildErrorMessage(Exception ex)
    {
        var sb = new StringBuilder();
        sb.AppendLine("数据库连接失败!");
        sb.AppendLine();
        sb.AppendLine($"连接模式: {_connectionMode}");
        sb.AppendLine($"Provider: {_provider}");

        var displayConnStr = MaskPassword(_connectionString);
        sb.AppendLine($"连接串: {displayConnStr}");
        sb.AppendLine();
        sb.AppendLine($"错误: {ex.InnerException?.Message ?? ex.Message}");
        sb.AppendLine();

        // 针对IM002错误给出专门诊断
        var errorMsg = ex.InnerException?.Message ?? ex.Message;
        if (errorMsg.Contains("IM002") || errorMsg.Contains("未发现数据源名称"))
        {
            sb.AppendLine(">>> DSN未找到! 这是ODBC数据源配置问题，不是SQL Server服务问题。");
            sb.AppendLine();
            sb.AppendLine("紧急修复步骤:");
            sb.AppendLine("1. 检查ODBC数据源: 运行 %windir%\\system32\\odbcad32.exe，查看'系统DSN'中是否有'qipei'");
            sb.AppendLine("2. 如果DSN丢失: 在'系统DSN'中添加，驱动选'SQL Server'，名称qipei，服务器填数据库服务器IP");
            sb.AppendLine("3. 或者修改appsettings.json: 将QipeiDb_ODBC_Driver设为 Driver={SQL Server};Server=IP,端口;Database=qipei;Uid=sa;Pwd=密码;");
            sb.AppendLine("4. 确认ODBC驱动存在: 在odbcad32.exe的'驱动程序'标签中查看是否有'SQL Server'驱动");
            sb.AppendLine("5. 注意32/64位: 64位系统需用%windir%\\system32\\odbcad32.exe(64位ODBC)，不要用syswow64下的32位版");
        }
        else
        {
            var hint = _provider switch
            {
                "OleDb" => "OleDb模式(SQLOLEDB): Windows内置组件，兼容SQL Server 2000，无需DSN",
                "Odbc" => "ODBC模式: 自动转换@命名参数为?位置参数，兼容SQL Server 2000",
                _ => "SqlClient模式: 使用System.Data.SqlClient v4.9，兼容SQL Server 2000"
            };
            sb.AppendLine($"提示: {hint}");
            sb.AppendLine();
            sb.AppendLine("建议:");
            sb.AppendLine("1. 确认SQL Server服务已启动");
            sb.AppendLine("2. 确认IP地址和端口正确");
            sb.AppendLine("3. 确认sa账号密码正确");
            sb.AppendLine("4. 确认SQL Server允许远程连接(TCP/IP协议已启用)");
            sb.AppendLine("5. 确认防火墙未阻止端口");
        }

        return sb.ToString();
    }

    private static string MaskPassword(string connStr)
    {
        if (string.IsNullOrEmpty(connStr)) return connStr;
        var patterns = new[] { "Pwd=", "PWD=", "Password=", "password=" };
        var result = connStr;
        foreach (var pattern in patterns)
        {
            var idx = result.IndexOf(pattern, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;
            var start = idx + pattern.Length;
            var end = result.IndexOf(';', start);
            if (end < 0) end = result.Length;
            result = result.Substring(0, start) + "***" + result.Substring(end);
        }
        return result;
    }

    public static bool TestConnection(out string message)
    {
        try
        {
            using var db = Create();
            // Create() 返回未打开连接，TestConnection 为同步方法（LoginWindow 已用 Task.Run 包装，不阻塞 UI）
            db.Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT 1";
            var result = cmd.ExecuteScalar();
            message = $"连接成功! (模式: {_connectionMode})";
            return true;
        }
        catch (Exception ex)
        {
            message = $"连接失败: {ex.InnerException?.Message ?? ex.Message}\n模式: {_connectionMode}";
            return false;
        }
    }

    public static string Provider => _provider;
    public static string ConnectionString => _connectionString;
    public static string ConnectionMode => _connectionMode;
    public static string LastError => _lastError;

    /// <summary>当前活跃连接数</summary>
    public static int ActiveConnections => Volatile.Read(ref _activeConnections);
    /// <summary>历史峰值连接数</summary>
    public static int PeakConnections => Volatile.Read(ref _peakConnections);
    /// <summary>累计创建连接总数</summary>
    public static long TotalConnectionsCreated => Volatile.Read(ref _totalConnectionsCreated);
    /// <summary>最大并发连接数上限</summary>
    public static int MaxConcurrentConnections => Volatile.Read(ref _maxConcurrentConnections);
}

/// <summary>
/// 连接包装器：在真实连接关闭/释放时回调递减活跃计数。
/// 所有 DbConnection 抽象方法均委托给内部真实连接，对上层完全透明。
/// </summary>
internal sealed class TrackedConnection : DbConnection
{
    private readonly DbConnection _inner;
    private readonly Action _onClose;
    private bool _closed;

    public TrackedConnection(DbConnection inner, Action onClose)
    {
        _inner = inner;
        _onClose = onClose;
        _inner.StateChange += (s, e) =>
        {
            if (e.CurrentState == ConnectionState.Closed && !_closed)
            {
                _closed = true;
                _onClose();
            }
        };
    }

#pragma warning disable CS8765
    public override string ConnectionString { get => _inner.ConnectionString; set => _inner.ConnectionString = value; }
#pragma warning restore CS8765
    public override string Database => _inner.Database;
    public override ConnectionState State => _inner.State;
    public override string DataSource => _inner.DataSource;
    public override string ServerVersion => _inner.ServerVersion;
    public override int ConnectionTimeout => _inner.ConnectionTimeout;

    public override void Open() => _inner.Open();
    public override void Close()
    {
        _inner.Close();
        if (!_closed) { _closed = true; _onClose(); }
    }
    public override void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => _inner.BeginTransaction(isolationLevel);
    protected override DbCommand CreateDbCommand() => _inner.CreateCommand();

    protected override void Dispose(bool disposing)
    {
        if (disposing && !_closed) { _closed = true; _onClose(); }
        _inner.Dispose();
        base.Dispose(disposing);
    }
}
