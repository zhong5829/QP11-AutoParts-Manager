using Dapper;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using Serilog;

namespace QP11.Services;

/// <summary>
/// 用户权限服务。
/// 权限体系（对应旧库模型）：权限按用户独立存储于 user_infor.auth（逗号分隔的菜单码集合，
/// 如 "all,1,2,3,44,5,6,7"），菜单码对应 mnu.code；auth 含 "all" 表示超级管理员完全放行。
/// groups（角色组）仅作归类展示，不参与权限判断。
/// </summary>
public class PermissionService
{
    private readonly IDbConnectionFactory _dbFactory;

    public PermissionService(IDbConnectionFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    /// <summary>所属角色组ID（仅展示用，权限判断以 auth 为准）</summary>
    public int? UserGroups { get; private set; }

    /// <summary>旧权限规则串字段，保留读取</summary>
    public string? UserRules { get; private set; }

    /// <summary>用户授权菜单码集合（user_infor.auth 解析结果，"all" 表示全部）</summary>
    public HashSet<string> Permissions { get; private set; } = new();

    /// <summary>超级管理员：auth 含 "all" 时完全放行</summary>
    public bool IsSuperAdmin => _permissionsLoaded && Permissions.Contains("all");

    private bool _permissionsLoaded;

    public async Task LoadUserPermissionsAsync(string username)
    {
        Permissions.Clear();
        _permissionsLoaded = false;
        try
        {
            using var db = await _dbFactory.CreateAsync();
            var user = await db.QueryFirstOrDefaultAsync<UserInfor>(
                "SELECT username, name, groups, rules, state, auth FROM user_infor WHERE username = @Username",
                new { Username = username });

            if (user == null)
            {
                _permissionsLoaded = false;
                return;
            }

            UserGroups = user.Groups;
            UserRules = user.Rules;

            var authStr = user.Auth ?? "";
            Permissions = new HashSet<string>(
                authStr.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                StringComparer.OrdinalIgnoreCase);

            _permissionsLoaded = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载用户 {Username} 权限失败", username);
            _permissionsLoaded = false;
        }
    }

    /// <summary>
    /// 判断是否拥有某菜单/按钮权限码（如 "11"、"133"、"78"）。
    /// 匹配规则：auth 含 "all" 全放行；否则权限码与请求码做前缀匹配
    /// （auth=1 可放行 11/118/13/133/138/15/17/18 等以 1 开头的菜单）。
    /// 权限未加载（用户不存在或加载失败）时返回 false，不默认放行。
    /// </summary>
    public bool HasPermission(string permissionCode)
    {
        if (!_permissionsLoaded || string.IsNullOrEmpty(permissionCode)) return false;
        if (Permissions.Contains("all")) return true;

        foreach (var code in Permissions)
        {
            if (permissionCode.Equals(code, StringComparison.OrdinalIgnoreCase) ||
                permissionCode.StartsWith(code, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}