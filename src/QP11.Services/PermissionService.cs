using Dapper;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using Serilog;

namespace QP11.Services;

public class PermissionService
{
    private readonly IDbConnectionFactory _dbFactory;

    public PermissionService(IDbConnectionFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public int UserGroups { get; private set; }
    public string? UserRules { get; private set; }
    public HashSet<string> Permissions { get; private set; } = new();
    public bool IsSuperAdmin => UserGroups == 1;

    private bool _permissionsLoaded;

    public async Task LoadUserPermissionsAsync(string username)
    {
        try
        {
            using var db = await _dbFactory.CreateAsync();

            var user = await db.QueryFirstOrDefaultAsync<UserInfor>(
                "SELECT * FROM user_infor WHERE username = @Username",
                new { Username = username });

            if (user == null)
            {
                _permissionsLoaded = false;
                return;
            }

            UserGroups = user.Groups ?? 4;
            UserRules = user.Rules;

            if (IsSuperAdmin)
            {
                _permissionsLoaded = true;
                return;
            }

            if (string.IsNullOrEmpty(UserRules))
            {
                _permissionsLoaded = false;
                return;
            }

            var mnu = await db.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT auth FROM mnu WHERE code = @Code",
                new { Code = UserRules });

            if (mnu != null)
            {
                var authObj = mnu.auth as string;
                if (!string.IsNullOrEmpty(authObj))
                {
                    Permissions = new HashSet<string>(
                        authObj.Split(',', StringSplitOptions.RemoveEmptyEntries),
                        StringComparer.OrdinalIgnoreCase);
                }
            }

            _permissionsLoaded = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载用户 {Username} 权限失败", username);
            _permissionsLoaded = false;
        }
    }

    public bool HasPermission(string permissionCode)
    {
        if (IsSuperAdmin) return true;
        if (!_permissionsLoaded || Permissions.Count == 0) return true; // 未加载权限时放行
        return Permissions.Contains(permissionCode);
    }
}
