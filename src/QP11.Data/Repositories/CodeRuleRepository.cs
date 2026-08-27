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

public class CodeRuleRepository : ICodeRuleRepository
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

    public async Task<IEnumerable<CodeRule>> GetAllAsync()
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<CodeRule>("SELECT * FROM code_rule ORDER BY table_name");
    }

    public async Task<CodeRule?> GetByTableAsync(string tableName)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryFirstOrDefaultAsync<CodeRule>(
            "SELECT * FROM code_rule WHERE table_name = @TableName", new { TableName = tableName });
    }

    public async Task<int> InsertAsync(CodeRule entity)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"INSERT INTO code_rule (table_name, prefix, date_format, seq_length, current_seq, reset_daily, memo)
                    VALUES (@TableName, @Prefix, @DateFormat, @SeqLength, @CurrentSeq, @ResetDaily, @Memo)";
        return await db.ExecuteAsync(sql, entity);
    }

    public async Task<int> UpdateAsync(CodeRule entity)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"UPDATE code_rule SET prefix=@Prefix, date_format=@DateFormat, seq_length=@SeqLength,
                    current_seq=@CurrentSeq, reset_daily=@ResetDaily, memo=@Memo WHERE id=@Id";
        return await db.ExecuteAsync(sql, entity);
    }

    public async Task<int> DeleteAsync(long id)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync("DELETE FROM code_rule WHERE id = @Id", new { Id = id });
    }

    public async Task<int> GetNextSeqAsync(long id, IDbTransaction? transaction = null)
    {
        var ownsConnection = transaction == null;
        IDbConnection db;
        if (transaction != null)
        {
            db = transaction.Connection!;
        }
        else
        {
            db = await CreateConnectionAsync();
        }

        try
        {
            var sql = "UPDATE code_rule SET current_seq = ISNULL(current_seq, 0) + 1 OUTPUT INSERTED.current_seq WHERE id = @Id";
            var result = await db.QueryFirstOrDefaultAsync<int>(sql, new { Id = id }, transaction);
            return result;
        }
        finally
        {
            if (ownsConnection) db.Dispose();
        }
    }
}
