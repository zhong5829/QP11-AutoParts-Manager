namespace QP11.WebApi.Services;

/// <summary>
/// 在线连接计数器 - 统计当前活跃的已认证用户数
/// 通过 Token 登录/登出/心跳 维护计数
/// </summary>
public static class ConnectionCounter
{
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, DateTime> _activeTokens = new();

    /// <summary>当前在线连接数</summary>
    public static int ActiveCount => _activeTokens.Count;

    /// <summary>
    /// 用户登录成功后调用（记录在线）
    /// </summary>
    public static void OnLogin(string token)
    {
        if (!string.IsNullOrEmpty(token))
            _activeTokens[token] = DateTime.Now;
    }

    /// <summary>
    /// 用户心跳/请求时调用（更新最后活动时间）
    /// </summary>
    public static void OnHeartbeat(string token)
    {
        if (!string.IsNullOrEmpty(token) && _activeTokens.ContainsKey(token))
            _activeTokens[token] = DateTime.Now;
    }

    /// <summary>
    /// 用户登出/Token失效时调用
    /// </summary>
    public static void OnLogout(string token)
    {
        if (!string.IsNullOrEmpty(token))
            _activeTokens.TryRemove(token, out _);
    }

    /// <summary>
    /// 清理超时未活动的连接（超过30分钟无心跳视为离线）
    /// 建议由定时器定期调用
    /// </summary>
    public static int CleanupExpired()
    {
        var cutoff = DateTime.Now.AddMinutes(-30);
        var expired = _activeTokens.Where(kvp => kvp.Value < cutoff).Select(kvp => kvp.Key).ToList();
        foreach (var key in expired)
            _activeTokens.TryRemove(key, out _);
        return expired.Count;
    }
}
