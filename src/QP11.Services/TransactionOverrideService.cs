using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace QP11.Services;

/// <summary>
/// 往来账手动修改值的本地持久化服务
/// JSON文件按 供应商sid_年份_月份 为key存储覆盖的进货/出货金额
/// </summary>
public class TransactionOverrideService
{
    private readonly string _filePath;
    private Dictionary<string, OverrideEntry>? _cache;

    public TransactionOverrideService()
    {
        var dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
        Directory.CreateDirectory(dir);
        _filePath = Path.Combine(dir, "transaction_overrides.json");
    }

    /// <summary>获取覆盖值，无则返回null</summary>
    public OverrideEntry? GetOverride(string sid, int year, int month)
    {
        EnsureLoaded();
        var key = $"{sid}_{year}_{month}";
        return _cache!.TryGetValue(key, out var entry) ? entry : null;
    }

    /// <summary>保存覆盖值</summary>
    public void SaveOverride(string sid, int year, int month, decimal buyTotal, decimal sellTotal, bool isSettled)
    {
        EnsureLoaded();
        var key = $"{sid}_{year}_{month}";
        _cache![key] = new OverrideEntry { sid = sid, year = year, month = month, buy_total = buyTotal, sell_total = sellTotal, is_settled = isSettled };
        SaveToFile();
    }

    /// <summary>删除覆盖值（恢复为数据库原值）</summary>
    public void RemoveOverride(string sid, int year, int month)
    {
        EnsureLoaded();
        var key = $"{sid}_{year}_{month}";
        if (_cache!.Remove(key))
            SaveToFile();
    }

    private void EnsureLoaded()
    {
        if (_cache != null) return;
        try
        {
            if (File.Exists(_filePath))
            {
                var json = File.ReadAllText(_filePath);
                _cache = JsonSerializer.Deserialize<Dictionary<string, OverrideEntry>>(json)
                         ?? new Dictionary<string, OverrideEntry>();
            }
            else
            {
                _cache = new Dictionary<string, OverrideEntry>();
            }
        }
        catch
        {
            _cache = new Dictionary<string, OverrideEntry>();
        }
    }

    private void SaveToFile()
    {
        try
        {
            var json = JsonSerializer.Serialize(_cache, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
        catch
        {
            // 静默失败，不影响主流程
        }
    }
}

public class OverrideEntry
{
    public string? sid { get; set; }
    public int year { get; set; }
    public int month { get; set; }
    public decimal buy_total { get; set; }
    public decimal sell_total { get; set; }
    public bool is_settled { get; set; }
}
