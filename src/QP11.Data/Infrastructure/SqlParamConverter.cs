using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text.RegularExpressions;

namespace QP11.Data.Infrastructure;

/// <summary>
/// SQL参数转换器：将 @命名参数 风格的SQL转换为 ?位置参数 风格。
/// ODBC 和 OLE DB 驱动都不支持 @命名参数，需要此转换。
/// 转换结果按SQL文本缓存，避免重复解析。
/// </summary>
public static class SqlParamConverter
{
    private record ParsedSql(string ConvertedSql, List<string> ParamNames);
    private static readonly ConcurrentDictionary<string, ParsedSql> _cache = new();

    /// <summary>缓存容量上限，超出时清空重建</summary>
    private const int _cacheMaxCapacity = 2000;

    /// <summary>
    /// 转换结果：转换后的SQL和参数名顺序
    /// </summary>
    public readonly struct ConvertResult
    {
        public readonly string ConvertedSql;
        public readonly List<string> ParamNames;
        public ConvertResult(string sql, List<string> names) { ConvertedSql = sql; ParamNames = names; }
    }

    /// <summary>
    /// 将 @命名参数 SQL 转换为 ?位置参数 SQL。
    /// 如果SQL中无命名参数或所有参数都在引号内，返回原SQL。
    /// </summary>
    public static ConvertResult Convert(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
            return new ConvertResult(sql, new List<string>());

        var parsed = _cache.GetOrAdd(sql, s =>
        {
            // 缓存容量安全阀
            if (_cache.Count > _cacheMaxCapacity)
            {
                _cache.Clear();
                Serilog.Log.Warning("SQL参数转换缓存已超容量上限{Max}，已清空重建", _cacheMaxCapacity);
            }

            return ParseInternal(s);
        });

        return new ConvertResult(parsed.ConvertedSql, parsed.ParamNames);
    }

    private static ParsedSql ParseInternal(string sql)
    {
        var paramPositions = new List<(int start, int length, string name)>();
        var matches = Regex.Matches(sql, @"(?<!@)@(?!@)(\w+)");
        foreach (Match m in matches)
        {
            paramPositions.Add((m.Index, m.Length, m.Groups[1].Value));
        }

        if (paramPositions.Count == 0)
            return new ParsedSql(sql, new List<string>());

        // 批量判断引号内位置
        var insideQuotes = GetPositionsInsideQuotes(sql, paramPositions);
        var filtered = paramPositions
            .Where((_, idx) => !insideQuotes.Contains(idx))
            .ToList();

        if (filtered.Count == 0)
            return new ParsedSql(sql, new List<string>());

        // 构建转换后的SQL：@param → ?
        var sb = new System.Text.StringBuilder();
        int lastEnd = 0;
        foreach (var (start, length, _) in filtered)
        {
            sb.Append(sql, lastEnd, start - lastEnd);
            sb.Append('?');
            lastEnd = start + length;
        }
        if (lastEnd < sql.Length)
            sb.Append(sql, lastEnd, sql.Length - lastEnd);

        var paramNames = filtered.Select(p => p.name).ToList();
        return new ParsedSql(sb.ToString(), paramNames);
    }

    /// <summary>
    /// 批量判断哪些匹配位置在单引号字符串内。
    /// SQL 字符串字面量规则：'...' 包裹字符串，字符串内的 '' 表示转义的单引号（字符串不关闭）。
    /// 因此独立的 ''（空字符串）应为“开-关”，而非“开后保持开启”。
    /// 旧实现用 (i==0 || sql[i-1]!='\'') 判断是否切换状态，会把独立的 '' 误判为“开启后未关闭”，
    /// 导致其后所有参数被视为字符串内而被跳过转换（如报损 INSERT 中的 @Worker）。
    /// </summary>
    private static HashSet<int> GetPositionsInsideQuotes(string sql, List<(int start, int length, string name)> positions)
    {
        if (positions.Count == 0) return new HashSet<int>();

        // 预计算每个字符位置是否位于单引号字符串内部
        var inStringAt = new bool[sql.Length];
        bool inSingle = false;
        int i = 0;
        while (i < sql.Length)
        {
            if (sql[i] != '\'')
            {
                inStringAt[i] = inSingle;
                i++;
                continue;
            }

            if (!inSingle)
            {
                // 当前引号开启字符串，引号字符本身不算“内部”
                inStringAt[i] = false;
                inSingle = true;
                i++;
            }
            else if (i + 1 < sql.Length && sql[i + 1] == '\'')
            {
                // 字符串内的转义引号 ''：两个字符均属于字符串内容，字符串不关闭
                inStringAt[i] = true;
                inStringAt[i + 1] = true;
                i += 2;
            }
            else
            {
                // 关闭字符串
                inStringAt[i] = false;
                inSingle = false;
                i++;
            }
        }

        var inside = new HashSet<int>();
        for (int idx = 0; idx < positions.Count; idx++)
        {
            var start = positions[idx].start;
            if (start < inStringAt.Length && inStringAt[start])
                inside.Add(idx);
        }
        return inside;
    }

    /// <summary>
    /// 根据转换结果，将 DbParameter 集合重排为位置参数顺序。
    /// 通用方法，适用于 ODBC / OleDb 等任何 DbParameterCollection。
    /// </summary>
    public static void ApplyToParameters(
        string originalSql,
        DbCommand innerCommand)
    {
        if (innerCommand.CommandType == CommandType.StoredProcedure)
        {
            innerCommand.CommandText = originalSql;
            return;
        }

        if (string.IsNullOrWhiteSpace(originalSql) || innerCommand.Parameters.Count == 0)
        {
            innerCommand.CommandText = originalSql;
            return;
        }

        var result = Convert(originalSql);
        if (result.ParamNames.Count == 0)
        {
            innerCommand.CommandText = originalSql;
            return;
        }

        // 收集现有参数
        var existingParams = new List<(string name, object? value, DbType dbType)>();
        foreach (DbParameter p in innerCommand.Parameters)
        {
            var pname = p.ParameterName?.TrimStart('@') ?? "";
            existingParams.Add((pname, p.Value, p.DbType));
        }

        // 按SQL中的出现顺序重排参数
        var orderedParams = new List<DbParameter>();
        foreach (var name in result.ParamNames)
        {
            var existing = existingParams.FirstOrDefault(p =>
                p.name.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (existing.name != null)
            {
                var newParam = innerCommand.CreateParameter();
                newParam.ParameterName = existing.name;
                newParam.Value = existing.value ?? DBNull.Value;
                newParam.DbType = existing.dbType;
                orderedParams.Add(newParam);
            }
            else
            {
                Serilog.Log.Warning("参数匹配失败: SQL中@{ParamName}在参数列表中未找到. SQL: {Sql}", name, originalSql);
                var newParam = innerCommand.CreateParameter();
                newParam.ParameterName = name;
                newParam.Value = DBNull.Value;
                orderedParams.Add(newParam);
            }
        }

        innerCommand.Parameters.Clear();
        innerCommand.CommandText = result.ConvertedSql;

        foreach (var p in orderedParams)
        {
            innerCommand.Parameters.Add(p);
        }
    }
}
