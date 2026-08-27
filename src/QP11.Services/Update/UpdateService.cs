using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;

namespace QP11.Services.Update;

/// <summary>应用更新服务</summary>
public class UpdateService : IDisposable
{
    private readonly GiteeReleaseClient _client;
    private readonly string _appDir;
    private bool _disposed;

    /// <summary>应用退出回调（由 WPF 层设置，用于关闭应用）</summary>
    public Action? ShutdownApp { get; set; }

    /// <summary>Gitee 访问令牌（私有仓库下载时用于认证）</summary>
    public string AccessToken { get; set; } = string.Empty;

    public UpdateService(GiteeReleaseClient client)
    {
        _client = client;
        // 去掉末尾的目录分隔符，避免 bat 脚本中引号提前闭合
        _appDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }

    /// <summary>获取当前应用版本号</summary>
    public static Version GetCurrentVersion()
    {
        var asm = Assembly.GetEntryAssembly();
        return asm?.GetName().Version ?? new Version(1, 0, 0);
    }

    /// <summary>检查更新</summary>
    public async Task<UpdateInfo?> CheckUpdateAsync()
    {
        var currentVersion = GetCurrentVersion();
        return await _client.GetLatestUpdateAsync(currentVersion);
    }

    /// <summary>
    /// 下载安装包并启动更新。
    /// 下载完成后生成更新脚本：等待主进程退出 → 启动 EXE 安装程序 → 退出当前应用。
    /// </summary>
    public async Task DownloadAndInstallAsync(UpdateInfo info, IProgress<(long downloaded, long total)>? progress = null)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "QP11_Update");
        // 强制清理旧临时目录（可能被上次未完成的更新残留）
        try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); } catch { }
        Directory.CreateDirectory(tempDir);

        var exePath = Path.Combine(tempDir, $"setup_v{info.Version}.exe");

        // 下载（私有仓库需要认证）
        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
        if (!string.IsNullOrEmpty(AccessToken))
            http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("token", AccessToken);

        // 记录下载 URL 用于调试
        var downloadUrl = info.DownloadUrl;
        System.Diagnostics.Debug.WriteLine($"[Update] Download URL: {downloadUrl}");
        System.Diagnostics.Debug.WriteLine($"[Update] Expected file size: {info.FileSize}");

        using var response = await http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead);
        var contentType = response.Content.Headers.ContentType?.ToString() ?? "unknown";
        System.Diagnostics.Debug.WriteLine($"[Update] Response status: {response.StatusCode}, Content-Type: {contentType}");
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? info.FileSize;

        // 流式下载：边下载边上报进度，避免进度条全程停在 0%
        var downloaded = 0L;
        using (var contentStream = await response.Content.ReadAsStreamAsync())
        using (var fileStream = new FileStream(exePath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            var buffer = new byte[256 * 1024];
            int read;
            while ((read = await contentStream.ReadAsync(buffer.AsMemory(0, buffer.Length))) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, read));
                downloaded += read;
                progress?.Report((downloaded, total));
            }
        }
        progress?.Report((downloaded, total));

        System.Diagnostics.Debug.WriteLine($"[Update] Downloaded bytes: {downloaded}, expected: {total}");

        // 校验文件完整性
        var fileInfo = new FileInfo(exePath);
        if (fileInfo.Length == 0)
            throw new InvalidOperationException("下载的文件为空，请检查网络连接后重试。");
        if (total > 0 && downloaded != total)
            throw new InvalidOperationException($"下载文件大小不匹配：期望 {total} 字节，实际 {downloaded} 字节");

        // 检查是否为有效的 PE/EXE 文件（MZ 魔数）
        var header = new byte[(int)Math.Min(200L, fileInfo.Length)];
        using (var headerStream = File.OpenRead(exePath))
        {
            _ = headerStream.Read(header, 0, header.Length);
        }
        if (header.Length < 2 || header[0] != 0x4D || header[1] != 0x5A)
        {
            File.Delete(exePath);
            var preview = System.Text.Encoding.UTF8.GetString(header);
            System.Diagnostics.Debug.WriteLine($"[Update] Not an EXE! First bytes: {BitConverter.ToString(header.Take(16).ToArray())}");
            System.Diagnostics.Debug.WriteLine($"[Update] Content preview: {preview}");
            throw new InvalidOperationException(
                $"下载内容不是有效的 EXE 安装文件！\n" +
                $"URL: {downloadUrl}\n" +
                $"Content-Type: {contentType}\n" +
                $"下载大小: {downloaded} 字节\n\n" +
                $"可能原因：Gitee Release 中未上传正确的 EXE 安装包附件。");
        }

        // MD5 校验
        if (!string.IsNullOrEmpty(info.Md5))
        {
            using var md5 = MD5.Create();
            using var checkStream = File.OpenRead(exePath);
            var hash = BitConverter.ToString(md5.ComputeHash(checkStream))
                .Replace("-", "").ToLowerInvariant();
            if (hash != info.Md5.ToLowerInvariant())
            {
                File.Delete(exePath);
                throw new InvalidOperationException("文件校验失败，安装包可能已被篡改，请稍后重试。");
            }
        }

        // 生成更新脚本：等待主进程退出 → 启动安装程序 → 清理临时文件
        var scriptPath = Path.Combine(tempDir, "update.bat");
        var pid = Process.GetCurrentProcess().Id;

        var lines = new List<string>
        {
            "@echo off",
            "",
            "rem 等待主进程退出",
            ":wait_loop",
            $"tasklist /fi \"PID eq {pid}\" 2>nul | find /i \"{pid}\" >nul",
            "if not errorlevel 1 (",
            "    timeout /t 1 /nobreak >nul",
            "    goto wait_loop",
            ")",
            "timeout /t 1 /nobreak >nul",
            "",
            "rem 启动安装程序",
            $"start \"\" \"{exePath}\"",
            "",
            "rem 安装完成后清理临时文件（延迟等待安装完成）",
            "timeout /t 10 /nobreak >nul",
            $"rd /s /q \"{tempDir}\" 2>nul",
            "",
            "exit"
        };
        var script = string.Join("\r\n", lines);
        File.WriteAllText(scriptPath, script, System.Text.Encoding.Default);

        // 启动更新脚本（必须用 ShellExecute 才能执行 start 等命令）
        Process.Start(new ProcessStartInfo
        {
            FileName = scriptPath,
            CreateNoWindow = true,
            UseShellExecute = true,
            WorkingDirectory = tempDir,
            WindowStyle = ProcessWindowStyle.Hidden
        });

        // 通过回调退出当前应用
        ShutdownApp?.Invoke();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _client.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
