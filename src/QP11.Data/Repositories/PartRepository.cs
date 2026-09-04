using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using QP11.Core.Models;
using QP11.Data.Infrastructure;

namespace QP11.Data.Repositories;

public class PartRepository : IPartRepository
{
    private const string PartColumns = "partid, partno, name, carname, cartype, unit, [class], area, place, inprice, isck, name_py, carname_py, cartype_py, unit_py, area_py, memo, DEL, name_bs, carname_bs, cartype_bs, unit_bs, area_bs, part_th, part_gg, part_tm, part_cclb, lsprice, pfprice, part_bzq, part_bzrq";

    protected DbConnection CreateConnection() => DatabaseFactory.Create();

    /// <summary>创建并异步打开连接，避免 UI 线程同步阻塞</summary>
    protected async Task<DbConnection> CreateConnectionAsync()
    {
        var db = DatabaseFactory.Create();
        if (db.State != ConnectionState.Open)
        {
            await db.OpenAsync();
        }
        return db;
    }

    public async Task<IEnumerable<PartData>> GetAllAsync()
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryAsync<PartData>(
            $"SELECT {PartColumns} FROM part_data WHERE (DEL IS NULL OR DEL = '0') ORDER BY partid");
    }

    public async Task<PartData?> GetByIdAsync(long partid)
    {
        using var db = await CreateConnectionAsync();
        return await db.QueryFirstOrDefaultAsync<PartData>(
            $"SELECT {PartColumns} FROM part_data WHERE partid = @Id", new { Id = partid });
    }

    public async Task<PagedResult<PartData>> GetPagedAsync(PartQueryCriteria criteria, int page = 1, int pageSize = 50)
    {
        using var db = await CreateConnectionAsync();
        var where = "WHERE (DEL IS NULL OR DEL = '0')";
        if (!string.IsNullOrEmpty(criteria.Keyword))
            where += " AND (name LIKE @Kw OR partno LIKE @Kw OR name_py LIKE @Kw OR carname LIKE @Kw)";
        if (!string.IsNullOrEmpty(criteria.ClassId))
            where += " AND [class] = @ClassId";

        var total = await db.ExecuteScalarAsync<int>($"SELECT COUNT(*) FROM part_data {where}",
            new { Kw = $"%{criteria.Keyword}%", criteria.ClassId });

        var offset = (page - 1) * pageSize;
        var sql = $@"
            SELECT TOP {pageSize} {PartColumns} FROM part_data {where}
            AND partid NOT IN (SELECT TOP {offset} partid FROM part_data {where} ORDER BY partid)
            ORDER BY partid";

        var data = await db.QueryAsync<PartData>(sql,
            new { Kw = $"%{criteria.Keyword}%", criteria.ClassId });

        return new PagedResult<PartData>(data, total, page, pageSize);
    }

    public async Task<IEnumerable<PartData>> SearchAsync(string keyword)
    {
        using var db = await CreateConnectionAsync();
        var sql = $@"SELECT {PartColumns} FROM part_data
            WHERE (DEL IS NULL OR DEL = '0')
              AND (name LIKE @Kw OR partno LIKE @Kw OR name_py LIKE @Kw
                   OR carname LIKE @Kw OR cartype LIKE @Kw OR cartype_py LIKE @Kw
                   OR [class] LIKE @Kw OR memo LIKE @Kw)
            ORDER BY partid";
        return await db.QueryAsync<PartData>(sql, new { Kw = $"%{keyword}%" });
    }

    public async Task<int> InsertAsync(PartData entity, IDbTransaction? transaction = null)
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
            // 乐观并发：不加锁获取 MAX(partid)+1，INSERT 后若 partid 唯一索引冲突则重试
            const int maxRetries = 3;
            for (int attempt = 0; attempt < maxRetries; attempt++)
            {
                var newId = await db.QuerySingleAsync<long>(
                    "SELECT ISNULL(MAX(partid), 0) + 1 FROM part_data",
                    transaction);
                entity.Partid = newId;
                var sql = @"INSERT INTO part_data (partid, partno, name, carname, cartype, unit, [class], area,
                    inprice, lsprice, pfprice, name_py, cartype_py, memo)
                    VALUES (@Partid, @Partno, @Name, @Carname, @Cartype, @Unit, @ClassName, @Area,
                    @Inprice, @Lsprice, @Pfprice, @NamePy, @CartypePy, @Memo)";
                try
                {
                    return await db.ExecuteAsync(sql, entity, transaction);
                }
                catch (Exception ex) when (attempt < maxRetries - 1 &&
                    (ex.Message.Contains("违反了 PRIMARY KEY 约束") ||
                     ex.Message.Contains("Violation of PRIMARY KEY constraint") ||
                     ex.Message.Contains("重复键") ||
                     ex.Message.Contains("duplicate key")))
                {
                    // partid 冲突，重试获取新的 MAX+1
                    Serilog.Log.Warning("PartData INSERT partid冲突(partid={Partid})，第{Attempt}次重试", newId, attempt + 1);
                }
            }
            // 最终重试仍失败，抛出最后一次异常
            throw new InvalidOperationException($"插入配件失败：partid分配冲突，已重试{maxRetries}次");
        }
        finally
        {
            if (ownsConnection) db.Dispose();
        }
    }

    public async Task<int> UpdateAsync(PartData entity)
    {
        using var db = await CreateConnectionAsync();
        var sql = @"UPDATE part_data SET partno=@Partno, name=@Name, carname=@Carname, cartype=@Cartype,
                    unit=@Unit, [class]=@ClassName, area=@Area, inprice=@Inprice, lsprice=@Lsprice,
                    pfprice=@Pfprice, name_py=@NamePy, cartype_py=@CartypePy, memo=@Memo
                    WHERE partid=@Partid";
        return await db.ExecuteAsync(sql, entity);
    }

    public async Task<int> LogicDeleteAsync(long partid)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync("UPDATE part_data SET DEL='1' WHERE partid=@Id", new { Id = partid });
    }

    public async Task<int> IncreaseStockAsync(long partid, decimal quantity, IDbTransaction? transaction = null, IDbConnection? conn = null)
    {
        var db = conn ?? transaction?.Connection ?? await CreateConnectionAsync();
        // 行级UPDATE本身是原子操作，无需UPDLOCK+HOLDLOCK
        var result = await db.ExecuteAsync(@"
            UPDATE part_stock SET amount = ISNULL(amount,0) + @Qty
            WHERE partid = @Pid", new { Qty = quantity, Pid = partid }, transaction);
        // 如果没有记录（新配件），则插入
        if (result == 0 && transaction == null)
        {
            result = await db.ExecuteAsync(@"
                INSERT INTO part_stock (partid, amount) VALUES (@Pid, @Qty)",
                new { Pid = partid, Qty = quantity });
        }
        else if (result == 0 && transaction != null)
        {
            result = await db.ExecuteAsync(@"
                INSERT INTO part_stock (partid, amount) VALUES (@Pid, @Qty)",
                new { Pid = partid, Qty = quantity }, transaction);
        }
        if (transaction == null && conn == null) db.Dispose();
        return result;
    }

    public async Task<int> DecreaseStockAsync(long partid, decimal quantity, IDbTransaction? transaction = null, IDbConnection? conn = null)
    {
        var db = conn ?? transaction?.Connection ?? await CreateConnectionAsync();
        // 行级UPDATE本身是原子操作，WHERE条件保护库存不足时不扣减
        var result = await db.ExecuteAsync(@"
            UPDATE part_stock SET amount = ISNULL(amount,0) - @Qty
            WHERE partid = @Pid AND ISNULL(amount,0) >= @Qty", new { Qty = quantity, Pid = partid }, transaction);
        if (transaction == null && conn == null) db.Dispose();
        return result;
    }

    public async Task<PartStock?> GetStockByIdAsync(long partId, IDbTransaction? transaction = null, IDbConnection? conn = null)
    {
        var ownsConn = (conn == null && transaction == null);
        using var db = conn ?? transaction?.Connection ?? await CreateConnectionAsync();
        var sql = "SELECT partid, amount, warning, sell_use, buy_use FROM part_stock WHERE partid = @PartId";
        return await db.QueryFirstOrDefaultAsync<PartStock>(sql, new { PartId = partId }, transaction);
    }

    public async Task<IEnumerable<PartStockDisplay>> GetStockListAsync(string? keyword = null, int top = 0)
    {
        using var db = await CreateConnectionAsync();
        // SQL Server 2000 在多列+ORDER BY 时会生成低效执行计划（9秒）。
        // 方案：子查询先取 partid（轻量排序），外层再 JOIN 取所有列（走主键，快）。
        // 实测：直接查 9366ms，子查询方案 22ms。
        // 注意：SQL Server 2000 要求子查询中有 ORDER BY 必须配合 TOP，故 top<=0 时用 TOP 999999999。
        var topClause = top > 0 ? $"TOP {top}" : "TOP 999999999";
        var innerWhere = "WHERE ISNULL(part_data.DEL, '0') = '0'";
        if (!string.IsNullOrEmpty(keyword))
            innerWhere += " AND (part_data.name LIKE @Kw OR part_data.partno LIKE @Kw OR part_data.name_py LIKE @Kw OR part_data.carname LIKE @Kw OR part_data.cartype LIKE @Kw OR part_data.cartype_py LIKE @Kw OR part_data.[class] LIKE @Kw OR part_data.memo LIKE @Kw)";
        var sql = $@"SELECT part_data.partid AS PartId, part_data.partno AS PartNo, part_data.name AS Name, part_data.cartype AS CarType,
                    part_data.carname AS CarName, part_stock.place AS Place, part_data.unit AS Unit, part_data.[class] AS Class,
                    part_data.area AS Area, part_data.inprice AS InPrice, part_stock.amount AS Amount,
                    part_stock.lsprice AS LsPrice, part_stock.pfprice AS PfPrice, part_stock.sell_use AS SellUse,
                    part_data.memo AS Memo, part_data.isck AS Isck, part_stock.warning AS Warning,
                    part_data.part_th AS PartTh, part_data.part_gg AS PartGg,
                    part_data.part_cclb AS PartCclb, part_data.name_py AS NamePy, part_data.cartype_py AS CartypePy,
                    part_data.part_bzq AS PartBzq, part_data.part_bzrq AS PartBzrq
                    FROM (SELECT {topClause} part_stock.partid, part_data.partno FROM part_stock
                          LEFT JOIN part_data ON part_data.partid = part_stock.partid
                          {innerWhere}
                          ORDER BY part_data.partno ASC) AS t
                    INNER JOIN part_data ON part_data.partid = t.partid
                    INNER JOIN part_stock ON part_stock.partid = t.partid";
        var result = await db.QueryAsync<PartStockDisplay>(sql, new { Kw = $"%{keyword}%" });
        return result;
    }

    public async Task<IEnumerable<PartStockDisplay>> GetStockListAdvancedAsync(
        string? partNo = null, string? partName = null, string? partNamePy = null,
        string? cartype = null, string? cartypePy = null,
        string? className = null, string? classPy = null, int queryMode = 3)
    {
        using var db = await CreateConnectionAsync();
        // SQL Server 2000 在多列+ORDER BY 时会生成低效执行计划（9秒）。
        // 方案：子查询先取 partid（轻量排序），外层再 JOIN 取所有列（走主键，快）。
        var innerWhere = "WHERE ISNULL(part_data.DEL, '0') = '0'";

        var parameters = new DynamicParameters();

        // 判断是否为拼音模式（纯ASCII输入，有拼音首字母可走索引）。
        // 额外要求输入全为字母数字：含 *、- 等符号的输入（如尺寸 "25*50*"）是字面匹配意图而非拼音缩写，
        // 否则拼音转换会丢弃符号（"25*50*"→"2550"），只搜 name_py 导致查不到。
        var nameIsPinyin = !string.IsNullOrEmpty(partNamePy) && IsPlainAlphanumeric(partName ?? "");
        var cartypeIsPinyin = !string.IsNullOrEmpty(cartypePy) && IsPlainAlphanumeric(cartype ?? "");

        if (!string.IsNullOrEmpty(partNo))
        {
            var (op, value) = BuildQueryExpression(queryMode, partNo);
            // 编号框：只搜partno字段，不跨字段搜索
            if (IsPureAscii(partNo))
            {
                var pyValue = BuildQueryValue(queryMode, partNo.ToLowerInvariant());
                innerWhere += $" AND (part_data.partno {op} @PartNoKw)";
                parameters.Add("PartNoKw", value);
            }
            else
            {
                innerWhere += $" AND (part_data.partno {op} @PartNoKw)";
                parameters.Add("PartNoKw", value);
            }
        }

        if (!string.IsNullOrEmpty(partName))
        {
            var (op, value) = BuildQueryExpression(queryMode, partName);
            if (nameIsPinyin)
            {
                // 拼音模式：name_py走索引，不跨字段搜索
                var pyValue = BuildQueryValue(queryMode, partNamePy!);
                innerWhere += $" AND (part_data.name_py {op} @NamePyKw)";
                parameters.Add("NamePyKw", pyValue);
            }
            else if (IsPureAscii(partName))
            {
                // 纯ASCII输入（拼音缩写）自动转为拼音搜索 name_py，同时保留中文 name 字段LIKE
                var py = partName.ToLowerInvariant();
                var pyValue = BuildQueryValue(queryMode, py);
                innerWhere += $" AND (part_data.name_py {op} @NamePyKw OR part_data.name {op} @NameKw)";
                parameters.Add("NamePyKw", pyValue);
                parameters.Add("NameKw", value);
            }
            else
            {
                // 中文模式：只搜name字段，不跨字段搜索
                innerWhere += $" AND (part_data.name {op} @NameKw)";
                parameters.Add("NameKw", value);
            }
        }

        if (!string.IsNullOrEmpty(cartype))
        {
            var (op, value) = BuildQueryExpression(queryMode, cartype);
            if (cartypeIsPinyin)
            {
                // 拼音模式：cartype_py走索引，去掉carname的LIKE
                var pyValue = BuildQueryValue(queryMode, cartypePy!);
                innerWhere += $" AND (part_data.cartype_py {op} @CartypePyKw)";
                parameters.Add("CartypePyKw", pyValue);
            }
            else if (IsPureAscii(cartype))
            {
                // 纯ASCII输入（拼音缩写）自动转为拼音搜索 cartype_py，同时保留中文 cartype 字段LIKE
                var py = cartype.ToLowerInvariant();
                var pyValue = BuildQueryValue(queryMode, py);
                innerWhere += $" AND (part_data.cartype_py {op} @CartypePyKw OR part_data.cartype {op} @CartypeKw OR part_data.carname {op} @CartypeKw)";
                parameters.Add("CartypePyKw", pyValue);
                parameters.Add("CartypeKw", value);
            }
            else
            {
                // 中文模式：保留cartype+carname的LIKE
                innerWhere += $" AND (part_data.cartype {op} @CartypeKw OR part_data.carname {op} @CartypeKw)";
                parameters.Add("CartypeKw", value);
            }
        }

        if (!string.IsNullOrEmpty(className))
        {
            var (op, value) = BuildQueryExpression(queryMode, className);
            innerWhere += $" AND (part_data.[class] {op} @ClassKw)";
            parameters.Add("ClassKw", value);
        }

        var sql = $@"SELECT part_data.partid AS PartId, part_data.partno AS PartNo, part_data.name AS Name, part_data.cartype AS CarType,
                    part_data.carname AS CarName, part_stock.place AS Place, part_data.unit AS Unit, part_data.[class] AS Class,
                    part_data.area AS Area, part_data.inprice AS InPrice, part_stock.amount AS Amount,
                    part_stock.lsprice AS LsPrice, part_stock.pfprice AS PfPrice, part_stock.sell_use AS SellUse,
                    part_data.memo AS Memo, part_data.isck AS Isck, part_stock.warning AS Warning,
                    part_data.part_th AS PartTh, part_data.part_gg AS PartGg,
                    part_data.part_cclb AS PartCclb, part_data.name_py AS NamePy, part_data.cartype_py AS CartypePy,
                    part_data.part_bzq AS PartBzq, part_data.part_bzrq AS PartBzrq
                    FROM (SELECT TOP 999999999 part_stock.partid, part_data.partno FROM part_stock
                          LEFT JOIN part_data ON part_data.partid = part_stock.partid
                          {innerWhere}
                          ORDER BY part_data.partno ASC) AS t
                    INNER JOIN part_data ON part_data.partid = t.partid
                    INNER JOIN part_stock ON part_stock.partid = t.partid";
        var result = await db.QueryAsync<PartStockDisplay>(sql, parameters);
        return result;
    }

    /// <summary>
    /// 标签打印数据查询：三条件（编码/名称/车型，均为模糊匹配）。
    /// 名称/车型同时匹配拼音列（name_py / cartype_py），支持拼音查询。
    /// SQL 结构与 GetStockListAsync 完全同构（已验证模式），排序在内存做：
    /// 仓位 → 零件编码，避免 SQL Server 2000 下派生表多列 ORDER BY 的兼容问题。
    /// </summary>
    public async Task<IEnumerable<PartStockDisplay>> GetLabelItemsAsync(string? partNo = null, string? partName = null, string? cartype = null, int top = 0)
    {
        using var db = await CreateConnectionAsync();
        var topClause = top > 0 ? $"TOP {top}" : "TOP 999999999";
        var innerWhere = "WHERE ISNULL(part_data.DEL, '0') = '0'";
        var p = new DynamicParameters();
        if (!string.IsNullOrWhiteSpace(partNo)) { innerWhere += " AND part_data.partno LIKE @PartNo"; p.Add("PartNo", $"%{partNo.Trim()}%"); }
        if (!string.IsNullOrWhiteSpace(partName)) { innerWhere += " AND (part_data.name LIKE @PartName OR part_data.name_py LIKE @PartName)"; p.Add("PartName", $"%{partName.Trim()}%"); }
        if (!string.IsNullOrWhiteSpace(cartype)) { innerWhere += " AND (part_data.cartype LIKE @CarType OR part_data.cartype_py LIKE @CarType)"; p.Add("CarType", $"%{cartype.Trim()}%"); }

        var sql = $@"SELECT part_data.partid AS PartId, part_data.partno AS PartNo, part_data.name AS Name, part_data.cartype AS CarType,
                    part_data.carname AS CarName, part_stock.place AS Place, part_data.unit AS Unit, part_data.[class] AS Class,
                    part_data.area AS Area, part_data.inprice AS InPrice, part_stock.amount AS Amount,
                    part_stock.lsprice AS LsPrice, part_stock.pfprice AS PfPrice, part_stock.sell_use AS SellUse,
                    part_data.memo AS Memo, part_data.isck AS Isck, part_stock.warning AS Warning,
                    part_data.part_th AS PartTh, part_data.part_gg AS PartGg,
                    part_data.part_cclb AS PartCclb, part_data.name_py AS NamePy, part_data.cartype_py AS CartypePy,
                    part_data.part_bzq AS PartBzq, part_data.part_bzrq AS PartBzrq
                    FROM (SELECT {topClause} part_stock.partid, part_data.partno FROM part_stock
                          LEFT JOIN part_data ON part_data.partid = part_stock.partid
                          {innerWhere}) AS t
                    INNER JOIN part_data ON part_data.partid = t.partid
                    INNER JOIN part_stock ON part_stock.partid = t.partid";
        var result = (await db.QueryAsync<PartStockDisplay>(sql, p)).ToList();
        // 内存排序：仓位 → 零件编码（稳定排序，规避兼容层对多列 ORDER BY 的处理差异）
        return result.OrderBy(r => r.Place).ThenBy(r => r.PartNo, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 按多个配件编号包含匹配查询（多条件查询弹窗用）。
    /// 每个输入编码按 LIKE '%code%' 匹配 part_data.partno，多个编码间为 OR 关系。
    /// 复用 GetStockListAdvancedAsync 的子查询+外层JOIN优化骨架（规避 SQL Server 2000 多列+ORDER BY 慢执行计划）。
    /// 只搜 part_data.partno，不跨字段（坑 #18）。
    /// 按 500 个/批分片查询合并，规避 SQL Server 单条语句 2100 参数上限。
    /// </summary>
    public async Task<IEnumerable<PartStockDisplay>> GetStockListByCodesAsync(IEnumerable<string> partNos)
    {
        var codeList = partNos?.Where(c => !string.IsNullOrWhiteSpace(c))
                               .Select(c => c.Trim())
                               .Distinct()
                               .ToList() ?? new List<string>();
        if (codeList.Count == 0) return new List<PartStockDisplay>();

        using var db = await CreateConnectionAsync();
        // 列与 GetStockListAdvancedAsync 完全一致
        const string selectColumns = @"SELECT part_data.partid AS PartId, part_data.partno AS PartNo, part_data.name AS Name, part_data.cartype AS CarType,
                    part_data.carname AS CarName, part_stock.place AS Place, part_data.unit AS Unit, part_data.[class] AS Class,
                    part_data.area AS Area, part_data.inprice AS InPrice, part_stock.amount AS Amount,
                    part_stock.lsprice AS LsPrice, part_stock.pfprice AS PfPrice, part_stock.sell_use AS SellUse,
                    part_data.memo AS Memo, part_data.isck AS Isck, part_stock.warning AS Warning,
                    part_data.part_th AS PartTh, part_data.part_gg AS PartGg,
                    part_data.part_cclb AS PartCclb, part_data.name_py AS NamePy, part_data.cartype_py AS CartypePy,
                    part_data.part_bzq AS PartBzq, part_data.part_bzrq AS PartBzrq";

        var allResults = new List<PartStockDisplay>();
        const int batchSize = 500;
        for (int i = 0; i < codeList.Count; i += batchSize)
        {
            var batch = codeList.Skip(i).Take(batchSize).ToList();
            // 动态构建 OR LIKE 条件，每个编码一个参数化 LIKE，避免 SQL 注入
            var parameters = new DynamicParameters();
            var likeClauses = new StringBuilder();
            for (int j = 0; j < batch.Count; j++)
            {
                if (j > 0) likeClauses.Append(" OR ");
                likeClauses.Append($"part_data.partno LIKE @Code{j}");
                parameters.Add($"Code{j}", $"%{batch[j]}%");
            }
            // 子查询先取 partid（轻量排序），外层 JOIN 取全列；TOP 999999999 满足 SQL Server 2000 子查询 ORDER BY 必须配 TOP（坑 #5）
            var sql = $@"{selectColumns}
                    FROM (SELECT TOP 999999999 part_stock.partid, part_data.partno FROM part_stock
                          LEFT JOIN part_data ON part_data.partid = part_stock.partid
                          WHERE ISNULL(part_data.DEL, '0') = '0' AND ({likeClauses})
                          ORDER BY part_data.partno ASC) AS t
                    INNER JOIN part_data ON part_data.partid = t.partid
                    INNER JOIN part_stock ON part_stock.partid = t.partid";
            var batchResult = await db.QueryAsync<PartStockDisplay>(sql, parameters);
            allResults.AddRange(batchResult);
        }
        // 合并后按编号升序排序
        allResults.Sort((a, b) => string.CompareOrdinal(a.PartNo, b.PartNo));
        return allResults;
    }

    /// <summary>判断字符串是否为纯ASCII字符（拼音/编号搜索意图）</summary>
    private static bool IsPureAscii(string text)
    {
        foreach (char c in text)
            if (c > 127) return false;
        return true;
    }

    /// <summary>判断字符串是否全由字母数字组成（可作为纯拼音缩写搜索意图）</summary>
    private static bool IsPlainAlphanumeric(string text)
    {
        foreach (char c in text)
        {
            if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9'))
                return false;
        }
        return true;
    }

    private static (string op, object value) BuildQueryExpression(int queryMode, string fieldValue)
    {
        return queryMode switch
        {
            0 => ("=", fieldValue),
            1 => ("LIKE", fieldValue + "%"),
            2 => ("LIKE", "%" + fieldValue),
            _ => ("LIKE", "%" + fieldValue + "%")
        };
    }

    private static object BuildQueryValue(int queryMode, string fieldValue)
    {
        return queryMode switch
        {
            0 => fieldValue,
            1 => fieldValue + "%",
            2 => "%" + fieldValue,
            _ => "%" + fieldValue + "%"
        };
    }

    public async Task<long> GetOrCreateWasteStockAsync(long originalPartId, int quantity, IDbTransaction? transaction = null, IDbConnection? conn = null)
    {
        var ownsConnection = (conn == null && transaction == null);
        IDbConnection db;
        if (conn != null)
        {
            db = conn;
        }
        else if (transaction != null)
        {
            db = transaction.Connection!;
        }
        else
        {
            db = await CreateConnectionAsync();
        }

        try
        {
            var originalPart = await db.QueryFirstOrDefaultAsync<PartData>(
                $"SELECT {PartColumns} FROM part_data WHERE partid = @Id", new { Id = originalPartId }, transaction);
            if (originalPart == null) return originalPartId;

            var wasteStock = await db.QueryFirstOrDefaultAsync<dynamic>(
                @"SELECT s.partid, s.amount FROM part_stock s 
              INNER JOIN part_data d ON d.partid = s.partid 
              WHERE d.partno = @PartNo AND d.name = @Name AND s.place = '废品仓'
                AND (d.DEL IS NULL OR d.DEL = '0')",
                new { PartNo = originalPart.Partno, Name = originalPart.Name }, transaction);

            if (wasteStock != null)
            {
                var wastePartId = (long)wasteStock.partid;
                await db.ExecuteAsync(
                    "UPDATE part_stock SET amount = ISNULL(amount,0) + @Qty WHERE partid = @Pid",
                    new { Qty = (decimal)quantity, Pid = wastePartId }, transaction);
                return wastePartId;
            }

            // 乐观并发：不加锁分配 partid，INSERT 若冲突则重试
            long newPartId;
            const int maxRetries = 3;
            int insertAttempt = 0;
            while (true)
            {
                newPartId = await db.QuerySingleAsync<long>(
                    "SELECT ISNULL(MAX(partid), 0) + 1 FROM part_data",
                    transaction);
                try
                {
                    await db.ExecuteAsync(
                        @"INSERT INTO part_data (partid, partno, name, carname, cartype, unit, [class], area, place,
                    inprice, lsprice, pfprice, name_py, carname_py, cartype_py, DEL)
            VALUES (@PartId, @PartNo, @Name, @CarName, @CarType, @Unit, @Class, @Area, '废品仓',
                    @InPrice, @LsPrice, @PfPrice, @NamePy, @CarNamePy, @CarTypePy, '0')",
                        new
                        {
                            PartId = newPartId,
                            PartNo = originalPart.Partno,
                            Name = originalPart.Name,
                            CarName = originalPart.Carname,
                            CarType = originalPart.Cartype,
                            Unit = originalPart.Unit,
                            Class = originalPart.ClassName,
                            Area = originalPart.Area,
                            InPrice = originalPart.Inprice,
                            LsPrice = originalPart.Lsprice,
                            PfPrice = originalPart.Pfprice,
                            NamePy = originalPart.NamePy,
                            CarNamePy = originalPart.CarnamePy,
                            CarTypePy = originalPart.CartypePy
                        }, transaction);
                    break;
                }
                catch (Exception ex) when (insertAttempt < maxRetries - 1 &&
                    (ex.Message.Contains("违反了 PRIMARY KEY 约束") ||
                     ex.Message.Contains("Violation of PRIMARY KEY constraint") ||
                     ex.Message.Contains("重复键") ||
                     ex.Message.Contains("duplicate key")))
                {
                    insertAttempt++;
                    Serilog.Log.Warning("WasteStock INSERT partid冲突(partid={Partid})，第{Attempt}次重试", newPartId, insertAttempt);
                }
                if (insertAttempt >= maxRetries - 1)
                    throw new InvalidOperationException($"创建废品配件失败：partid分配冲突，已重试{maxRetries}次");
            }

            await db.ExecuteAsync(
                @"INSERT INTO part_stock (partid, place, amount, lsprice, pfprice)
              VALUES (@PartId, '废品仓', @Qty, @LsPrice, @PfPrice)",
                new { PartId = newPartId, Qty = (decimal)quantity, LsPrice = originalPart.Lsprice, PfPrice = originalPart.Pfprice }, transaction);

            return newPartId;
        }
        finally
        {
            if (ownsConnection) db.Dispose();
        }
    }

    /// <summary>
    /// 减少废品仓库存（编辑退货单减少退货时使用）
    /// </summary>
    public async Task DecreaseWasteStockAsync(long originalPartId, int quantity, IDbTransaction? transaction = null, IDbConnection? conn = null)
    {
        var ownsConnection = (conn == null && transaction == null);
        IDbConnection db;
        if (conn != null)
            db = conn;
        else if (transaction != null)
            db = transaction.Connection!;
        else
            db = await CreateConnectionAsync();

        try
        {
            var originalPart = await db.QueryFirstOrDefaultAsync<PartData>(
                $"SELECT {PartColumns} FROM part_data WHERE partid = @Id", new { Id = originalPartId }, transaction);
            if (originalPart == null) return;

            // 行级UPDATE原子操作，无需悲观锁
            await db.ExecuteAsync(
                @"UPDATE part_stock SET amount = ISNULL(amount,0) - @Qty
                  WHERE place = '废品仓' AND partid IN (
                      SELECT s.partid FROM part_stock s
                      INNER JOIN part_data d ON d.partid = s.partid
                      WHERE d.partno = @PartNo AND d.name = @Name AND s.place = '废品仓'
                        AND (d.DEL IS NULL OR d.DEL = '0')
                  )",
                new { Qty = (decimal)quantity, PartNo = originalPart.Partno, Name = originalPart.Name }, transaction);
        }
        finally
        {
            if (ownsConnection) db.Dispose();
        }
    }

    /// <summary>
    /// 批量查询配件信息（解决 N+1 查询问题）
    /// </summary>
    public async Task<Dictionary<long, PartData>> GetByIdsAsync(IEnumerable<long> partIds)
    {
        var idList = partIds?.Distinct().ToList() ?? new List<long>();
        if (idList.Count == 0) return new Dictionary<long, PartData>();

        using var db = await CreateConnectionAsync();
        var result = await db.QueryAsync<PartData>(
            $"SELECT {PartColumns} FROM part_data WHERE partid IN @Ids",
            new { Ids = idList });
        return result.ToDictionary(p => p.Partid);
    }

    // IRepository<PartData> 显式实现
    Task<PartData?> IRepository<PartData>.GetByIdAsync(object id) => GetByIdAsync(Convert.ToInt64(id));
    Task<int> IRepository<PartData>.UpdateAsync(PartData entity, IDbTransaction? transaction) => throw new NotImplementedException("请使用 UpdateAsync(PartData entity)");
    Task<int> IRepository<PartData>.DeleteAsync(object id, IDbTransaction? transaction) => throw new NotImplementedException();
    Task<int> IRepository<PartData>.CountAsync() => throw new NotImplementedException();

    public async Task<IEnumerable<StockAlertItem>> GetStockAlertItemsAsync()
    {
        using var db = await CreateConnectionAsync();
        // 配件级预警：已设预警值(amount <= warning) 或 预警值未设置(amount=0)
        var sql = @"SELECT s.partid AS PartId, p.partno AS Partno, p.name AS Name, p.name_py AS NamePy, p.cartype AS Cartype, p.cartype_py AS CartypePy,
                    p.unit AS Unit,
                    ISNULL(s.amount,0) AS Amount, ISNULL(s.warning,0) AS Warning,
                    s.lsprice AS Lsprice, p.[class] AS ClassName, s.place AS Place
                    FROM part_stock s
                    INNER JOIN part_data p ON p.partid = s.partid
                    WHERE (p.DEL IS NULL OR p.DEL = '0')
                      AND (ISNULL(s.warning,0) = 0 OR ISNULL(s.amount,0) <= ISNULL(s.warning,0))
                    ORDER BY s.amount";
        return await db.QueryAsync<StockAlertItem>(sql);
    }

    public async Task<int> UpdateWarningAsync(long partId, decimal warning)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync(
            "UPDATE part_stock SET warning = @Warning WHERE partid = @PartId",
            new { Warning = warning, PartId = partId });
    }

    public async Task<List<PinyinFixRow>> GetMissingPinyinAsync()
    {
        using var db = await CreateConnectionAsync();
        // 全量校正：返回所有未删除且有 name/cartype 的记录，由 C# 端重新生成拼音并与原值比较
        // 这样可覆盖所有不一致情况（问号、空、数字丢失、以及其他编码错误）
        var sql = @"SELECT partid AS PartId, partno AS Partno, name AS Name, name_py AS NamePy,
                    cartype AS Cartype, cartype_py AS CartypePy
                    FROM part_data
                    WHERE (DEL IS NULL OR DEL = '0')
                      AND (
                        (name IS NOT NULL AND name <> '')
                        OR (cartype IS NOT NULL AND cartype <> '')
                      )
                    ORDER BY partno";
        var rows = await db.QueryAsync<PinyinFixRow>(sql);
        return rows.AsList();
    }

    public async Task<int> UpdatePinyinAsync(long partId, string? namePy, string? cartypePy)
    {
        using var db = await CreateConnectionAsync();
        return await db.ExecuteAsync(
            "UPDATE part_data SET name_py = @NamePy, cartype_py = @CartypePy WHERE partid = @PartId",
            new { NamePy = namePy, CartypePy = cartypePy, PartId = partId });
    }
}
