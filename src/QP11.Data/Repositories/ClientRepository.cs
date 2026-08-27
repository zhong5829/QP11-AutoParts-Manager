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

public class ClientRepository : IClientRepository
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

    public async Task<IEnumerable<ClientInfor>> GetAllAsync()
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<ClientInfor>(
            "SELECT * FROM client_infor ORDER BY cid");
    }

    public async Task<ClientInfor?> GetByIdAsync(string cid)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryFirstOrDefaultAsync<ClientInfor>(
            "SELECT * FROM client_infor WHERE cid = @Cid", new { Cid = cid });
    }

    public async Task<IEnumerable<ClientInfor>> SearchAsync(string keyword)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<ClientInfor>(
            @"SELECT * FROM client_infor
              WHERE (name LIKE @Kw OR tel LIKE @Kw OR mobile LIKE @Kw OR cid LIKE @Kw) ORDER BY cid",
            new { Kw = $"%{keyword}%" });
    }

    public async Task<int> InsertAsync(ClientInfor entity)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"INSERT INTO client_infor (cid, name, address, linkman, tel, fax, mobile, zip,
                    level, credit, bank, tax, [class], name_py, jyfw, note, bank1, bank2, sell_use)
                    VALUES (@Cid, @Name, @Address, @Linkman, @Tel, @Fax, @Mobile, @Zip,
                    @Level, @Credit, @Bank, @Tax, @Class, @NamePy, @Jyfw, @Note, @Bank1, @Bank2, @SellUse)";
        return await db.ExecuteAsync(sql, entity);
    }

    public async Task<int> UpdateAsync(ClientInfor entity)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"UPDATE client_infor SET name=@Name, address=@Address, linkman=@Linkman,
                    tel=@Tel, fax=@Fax, mobile=@Mobile, zip=@Zip,
                    level=@Level, credit=@Credit, bank=@Bank, tax=@Tax, [class]=@Class,
                    name_py=@NamePy, jyfw=@Jyfw, note=@Note, bank1=@Bank1, bank2=@Bank2, sell_use=@SellUse
                    WHERE cid=@Cid";
        return await db.ExecuteAsync(sql, entity);
    }

    // IRepository<ClientInfor> 显式实现
    Task<ClientInfor?> IRepository<ClientInfor>.GetByIdAsync(object id) => GetByIdAsync(id.ToString()!);
    Task<int> IRepository<ClientInfor>.InsertAsync(ClientInfor entity, IDbTransaction? transaction) => throw new NotImplementedException();
    Task<int> IRepository<ClientInfor>.UpdateAsync(ClientInfor entity, IDbTransaction? transaction) => throw new NotImplementedException();
    Task<int> IRepository<ClientInfor>.DeleteAsync(object id, IDbTransaction? transaction) => throw new NotImplementedException();
    Task<int> IRepository<ClientInfor>.CountAsync() => throw new NotImplementedException();
}
