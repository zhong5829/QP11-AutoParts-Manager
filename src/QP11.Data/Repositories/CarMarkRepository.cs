using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading.Tasks;
using Dapper;
using QP11.Core.Entities;
using QP11.Data.Infrastructure;

namespace QP11.Data.Repositories;

public class CarMarkRepository
{
    private const string CarMarkColumns = "id, name, carname, cartype, engine, carframe, linkman, tel, memo, name_py, client_cid";

    protected DbConnection CreateConnection() => DatabaseFactory.Create();

    /// <summary>创建并异步打开连接，避免 UI 线程同步阻塞</summary>
    protected async Task<DbConnection> CreateConnectionAsync()
    {
        var db = DatabaseFactory.Create();
        if (db.State != ConnectionState.Open)
            await db.OpenAsync();
        return db;
    }

    public async Task<IEnumerable<CarMark>> GetAllAsync()
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<CarMark>(
            $"SELECT {CarMarkColumns} FROM car_mark ORDER BY carname");
    }

    public async Task<CarMark?> GetByIdAsync(long id)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryFirstOrDefaultAsync<CarMark>(
            $"SELECT {CarMarkColumns} FROM car_mark WHERE id = @Id", new { Id = id });
    }

    public async Task<IEnumerable<CarMark>> SearchAsync(string keyword)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<CarMark>(
            $@"SELECT {CarMarkColumns} FROM car_mark
              WHERE (name LIKE @Kw OR carname LIKE @Kw OR cartype LIKE @Kw OR engine LIKE @Kw)
              ORDER BY carname",
            new { Kw = $"%{keyword}%" });
    }

    public async Task<int> InsertAsync(CarMark entity)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"INSERT INTO car_mark (name, carname, cartype, engine, carframe, memo)
                    VALUES (@Name, @Carname, @Cartype, @Engine, @Carframe, @Memo)";
        return await db.ExecuteAsync(sql, entity);
    }

    public async Task<int> UpdateAsync(CarMark entity)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"UPDATE car_mark SET name=@Name, carname=@Carname, cartype=@Cartype,
                    engine=@Engine, carframe=@Carframe, memo=@Memo WHERE id=@Id";
        return await db.ExecuteAsync(sql, entity);
    }

    public async Task<int> LogicDeleteAsync(long id)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync("UPDATE car_mark SET name = name + '_已删' WHERE id = @Id", new { Id = id });
    }
}
