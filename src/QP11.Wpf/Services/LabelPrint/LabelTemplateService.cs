using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace QP11.Wpf.Services.LabelPrint;

/// <summary>标签模板管理：内置预设 + 用户自定义持久化（labeltemplates.json）</summary>
public static class LabelTemplateService
{
    private static readonly string SettingsPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "labeltemplates.json");

    /// <summary>已删除内置模板的名称记录（内置模板代码内定义，删除需持久化排除名单）</summary>
    private static readonly string RemovedBuiltInPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "removed_builtin_templates.json");

    /// <summary>模板→打印机 绑定映射（内置/自定义模板统一存此，打印时绑定优先）</summary>
    private static readonly string PrinterMapPath =
        Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "labeltemplate_printers.json");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>内置模板（热敏标签常见规格）</summary>
    public static List<LabelTemplate> BuiltInTemplates() => new()
    {
        new LabelTemplate { Name = "40×30", LabelWidthMm = 40, LabelHeightMm = 30, ColsPerRow = 1, FontSizeCode = 13, FontSizeName = 10, FontSizeCarType = 8 },
        new LabelTemplate { Name = "50×30", LabelWidthMm = 50, LabelHeightMm = 30, ColsPerRow = 1, FontSizeCode = 14, FontSizeName = 11, FontSizeCarType = 9 },
        new LabelTemplate { Name = "50×40", LabelWidthMm = 50, LabelHeightMm = 40, ColsPerRow = 1, FontSizeCode = 14, FontSizeName = 11, FontSizeCarType = 9 },
        new LabelTemplate { Name = "60×40", LabelWidthMm = 60, LabelHeightMm = 40, ColsPerRow = 1, FontSizeCode = 14, FontSizeName = 11, FontSizeCarType = 9 },
        new LabelTemplate { Name = "70×50", LabelWidthMm = 70, LabelHeightMm = 50, ColsPerRow = 1, FontSizeCode = 15, FontSizeName = 12, FontSizeCarType = 10 },
        new LabelTemplate { Name = "100×60", LabelWidthMm = 100, LabelHeightMm = 60, ColsPerRow = 1, FontSizeCode = 18, FontSizeName = 14, FontSizeCarType = 11 },
    };

    /// <summary>全部模板 = 内置(已删除的内置剔除) + 自定义</summary>
    public static List<LabelTemplate> GetAll()
    {
        var removed = LoadRemovedBuiltIns();
        var list = BuiltInTemplates().Where(t => !removed.Contains(t.Name)).ToList();
        foreach (var c in LoadCustoms())
        {
            c.IsBuiltIn = false;
            list.Add(c);
        }
        return list;
    }

    /// <summary>读取已删除的内置模板名称</summary>
    public static HashSet<string> LoadRemovedBuiltIns()
    {
        try
        {
            if (File.Exists(RemovedBuiltInPath))
                return JsonSerializer.Deserialize<HashSet<string>>(File.ReadAllText(RemovedBuiltInPath), JsonOptions) ?? new HashSet<string>();
        }
        catch { }
        return new HashSet<string>();
    }

    /// <summary>持久化已删除的内置模板名称</summary>
    private static void SaveRemovedBuiltIns(HashSet<string> removed)
    {
        try
        {
            File.WriteAllText(RemovedBuiltInPath, JsonSerializer.Serialize(removed, JsonOptions));
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "保存内置模板删除记录失败");
        }
    }

    /// <summary>读取用户自定义模板</summary>
    public static List<LabelTemplate> LoadCustoms()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return JsonSerializer.Deserialize<List<LabelTemplate>>(File.ReadAllText(SettingsPath), JsonOptions) ?? new();
        }
        catch { }
        return new List<LabelTemplate>();
    }

    /// <summary>保存用户自定义模板（内置模板不落盘）</summary>
    public static void SaveCustoms(IEnumerable<LabelTemplate> customs)
    {
        var toSave = customs.Where(c => !c.IsBuiltIn).ToList();
        try
        {
            File.WriteAllText(SettingsPath, JsonSerializer.Serialize(toSave, JsonOptions));
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "保存标签模板失败");
        }
    }

    /// <summary>新增自定义模板</summary>
    public static bool AddCustom(LabelTemplate tpl)
    {
        // 按当前可见模板校验同名（已删除的内置模板允许重建同名自定义模板）
        if (GetAll().Any(t => t.Name == tpl.Name)) return false;
        var customs = LoadCustoms();
        customs.Add(tpl);
        SaveCustoms(customs);
        return true;
    }

    /// <summary>读取模板→打印机绑定映射（模板名→打印机名）</summary>
    public static Dictionary<string, string> LoadPrinterBindings()
    {
        try
        {
            if (File.Exists(PrinterMapPath))
                return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(PrinterMapPath), JsonOptions) ?? new Dictionary<string, string>();
        }
        catch { }
        return new Dictionary<string, string>();
    }

    /// <summary>查询模板绑定的打印机名（未绑定返回 null）</summary>
    public static string? GetBoundPrinter(string templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName)) return null;
        var map = LoadPrinterBindings();
        return map.TryGetValue(templateName.Trim(), out var printer) ? printer : null;
    }

    /// <summary>保存模板→打印机绑定（覆盖旧绑定）</summary>
    public static void SavePrinterBinding(string templateName, string printerName)
    {
        var map = LoadPrinterBindings();
        map[templateName.Trim()] = printerName;
        try
        {
            File.WriteAllText(PrinterMapPath, JsonSerializer.Serialize(map, JsonOptions));
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "保存模板打印机绑定失败");
        }
    }

    /// <summary>解除模板的打印机绑定</summary>
    public static void ClearPrinterBinding(string templateName)
    {
        var map = LoadPrinterBindings();
        if (map.Remove(templateName.Trim()))
        {
            try
            {
                File.WriteAllText(PrinterMapPath, JsonSerializer.Serialize(map, JsonOptions));
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "保存模板打印机绑定失败");
            }
        }
    }

    /// <summary>删除模板：自定义模板直接移除；内置模板记录到删除名单（代码内有定义，需排除显示）</summary>
    public static bool DeleteTemplate(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        name = name.Trim();

        var customs = LoadCustoms();
        var custom = customs.FirstOrDefault(t => t.Name == name);
        if (custom != null)
        {
            customs.Remove(custom);
            SaveCustoms(customs);
        }
        else if (BuiltInTemplates().Any(t => t.Name == name))
        {
            var removed = LoadRemovedBuiltIns();
            removed.Add(name);
            SaveRemovedBuiltIns(removed);
        }
        else
        {
            return false;
        }

        // 删除模板时一并清除其打印机绑定
        ClearPrinterBinding(name);
        return true;
    }

    /// <summary>保存（覆盖）同名自定义模板；内置模板不可保存，返回 false</summary>
    public static bool SaveCustom(LabelTemplate tpl)
    {
        if (tpl == null || tpl.IsBuiltIn) return false;
        var customs = LoadCustoms();
        int idx = customs.FindIndex(t => t.Name == tpl.Name);
        if (idx < 0) return false;   // 无同名自定义模板 → 被视为新建，走 AddCustom
        customs[idx] = tpl.Clone();
        SaveCustoms(customs);
        return true;
    }
}