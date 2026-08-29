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

    /// <summary>全部模板 = 内置 + 自定义</summary>
    public static List<LabelTemplate> GetAll()
    {
        var list = BuiltInTemplates();
        foreach (var c in LoadCustoms())
        {
            c.IsBuiltIn = false;
            list.Add(c);
        }
        return list;
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
        var customs = LoadCustoms();
        // 同名校验（内置名也不允许重复，避免混淆）
        var exists = BuiltInTemplates().Any(t => t.Name == tpl.Name) || customs.Any(t => t.Name == tpl.Name);
        if (exists) return false;
        customs.Add(tpl);
        SaveCustoms(customs);
        return true;
    }

    /// <summary>删除自定义模板</summary>
    public static bool DeleteCustom(string name)
    {
        var customs = LoadCustoms();
        var target = customs.FirstOrDefault(t => t.Name == name);
        if (target == null) return false;
        customs.Remove(target);
        SaveCustoms(customs);
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