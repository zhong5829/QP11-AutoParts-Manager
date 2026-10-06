using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using QP11.Core.Entities;

namespace QP11.Core.Interfaces;

/// <summary>VIN配件数据源接口 — 每个平台(318car、品秀等)实现此接口</summary>
public interface IVinDataSource
{
    /// <summary>数据源名称（如"318car"、"品秀"）</summary>
    string SourceName { get; }

    /// <summary>是否已登录（Token有效）</summary>
    bool IsLoggedIn { get; }

    /// <summary>登录状态变更事件（Token过期/401导致登出时触发）</summary>
    event EventHandler? LoginStatusChanged;

    /// <summary>获取Token预计到期时间（未登录或无法解析时返回null）</summary>
    DateTime? GetTokenExpiryTime();

    /// <summary>发送短信验证码</summary>
    Task<bool> SendSmsAsync(string phone, CancellationToken ct = default);

    /// <summary>短信验证码登录，返回是否成功</summary>
    Task<bool> LoginAsync(string phone, string smsCode, CancellationToken ct = default);

    /// <summary>VIN解码</summary>
    Task<VinDecodeResult?> DecodeVinAsync(string vin, CancellationToken ct = default);

    /// <summary>获取适配配件列表</summary>
    Task<VinPartPageResult?> GetPartCardsAsync(string vin, VinDecodeResult vehicleInfo, int page = 1, CancellationToken ct = default);

    /// <summary>按配件编号/型号/名称关键词搜索配件（不依赖VIN，全库搜索）</summary>
    Task<VinPartPageResult?> SearchByKeywordAsync(string keyword, int page = 1, CancellationToken ct = default);

    /// <summary>查询指定配件的适配车型列表（品牌分组），不支持的数据源返回空列表</summary>
    Task<List<VinAdaptVehicleBrand>> GetAdaptVehiclesAsync(long productId, CancellationToken ct = default);

    /// <summary>刷新Token</summary>
    Task<bool> RefreshTokenAsync(CancellationToken ct = default);

    /// <summary>启动时主动续期（accessToken过期时尝试用refreshToken刷新）</summary>
    Task StartupRefreshAsync();
}
