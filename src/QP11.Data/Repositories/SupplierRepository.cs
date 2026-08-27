using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using QP11.Data.Infrastructure;

namespace QP11.Data.Repositories;

public class SupplierRepository : ISupplierRepository
{
    protected DbConnection CreateConnection() => DatabaseFactory.Create();

    /// <summary>创建并异步打开连接，避免 UI 线程同步阻塞</summary>
    protected async Task<DbConnection> CreateConnectionAsync()
    {
        var db = DatabaseFactory.Create();
        if (db.State != ConnectionState.Open)
            await db.OpenAsync();
        return db;
    }

    public async Task<IEnumerable<SupplierInfor>> GetAllAsync()
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<SupplierInfor>(
            "SELECT * FROM supplier_infor ORDER BY sid");
    }

    public async Task<SupplierInfor?> GetByIdAsync(string sid)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryFirstOrDefaultAsync<SupplierInfor>(
            "SELECT * FROM supplier_infor WHERE sid = @Sid", new { Sid = sid });
    }

    public async Task<IEnumerable<SupplierInfor>> SearchAsync(string keyword)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<SupplierInfor>(
            @"SELECT * FROM supplier_infor
              WHERE (name LIKE @Kw OR tel LIKE @Kw OR mobile LIKE @Kw OR sid LIKE @Kw) ORDER BY sid",
            new { Kw = $"%{keyword}%" });
    }

    public async Task<int> InsertAsync(SupplierInfor entity)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"INSERT INTO supplier_infor (sid, name, address, linkman, tel, fax, mobile, zip,
                    level, credit, bank, tax, [class], name_py)
                    VALUES (@Sid, @Name, @Address, @Linkman, @Tel, @Fax, @Mobile, @Zip,
                    @Level, @Credit, @Bank, @Tax, @Class, @NamePy)";
        return await db.ExecuteAsync(sql, entity);
    }

    public async Task<int> UpdateAsync(SupplierInfor entity)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"UPDATE supplier_infor SET name=@Name, address=@Address, linkman=@Linkman,
                    tel=@Tel, fax=@Fax, mobile=@Mobile, zip=@Zip,
                    level=@Level, credit=@Credit, bank=@Bank, tax=@Tax, [class]=@Class, name_py=@NamePy
                    WHERE sid=@Sid";
        return await db.ExecuteAsync(sql, entity);
    }

    public async Task<string?> GetNameBySidAsync(string sid)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryFirstOrDefaultAsync<string>(
            "SELECT name FROM supplier_infor WHERE sid=@Sid", new { Sid = sid });
    }

    // IRepository<SupplierInfor> 显式实现
    Task<SupplierInfor?> IRepository<SupplierInfor>.GetByIdAsync(object id) => GetByIdAsync(id.ToString()!);
    Task<int> IRepository<SupplierInfor>.InsertAsync(SupplierInfor entity, IDbTransaction? transaction) => throw new NotImplementedException();
    Task<int> IRepository<SupplierInfor>.UpdateAsync(SupplierInfor entity, IDbTransaction? transaction) => throw new NotImplementedException();
    Task<int> IRepository<SupplierInfor>.DeleteAsync(object id, IDbTransaction? transaction) => throw new NotImplementedException();
    Task<int> IRepository<SupplierInfor>.CountAsync() => throw new NotImplementedException();
}
