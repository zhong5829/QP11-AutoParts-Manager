using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

/// <summary>操作类型下拉选项（中文显示 + 原始代码值）</summary>
public class ActionItem
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public override string ToString() => Name;
}

public partial class SysLogWindow : Window
{
    private readonly ISysLogRepository _repo;
    private readonly IUserRepository _userRepo;
    public ObservableCollection<SysLog> Logs { get; } = new();

    /// <summary>操作代码 → 中文映射（支持前缀匹配，如 "ue_add -1" → "新增"）</summary>
    private static readonly Dictionary<string, string> ActionNameMap = new()
    {
        ["of_pic"] = "图片上传",
        ["open"] = "打开",
        ["resize"] = "窗口调整",
        ["ue_add"] = "新增",
        ["ue_edit"] = "编辑",
        ["ue_delete"] = "删除",
        ["ue_cancel"] = "取消",
        ["ue_save"] = "保存",
        ["ue_settle"] = "结算",
        ["ue_print"] = "打印",
        ["ue_return"] = "退货",
        ["ue_chg"] = "修改",
        ["ue_query"] = "查询",
        ["ue_ref"] = "刷新",
        ["ue_export"] = "导出",
        ["ue_import"] = "导入",
        ["ue_login"] = "登录",
        ["ue_logout"] = "登出",
        ["ue_set_hide"] = "设置隐藏",
        ["ue_view"] = "查看",
        ["wf_update"] = "更新",
        ["web_start"] = "启动Web服务",
        ["web_stop"] = "停止Web服务",
    };

    /// <summary>根据原始代码查找中文名称（前缀匹配）</summary>
    private static string MapActionName(string rawCode)
    {
        if (string.IsNullOrEmpty(rawCode)) return "";
        // 精确匹配
        if (ActionNameMap.TryGetValue(rawCode, out var exact)) return exact;
        // 前缀匹配（如 "ue_add -1" → 取 "ue_add" 前缀）
        var prefix = rawCode.Split(' ')[0].Trim();
        return ActionNameMap.TryGetValue(prefix, out var name) ? name : rawCode;
    }

    public SysLogWindow(ISysLogRepository repo, IUserRepository userRepo)
    {
        _repo = repo;
        _userRepo = userRepo;
        InitializeComponent();
        dgLogs.ItemsSource = Logs;
        dtStart.SelectedDate = DateTime.Now.AddDays(-7);
        dtEnd.SelectedDate = DateTime.Now;
        // 注意: 此窗口可能通过 WindowHostControl 托管(非独立Show)，Loaded 事件不会触发
        // 因此数据加载放在构造函数中异步执行
        _ = LoadDropdownsAsync();
    }

    private async Task LoadDropdownsAsync()
    {
        // 加载用户列表
        try
        {
            var users = (await _userRepo.GetAllAsync()).ToList();
            users.Insert(0, new UserInfor { Username = "", Name = "（全部）" });
            cboUser.ItemsSource = users;
            cboUser.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载用户列表失败: {ex.Message}", "错误");
        }

        // 加载操作类型（去重 + 中文映射）
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            var rawActions = await db.QueryAsync<string>("SELECT DISTINCT action FROM sys_log ORDER BY action");
            var items = rawActions.Select(a => new ActionItem
            {
                Code = a,
                Name = MapActionName(a)
            }).OrderBy(a => a.Name).ToList();
            items.Insert(0, new ActionItem { Code = "", Name = "（全部）" });
            cboAction.ItemsSource = items;
            cboAction.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载操作类型失败: {ex.Message}", "错误");
        }
    }

    private async void BtnQuery_Click(object sender, RoutedEventArgs e)
    {
        var start = dtStart.SelectedDate;
        var end = dtEnd.SelectedDate;
        var uid = cboUser.SelectedValue as string;
        var actionItem = cboAction.SelectedItem as ActionItem;
        var action = actionItem?.Code;

        try
        {
            var (data, total) = await _repo.GetListAsync(1, 50, null, start, end,
                string.IsNullOrEmpty(uid) ? null : uid,
                string.IsNullOrEmpty(action) ? null : action);
            Logs.Clear();
            foreach (var item in data) Logs.Add(item);
            txtCount.Text = $"共 {total} 条记录";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"查询失败: {ex.Message}", "错误");
        }
    }

    private async void BtnClean_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show("确认清理30天前的日志?", "确认", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
            return;

        try
        {
            var count = await _repo.DeleteBeforeAsync(DateTime.Now.AddDays(-30));
            MessageBox.Show($"已清理 {count} 条日志", "提示");
            BtnQuery_Click(sender, e);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"清理失败: {ex.Message}", "错误");
        }
    }
}
