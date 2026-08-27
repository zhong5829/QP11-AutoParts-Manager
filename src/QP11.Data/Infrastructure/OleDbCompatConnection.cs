using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Data.OleDb;
using System.Runtime.Versioning;

namespace QP11.Data.Infrastructure;

/// <summary>
/// OleDb 连接兼容包装器：将 @命名参数 SQL 转换为 ?位置参数 SQL。
/// SQLOLEDB 是 Windows 内置组件（MDAC/WDAC），永远不会被系统更新清除，
/// 是连接 SQL Server 2000 最稳定的方式。
/// </summary>
[SupportedOSPlatform("windows")]
public class OleDbCompatConnection : DbConnection
{
    private readonly OleDbConnection _inner;
    private ConnectionState _state = ConnectionState.Closed;
    private OleDbTransaction? _activeInnerTransaction;

    public OleDbCompatConnection(string connectionString)
    {
        _inner = new OleDbConnection(connectionString);
        _inner.StateChange += (s, e) => _state = e.CurrentState;
    }

    public OleDbConnection InnerConnection => _inner;

    public override string ConnectionString
    {
        get => _inner.ConnectionString;
#pragma warning disable CS8765
        set => _inner.ConnectionString = value;
#pragma warning restore CS8765
    }

    public override string Database => _inner.Database;
    public override ConnectionState State => _state;
    public override string DataSource => _inner.DataSource;
    public override string ServerVersion => _inner.ServerVersion;
    public override int ConnectionTimeout => _inner.ConnectionTimeout;

    public override void Open() { _inner.Open(); _state = _inner.State; }
    public override void Close() { _inner.Close(); _state = _inner.State; }
    public override void ChangeDatabase(string databaseName) => _inner.ChangeDatabase(databaseName);

    protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
    {
        var innerTxn = _inner.BeginTransaction(isolationLevel);
        _activeInnerTransaction = innerTxn;
        return new OleDbCompatTransaction(innerTxn, this);
    }

    internal OleDbTransaction? ActiveInnerTransaction => _activeInnerTransaction;

    internal void OnTransactionDisposed()
    {
        _activeInnerTransaction = null;
    }

    protected override DbCommand CreateDbCommand()
    {
        return new OleDbCompatCommand(_inner.CreateCommand(), this);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }
}

[SupportedOSPlatform("windows")]
public class OleDbCompatCommand : DbCommand
{
    private readonly OleDbCommand _inner;
    private readonly OleDbCompatConnection _connection;
    private string _originalSql = "";
    private OleDbCompatParameterCollection? _paramWrapper;
    private DbTransaction? _cachedTransaction;

    public OleDbCompatCommand(OleDbCommand inner, OleDbCompatConnection connection)
    {
        _inner = inner;
        _connection = connection;
    }

    public override string CommandText
    {
        get => _originalSql;
#pragma warning disable CS8765
        set => _originalSql = value;
#pragma warning restore CS8765
    }

    public override int CommandTimeout
    {
        get => _inner.CommandTimeout;
        set => _inner.CommandTimeout = value;
    }

    public override CommandType CommandType
    {
        get => _inner.CommandType;
        set => _inner.CommandType = value;
    }

    protected override DbConnection? DbConnection
    {
        get => _connection;
        set { }
    }

    protected override DbParameterCollection DbParameterCollection =>
        _paramWrapper ??= new OleDbCompatParameterCollection(_inner.Parameters);

    public override UpdateRowSource UpdatedRowSource
    {
        get => _inner.UpdatedRowSource;
        set => _inner.UpdatedRowSource = value;
    }

    public override bool DesignTimeVisible
    {
        get => _inner.DesignTimeVisible;
        set => _inner.DesignTimeVisible = value;
    }

    protected override DbTransaction? DbTransaction
    {
        get => _cachedTransaction ?? _inner.Transaction;
        set
        {
            _cachedTransaction = value;
            _inner.Transaction = value is OleDbCompatTransaction compat ? compat.InnerTransaction : (OleDbTransaction?)value;
        }
    }

