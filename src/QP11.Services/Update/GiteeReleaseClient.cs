using System.Text.Json;

namespace QP11.Services.Update;

/// <summary>Gitee Releases API 客户端</summary>
public class GiteeReleaseClient : IDisposable
{
    private readonly HttpClient _http;
    private bool _disposed;

    /// <summary>Gitee 用户名/组织名</summary>
    public string Owner { get; set; } = string.Empty;

    /// <summary>Gitee 仓库名</summary>
    public string Repo { get; set; } = string.Empty;

    /// <summary>Gitee 私有令牌（私有仓库或提高速率限制时使用）</summary>
    public string AccessToken { get; set; } = string.Empty;

    private const string ApiBase = "https://gitee.com/api/v5";

    public GiteeReleaseClient()
    {
        _http = new HttpClient();
        _http.Timeout = TimeSpan.FromSeconds(30);
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
        if (!string.IsNullOrEmpty(AccessToken))
            url += $"?access_token={AccessToken}";

        string resp;
        try
        {
            resp = await _http.GetStringAsync(url);
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException($"无法连接 Gitee 更新服务器: {ex.Message}", ex);
        }

        using var doc = JsonDocument.Parse(resp);
        var root = doc.RootElement;

        // 检查 API 是否返回了错误
        if (root.TryGetProperty("message", out var errorMsg))
        {
            var msg = errorMsg.GetString() ?? "未知错误";
            throw new InvalidOperationException($"Gitee API 错误: {msg}");
        }

        // 解析 tag_name，如 "v2.1.0" 或 "2.1.0"
        if (!root.TryGetProperty("tag_name", out var tagElem))
            throw new InvalidOperationException("Gitee API 响应格式无效：缺少 tag_name");

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
                    // 私有仓库需在下载 URL 中追加认证令牌
                    if (!string.IsNullOrEmpty(downloadUrl) && !string.IsNullOrEmpty(AccessToken) && !downloadUrl.Contains("access_token"))
                        downloadUrl += $"?access_token={AccessToken}";
                }
                else if (name.EndsWith(".md5", StringComparison.OrdinalIgnoreCase)
                      || name.Equals("md5.txt", StringComparison.OrdinalIgnoreCase))
                {
                    if (!asset.TryGetProperty("browser_download_url", out var md5UrlElem)) continue;
                    var md5Url = md5UrlElem.GetString();
                    if (string.IsNullOrEmpty(md5Url)) continue;

                    if (!string.IsNullOrEmpty(AccessToken) && !md5Url.Contains("access_token"))
                        md5Url += $"?access_token={AccessToken}";
                    md5 = (await _http.GetStringAsync(md5Url)).Trim();
                }
            }
        }

        // 如果 assets 中没有 EXE，尝试使用浏览器下载 URL（拼接标准命名）
        if (string.IsNullOrEmpty(downloadUrl))
        {
            downloadUrl = $"https://gitee.com/{Owner}/{Repo}/releases/download/{tag}/{Repo}-v{latestVersion}.exe";
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
