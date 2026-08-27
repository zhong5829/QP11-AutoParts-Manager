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

public class LogisticsRepository : ILogisticsRepository
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

    public async Task<IEnumerable<Logistics>> GetAllAsync()
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<Logistics>(
            "SELECT * FROM wuliu_infor ORDER BY sid");
    }

    public async Task<Logistics?> GetByIdAsync(string sid)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryFirstOrDefaultAsync<Logistics>(
            "SELECT * FROM wuliu_infor WHERE sid = @Sid", new { Sid = sid });
    }

    public async Task<int> InsertAsync(Logistics entity)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"INSERT INTO wuliu_infor (sid, name, address, linkman, tel, fax, mobile, zip, level, credit, bank, tax, class, name_py)
                    VALUES (@Sid, @Name, @Address, @Linkman, @Tel, @Fax, @Mobile, @Zip, @Level, @Credit, @Bank, @Tax, @Class, @NamePy)";
        return await db.ExecuteAsync(sql, entity);
    }

    public async Task<int> UpdateAsync(Logistics entity)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"UPDATE wuliu_infor SET name=@Name, address=@Address, linkman=@Linkman, tel=@Tel, fax=@Fax,
                    mobile=@Mobile, zip=@Zip, level=@Level, credit=@Credit, bank=@Bank, tax=@Tax, class=@Class, name_py=@NamePy
                    WHERE sid=@Sid";
        return await db.ExecuteAsync(sql, entity);
    }

    public async Task<int> DeleteAsync(string sid)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync("DELETE FROM wuliu_infor WHERE sid=@Sid", new { Sid = sid });
    }
}
