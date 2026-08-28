using System.Text.Json;

namespace QP11.Services.Update;

/// <summary>GitHub Releases API 客户端</summary>
public class GitHubReleaseClient : IDisposable
{
    private readonly HttpClient _http;
    private bool _disposed;

    /// <summary>GitHub 用户名/组织名</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>GitHub 仓库名</summary>
    public string Repo { get; set; } = string.Empty;

    /// <summary>GitHub 访问令牌（私有仓库或提高速率限制时使用，留空则匿名访问公开仓库）</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// GitHub 下载加速代理前缀（如 https://ghfast.top/ ），拼接在资产下载 URL 之前。
    /// 留空则直连 GitHub 下载。
    /// </summary>
    public string DownloadProxy { get; set; } = string.Empty;

    private const string ApiBase = "https://api.github.com";

    public GitHubReleaseClient()
    {
        _http = new HttpClient();
        _http.Timeout = TimeSpan.FromSeconds(30);
        // GitHub API 强制要求 User-Agent 请求头，否则返回 403
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("QP11-Updater/2.0");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _http.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 获取最新 Release 信息，解析为 UpdateInfo。
    /// 如果当前版本已是最新，返回 null。
    /// </summary>
    public async Task<UpdateInfo?> GetLatestUpdateAsync(Version currentVersion)
    {
        var url = $"{ApiBase}/repos/{Owner}/{Repo}/releases/latest";

        // 认证走请求头发送（GitHub 不支持 URL 拼接令牌），有令牌则设置，无令牌则匿名
        _http.DefaultRequestHeaders.Authorization = string.IsNullOrEmpty(AccessToken)
            ? null
            : new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", AccessToken);

        string resp;
        try
        {
            resp = await _http.GetStringAsync(url);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"无法连接 GitHub 更新服务器: {ex.Message}", ex);
        }

        using var doc = JsonDocument.Parse(resp);
        var root = doc.RootElement;

        // GitHub API 错误时（如仓库不存在/无权访问）返回 { "message": "..." }
        if (root.TryGetProperty("message", out var errorMsg))
        {
            var msg = errorMsg.GetString() ?? "未知错误";
            throw new InvalidOperationException($"GitHub API 错误: {msg}");
        }

        // 解析 tag_name，如 "v2.1.0" 或 "2.1.0"
        if (!root.TryGetProperty("tag_name", out var tagElem))
            throw new InvalidOperationException("GitHub API 响应格式无效：缺少 tag_name");

        var tag = tagElem.GetString()!;
        var versionStr = tag.TrimStart('v', 'V');
        Version latestVersion;
        try
        {
            latestVersion = Version.Parse(versionStr);
        }
        catch
        {
            throw new InvalidOperationException($"无法解析版本号: {versionStr}");
        }

        if (latestVersion <= currentVersion)
            return null;

        // 从 assets 中查找 EXE 安装包和 MD5 文件
        string? downloadUrl = null;
        string? md5 = null;
        long fileSize = 0;

        if (root.TryGetProperty("assets", out var assets))
        {
            foreach (var asset in assets.EnumerateArray())
            {
                // 安全获取 name 字段
                if (!asset.TryGetProperty("name", out var nameElem)) continue;
                var name = nameElem.GetString();
                if (string.IsNullOrEmpty(name)) continue;

                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    if (asset.TryGetProperty("browser_download_url", out var urlElem))
                        downloadUrl = urlElem.GetString();
                    if (asset.TryGetProperty("size", out var sizeElem))
                        fileSize = sizeElem.GetInt64();
                    // GitHub 资产下载 URL 已带签名且认证走请求头（下载时 UpdateService 会附带 Authorization），无需拼接令牌
                }
                else if (name.EndsWith(".md5", StringComparison.OrdinalIgnoreCase)
                      || name.Equals("md5.txt", StringComparison.OrdinalIgnoreCase))
                {
                    if (!asset.TryGetProperty("browser_download_url", out var md5UrlElem)) continue;
                    var md5Url = md5UrlElem.GetString();
                    if (string.IsNullOrEmpty(md5Url)) continue;

                    // MD5 文件同样走下载代理，避免直连失败导致校验不可用
                    if (!string.IsNullOrEmpty(DownloadProxy))
                        md5Url = $"{DownloadProxy.TrimEnd('/')}/{md5Url.TrimStart('/')}";
                    md5 = (await _http.GetStringAsync(md5Url)).Trim();
                }
            }
        }

        // 如果 assets 中没有 EXE，尝试使用浏览器下载 URL（拼接标准命名）
        if (string.IsNullOrEmpty(downloadUrl))
        {
            downloadUrl = $"https://github.com/{Owner}/{Repo}/releases/download/{tag}/{Repo}-v{latestVersion}.exe";
        }

        var changelog = root.TryGetProperty("body", out var body)
            ? body.GetString() ?? string.Empty
            : string.Empty;

        // 检查是否强制更新（tag 或 body 中包含 [mandatory] 标记）
        var mandatory = tag.Contains("mandatory", StringComparison.OrdinalIgnoreCase)
                        || changelog.Contains("[mandatory]", StringComparison.OrdinalIgnoreCase);

        return new UpdateInfo
        {
            Version = latestVersion,
            DownloadUrl = downloadUrl,
            Changelog = changelog,
            Md5 = md5 ?? string.Empty,
            Mandatory = mandatory,
            FileSize = fileSize
        };
    }
}