    public override void Cancel() => _inner.Cancel();

    private void EnsureInnerTransaction()
    {
        if (_inner.Transaction != null) return;

        if (_cachedTransaction != null)
        {
            _inner.Transaction = _cachedTransaction is OleDbCompatTransaction compat
                ? compat.InnerTransaction
                : (OleDbTransaction)_cachedTransaction;
            return;
        }

        var connTxn = _connection.ActiveInnerTransaction;
        if (connTxn != null)
            _inner.Transaction = connTxn;
    }

    public override int ExecuteNonQuery() { PrepareCommand(); EnsureInnerTransaction(); return _inner.ExecuteNonQuery(); }
    public override object? ExecuteScalar() { PrepareCommand(); EnsureInnerTransaction(); return _inner.ExecuteScalar(); }

    protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
    {
        PrepareCommand();
        EnsureInnerTransaction();
        return _inner.ExecuteReader(behavior);
    }

    protected override DbParameter CreateDbParameter() => _inner.CreateParameter();

    public override void Prepare() => _inner.Prepare();

    protected override void Dispose(bool disposing)
    {
        if (disposing) _inner.Dispose();
        base.Dispose(disposing);
    }

    /// <summary>将 @命名参数 SQL 转换为 ?位置参数 SQL，委托给共享的 SqlParamConverter</summary>
    private void PrepareCommand() => SqlParamConverter.ApplyToParameters(_originalSql, _inner);
}

[SupportedOSPlatform("windows")]
public class OleDbCompatParameterCollection : DbParameterCollection
{
    private readonly OleDbParameterCollection _inner;

    public OleDbCompatParameterCollection(OleDbParameterCollection inner)
    {
        _inner = inner;
    }

    public override int Count => _inner.Count;
    public override object SyncRoot => _inner.SyncRoot;
    public override bool IsFixedSize => _inner.IsFixedSize;
    public override bool IsReadOnly => _inner.IsReadOnly;
    public override bool IsSynchronized => _inner.IsSynchronized;

    public override int Add(object value) => _inner.Add(value);
    public override void AddRange(Array values) => _inner.AddRange(values);
    public override void Clear() => _inner.Clear();
    public override bool Contains(object value) => _inner.Contains(value);
    public override bool Contains(string value) => _inner.Contains(value);
    public override void CopyTo(Array array, int index) => _inner.CopyTo(array, index);
    public override IEnumerator GetEnumerator() => _inner.GetEnumerator();
    public override int IndexOf(object value) => _inner.IndexOf(value);
    public override int IndexOf(string parameterName) => _inner.IndexOf(parameterName);
    public override void Insert(int index, object value) => _inner.Insert(index, value);
    public override void Remove(object value) => _inner.Remove(value);
    public override void RemoveAt(int index) => _inner.RemoveAt(index);
    public override void RemoveAt(string parameterName) => _inner.RemoveAt(parameterName);

    protected override DbParameter GetParameter(int index) => _inner[index];
    protected override DbParameter GetParameter(string parameterName) => _inner[parameterName];
    protected override void SetParameter(int index, DbParameter value) => _inner[index] = (OleDbParameter)value;
    protected override void SetParameter(string parameterName, DbParameter value) => _inner[parameterName] = (OleDbParameter)value;
}

[SupportedOSPlatform("windows")]
public class OleDbCompatTransaction : DbTransaction
{
    private readonly OleDbTransaction _inner;
    private readonly OleDbCompatConnection _ownerConnection;

    public OleDbCompatTransaction(OleDbTransaction inner, OleDbCompatConnection ownerConnection)
    {
        _inner = inner;
        _ownerConnection = ownerConnection;
    }

    public OleDbTransaction InnerTransaction => _inner;

    protected override DbConnection DbConnection => _ownerConnection;

    public override IsolationLevel IsolationLevel => _inner.IsolationLevel;

    public override void Commit() => _inner.Commit();
    public override void Rollback() => _inner.Rollback();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _inner.Dispose();
            _ownerConnection.OnTransactionDisposed();
        }
        base.Dispose(disposing);
    }
}
