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

public class PartLocationRepository : IPartLocationRepository
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

    public async Task<IEnumerable<PartLocation>> GetAllAsync()
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<PartLocation>(
            "SELECT * FROM part_place ORDER BY place");
    }

    public async Task<PartLocation?> GetByPlaceAsync(string place)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryFirstOrDefaultAsync<PartLocation>(
            "SELECT * FROM part_place WHERE place = @Place", new { Place = place });
    }

    public async Task<int> InsertAsync(PartLocation entity)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"INSERT INTO part_place (place, place_nm, place_user, place_type, place_area, place_note)
                    VALUES (@Place, @PlaceNm, @PlaceUser, @PlaceType, @PlaceArea, @PlaceNote)";
        return await db.ExecuteAsync(sql, entity);
    }

    public async Task<int> UpdateAsync(PartLocation entity)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"UPDATE part_place SET place_nm=@PlaceNm, place_user=@PlaceUser,
                    place_type=@PlaceType, place_area=@PlaceArea, place_note=@PlaceNote
                    WHERE place=@Place";
        return await db.ExecuteAsync(sql, entity);
    }

    public async Task<int> DeleteAsync(string place)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync("DELETE FROM part_place WHERE place=@Place", new { Place = place });
    }
}
