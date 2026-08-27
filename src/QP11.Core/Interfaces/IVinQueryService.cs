using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QP11.Core.Entities;

namespace QP11.Core.Interfaces;

/// <summary>VIN查询服务接口 — 多数据源VIN解码与配件查询</summary>
public interface IVinQueryService
{
    /// <summary>发送短信验证码</summary>
    Task<bool> SendSmsAsync(string phone, CancellationToken ct = default);

    /// <summary>短信验证码登录，返回是否成功</summary>
    Task<bool> LoginAsync(string phone, string smsCode, CancellationToken ct = default);

    /// <summary>VIN解码</summary>
    Task<VinDecodeResult?> DecodeVinAsync(string vin, CancellationToken ct = default);

    /// <summary>获取适配配件列表</summary>
    Task<VinPartPageResult?> GetPartCardsAsync(string vin, VinDecodeResult vehicleInfo, int page = 1, CancellationToken ct = default);

    /// <summary>刷新Token</summary>
    Task<bool> RefreshTokenAsync(CancellationToken ct = default);

    /// <summary>是否已登录（任一数据源Token有效）</summary>
    bool IsLoggedIn { get; }

    /// <summary>获取已登录的数据源列表</summary>
    List<IVinDataSource> GetLoggedInSources();

    /// <summary>指定数据源发送短信验证码</summary>
    Task<bool> SendSourceSmsAsync(string sourceName, string phone, CancellationToken ct = default);

    /// <summary>指定数据源登录</summary>
    Task<bool> LoginSourceAsync(string sourceName, string phone, string smsCode, CancellationToken ct = default);

    /// <summary>数据源登录状态变更事件</summary>
    event EventHandler? SourceStatusChanged;

    /// <summary>获取所有数据源（含未登录）</summary>
    List<IVinDataSource> GetAllSources();

    /// <summary>启动时主动续期所有已登录数据源</summary>
    Task StartupRefreshAsync();
}
