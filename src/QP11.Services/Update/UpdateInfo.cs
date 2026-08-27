namespace QP11.Services.Update;

/// <summary>更新信息</summary>
public class UpdateInfo
{
    /// <summary>新版本号</summary>
    public Version Version { get; set; } = new();

    /// <summary>下载地址</summary>
    public string DownloadUrl { get; set; } = string.Empty;

    /// <summary>更新日志</summary>
    public string Changelog { get; set; } = string.Empty;

    /// <summary>文件 MD5 校验值</summary>
    public string Md5 { get; set; } = string.Empty;

    /// <summary>是否强制更新</summary>
    public bool Mandatory { get; set; }

    /// <summary>文件大小（字节）</summary>
    public long FileSize { get; set; }
}
