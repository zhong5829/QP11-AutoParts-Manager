using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using Serilog;

namespace QP11.Services;

/// <summary>品秀传动(dataenlighten)平台 VIN配件数据源</summary>
public class PinxiuDataSource : IVinDataSource
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private string? _accessToken;
    private string? _savedPhone;
    private string[]? _cachedMjsids;

    private static readonly string TokenFilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "Data", "vin_token_pinxiu.json");

    public string SourceName => "品秀";

    public bool IsLoggedIn => !string.IsNullOrEmpty(_accessToken) && !IsTokenExpired();

    public event EventHandler? LoginStatusChanged;

    /// <summary>检查accessToken是否已过期（JWT exp字段）</summary>
    private bool IsTokenExpired()
    {
        try
        {
            if (string.IsNullOrEmpty(_accessToken)) return true;
            // JWT格式: header.payload.signature
            var parts = _accessToken.Split('.');
            if (parts.Length < 2) return false; // 无法解析，不过期
            var payload = parts[1];
            // Base64url补齐填充
            var padded = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("exp", out var expEl))
            {
                var exp = expEl.GetInt64();
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                return now >= exp;
            }
            // 无exp字段，检查loginuser中的有效期
            if (doc.RootElement.TryGetProperty("loginuser", out var lu))
            {
                var loginStr = lu.GetString() ?? "";
                // 格式: SMS:phone:timestamp:duration
                var segments = loginStr.Split(':');
                if (segments.Length >= 4 && long.TryParse(segments[2], out var ts) && long.TryParse(segments[3], out var dur))
                {
                    var expiry = ts / 1000 + dur; // loginuser的timestamp是毫秒
                    var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    return now >= expiry;
                }
            }
            return false; // 无法判断，视为未过期
        }
        catch
        {
            return false; // 解析失败，视为未过期（保守策略）
        }
    }

    /// <summary>获取Token预计到期时间</summary>
    public DateTime? GetTokenExpiryTime()
    {
        try
        {
            if (string.IsNullOrEmpty(_accessToken)) return null;
            var parts = _accessToken.Split('.');
            if (parts.Length < 2) return null;
            var payload = parts[1];
            var padded = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("exp", out var expEl))
            {
                var exp = expEl.GetInt64();
                return DateTimeOffset.FromUnixTimeSeconds(exp).LocalDateTime;
            }
            if (doc.RootElement.TryGetProperty("loginuser", out var lu))
            {
                var loginStr = lu.GetString() ?? "";
                var segments = loginStr.Split(':');
                if (segments.Length >= 4 && long.TryParse(segments[2], out var ts) && long.TryParse(segments[3], out var dur))
                {
                    var expiry = ts / 1000 + dur;
                    return DateTimeOffset.FromUnixTimeSeconds(expiry).LocalDateTime;
                }
            }
            return null;
        }
        catch { return null; }
    }

    private string ApiBase => _config["Pinxiu:ApiBaseUrl"] ?? "https://api.dataenlighten.com:8045";
    private string CompanyId => _config["Pinxiu:CompanyId"] ?? "MTEzMQ==";
    private string ProductCode => _config["Pinxiu:ProductCode"] ?? "MKZ25";

    public PinxiuDataSource(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;

        LoadTokenFromFile();

        var timeout = int.TryParse(config["Pinxiu:RequestTimeoutSeconds"], out var t) ? t : 15;
        _httpClient.Timeout = TimeSpan.FromSeconds(timeout);
    }

    #region Token 持久化

    private void LoadTokenFromFile()
    {
        try
        {
            if (!File.Exists(TokenFilePath)) return;
            var json = File.ReadAllText(TokenFilePath);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            _accessToken = root.TryGetProperty("accessToken", out var at) ? at.GetString() : null;
            _savedPhone = root.TryGetProperty("phone", out var p) ? p.GetString() : null;
        }
        catch { /* 加载失败不影响启动 */ }
    }

    private void SaveTokenToFile()
    {
        try
        {
            var dir = Path.GetDirectoryName(TokenFilePath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(new { accessToken = _accessToken, phone = _savedPhone });
            File.WriteAllText(TokenFilePath, json);
        }
        catch { /* 保存失败不影响主流程 */ }
    }

    #endregion

    #region 通用请求方法

    /// <summary>构建带公共Headers的请求</summary>
    private HttpRequestMessage CreateRequest(HttpMethod method, string url)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("company-id", CompanyId);
        request.Headers.Add("product-code", ProductCode);
        request.Headers.Add("origin", "https://applets-new.dataenlighten.com");
        request.Headers.Add("accept", "application/json, text/plain, */*");
        if (!string.IsNullOrEmpty(_accessToken))
            request.Headers.Add("authorization", $"Bearer {_accessToken}");
        return request;
    }

    /// <summary>发送带认证的请求，401时验证Token过期状态再决定是否清空</summary>
    private async Task<HttpResponseMessage> SendWithAuthAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var response = await _httpClient.SendAsync(request, ct);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            // 确认Token确实过期（避免临时服务端问题误清Token）
            if (IsTokenExpired())
            {
                Log.Warning("品秀API返回401且Token已过期，清空登录状态");
                _accessToken = null;
                SaveTokenToFile();
                LoginStatusChanged?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                Log.Warning("品秀API返回401但Token未过期（可能临时问题），保留登录状态");
            }
        }

        return response;
    }

    /// <summary>Base64解码响应体并解析JSON</summary>
    private JsonElement? DecodeBase64Response(string responseBody)
    {
        try
        {
            var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(responseBody));
            var doc = JsonDocument.Parse(decoded);
            return doc.RootElement;
        }
        catch (FormatException)
        {
            // 响应可能不是Base64（如错误场景），尝试直接解析JSON
            try
            {
                var doc = JsonDocument.Parse(responseBody);
                return doc.RootElement;
            }
            catch
            {
                Log.Warning("品秀API响应既非Base64也非合法JSON: {Response}", Truncate(responseBody, 200));
                return null;
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "品秀API响应解码失败: {Response}", Truncate(responseBody, 200));
            return null;
        }
    }

    /// <summary>检查品秀API响应code是否为成功 "0000" 或 0</summary>
    private static bool IsSuccess(JsonElement root)
    {
        if (!root.TryGetProperty("code", out var c)) return false;
        return c.ValueKind == JsonValueKind.String && c.GetString() == "0000"
            || c.ValueKind == JsonValueKind.Number && c.GetInt32() == 0;
    }

    private static string Truncate(string s, int maxLen) =>
        s.Length <= maxLen ? s : s[..maxLen] + "...";

    #endregion

    #region IVinDataSource 实现

    public async Task<bool> SendSmsAsync(string phone, CancellationToken ct = default)
    {
        try
        {
            var url = $"{ApiBase}/pdmPro/oauth/oauthSendSmsCodeValidate";
            var request = CreateRequest(HttpMethod.Post, url);
            var body = JsonSerializer.Serialize(new { phoneNum = phone });
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            Log.Information("品秀发送短信响应: {Resp}", Truncate(responseBody, 300));

            var root = DecodeBase64Response(responseBody);
            if (root == null) return false;

            if (!IsSuccess(root.Value))
            {
                var msg = root.Value.TryGetProperty("codeDescription", out var m) ? m.GetString() : "未知错误";
                Log.Warning("品秀发送短信失败: {Msg}", msg);
                return false;
            }

            // 记住手机号，后续登录和刷新Token需要
            _savedPhone = phone;
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "品秀发送短信异常");
            return false;
        }
    }

    public async Task<bool> LoginAsync(string phone, string smsCode, CancellationToken ct = default)
    {
        try
        {
            var url = $"{ApiBase}/pdmPro/oauth/loginOrRegSpUser";
            var request = CreateRequest(HttpMethod.Post, url);
            var body = JsonSerializer.Serialize(new { phone, code = smsCode });
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            var response = await _httpClient.SendAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            var root = DecodeBase64Response(responseBody);
            if (root == null) return false;

            if (!IsSuccess(root.Value))
            {
                var msg = root.Value.TryGetProperty("codeDescription", out var m) ? m.GetString() : "未知错误";
                Log.Warning("品秀登录失败: {Msg}", msg);
                return false;
            }

            // 提取 accessToken
            if (root.Value.TryGetProperty("data", out var data))
            {
                _accessToken = data.TryGetProperty("accessToken", out var at) ? at.GetString() : null;
            }

            _savedPhone = phone;

            if (IsLoggedIn)
            {
                SaveTokenToFile();
                return true;
            }

            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "品秀登录异常");
            return false;
        }
    }

    public async Task<VinDecodeResult?> DecodeVinAsync(string vin, CancellationToken ct = default)
    {
        try
        {
            var url = $"{ApiBase}/pdmPro/aisearch/getAlphaRecommendVehicleList";
            var request = CreateRequest(HttpMethod.Post, url);

            var body = JsonSerializer.Serialize(new
            {
                sourcetype = 1,
                fieldType = "",
                fieldName = "",
                keyword = vin,
                vehicleInfoReq = new { },
                flag = 0,
                vehicleInfo = new { },
                addField = new[] { "brand", "sub_brand", "vehicle_group", "displacement", "engine" },
                pageIndex = 1,
                pageSize = 10
            });
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            var response = await SendWithAuthAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            var root = DecodeBase64Response(responseBody);
            if (root == null) return null;

            if (!IsSuccess(root.Value)) return null;

            if (!root.Value.TryGetProperty("data", out var data)) return null;

            // 缓存mjsid用于后续GetPartCards查询
            var mjsidRaw = data.TryGetProperty("list", out var list) && list.GetArrayLength() > 0
                ? list[0].TryGetProperty("mjsid", out var mid) ? mid.GetString() : null
                : null;

            if (!string.IsNullOrEmpty(mjsidRaw))
            {
                _cachedMjsids = mjsidRaw.Split(',', StringSplitOptions.RemoveEmptyEntries);
            }

            // 从 list[0] 解析车辆信息
            if (list.ValueKind != JsonValueKind.Array || list.GetArrayLength() == 0)
                return null;

            var vehicle = list[0];
            var result = new VinDecodeResult
            {
                Vin = vin,
                Brand = vehicle.TryGetProperty("brand", out var brand) ? brand.GetString() ?? "" : "",
                Series = vehicle.TryGetProperty("vehicle_chn", out var series) ? series.GetString() ?? "" : "",
                Models = vehicle.TryGetProperty("vehicle_group", out var models) ? models.GetString() ?? "" : "",
                DisplacementWithT = vehicle.TryGetProperty("displacement", out var disp) ? disp.GetString() ?? "" : "",
                EngineModel = vehicle.TryGetProperty("engine", out var eng) ? eng.GetString() ?? "" : ""
            };

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "品秀VIN解码异常: {Vin}", vin);
            return null;
        }
    }

    public async Task<VinPartPageResult?> GetPartCardsAsync(string vin, VinDecodeResult vehicleInfo, int page = 1, CancellationToken ct = default)
    {
        try
        {
            // 品秀API无分页，第1页已返回全部数据，后续页返回空结果避免重复
            if (page > 1)
                return new VinPartPageResult { Current = page, Pages = 1, Total = 0 };
            if (_cachedMjsids == null || _cachedMjsids.Length == 0)
            {
                Log.Warning("品秀GetPartCards: 无缓存mjsid，需先调用DecodeVinAsync");
                return null;
            }

            var url = $"{ApiBase}/pdmPro/sp/getProdListByVIN";
            var request = CreateRequest(HttpMethod.Post, url);

            var body = JsonSerializer.Serialize(new
            {
                mjsids = _cachedMjsids,
                vin = vin
            });
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");

            var response = await SendWithAuthAsync(request, ct);
            var responseBody = await response.Content.ReadAsStringAsync(ct);

            var root = DecodeBase64Response(responseBody);
            if (root == null) return null;

            if (!IsSuccess(root.Value)) return null;

            if (!root.Value.TryGetProperty("data", out var data)) return null;

            var result = new VinPartPageResult();

            // 解析 cspuList
            if (!data.TryGetProperty("cspuList", out var cspuList) || cspuList.ValueKind != JsonValueKind.Array)
                return result;

            // 品秀API因多个mjsid返回重复配件，需要全局按编码去重 + 按分类名合并
            var seenModels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var categoryDict = new Dictionary<string, VinPartCategoryGroup>(StringComparer.OrdinalIgnoreCase);

            foreach (var categoryItem in cspuList.EnumerateArray())
            {
                var categoryName = categoryItem.TryGetProperty("categoryName", out var cn) ? cn.GetString() ?? "" : "";
                var categoryId = categoryItem.TryGetProperty("categoryId", out var cid)
                    ? long.TryParse(cid.GetString(), out var cidVal) ? cidVal : 0 : 0;

                if (!categoryDict.TryGetValue(categoryName, out var categoryGroup))
                {
                    categoryGroup = new VinPartCategoryGroup
                    {
                        TenantCategoryId = categoryId,
                        CategoryName = categoryName
                    };
                    categoryDict[categoryName] = categoryGroup;
                }

                if (categoryItem.TryGetProperty("prodList", out var prodList) && prodList.ValueKind == JsonValueKind.Array)
                {
                    foreach (var prod in prodList.EnumerateArray())
                    {
                        var card = MapToVinPartCard(prod);
                        // 全局按编码去重
                        if (!string.IsNullOrWhiteSpace(card.Model) && !seenModels.Add(card.Model))
                            continue;
                        categoryGroup.Products.Add(card);
                    }
                }
            }

            result.Categories = categoryDict.Values.ToList();

            result.Total = result.Categories.Sum(c => c.Products.Count);
            result.Current = page;
            result.Pages = 1;

            return result;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "品秀获取配件列表异常: {Vin}", vin);
            return null;
        }
    }

    public async Task<bool> RefreshTokenAsync(CancellationToken ct = default)
    {
        // 品秀Token有效期15天，无refreshToken机制
        // Token仍有效时无需刷新，直接返回true
        if (!string.IsNullOrEmpty(_accessToken) && !IsTokenExpired())
        {
            Log.Information("品秀Token仍有效，无需刷新");
            return true;
        }

        // Token已过期，清空登录状态触发重新登录
        _accessToken = null;
        SaveTokenToFile();
        LoginStatusChanged?.Invoke(this, EventArgs.Empty);
        Log.Information("品秀Token已过期，需重新登录");
        return false;
    }

    public Task StartupRefreshAsync()
    {
        // 品秀Token有效期15天，如果过期则清空Token触发重新登录
        if (!string.IsNullOrEmpty(_accessToken))
        {
            var expiry = GetTokenExpiryTime();
            if (expiry.HasValue && expiry.Value <= DateTime.Now)
            {
                _accessToken = null;
                SaveTokenToFile();
                LoginStatusChanged?.Invoke(this, EventArgs.Empty);
                Log.Information("品秀启动时发现Token已过期，已清除");
            }
        }
        return Task.CompletedTask;
    }

    #endregion

    #region 字段映射

    private VinPartCard MapToVinPartCard(JsonElement prod)
    {
        var imgUrlList = new List<string>();
        if (prod.TryGetProperty("pImage", out var img) && img.ValueKind == JsonValueKind.String)
        {
            var imgUrl = img.GetString();
            if (!string.IsNullOrWhiteSpace(imgUrl))
                imgUrlList.Add(imgUrl);
        }

        var marketPriceStr = prod.TryGetProperty("marketPrice", out var mp) ? mp.GetString() : "";
        var mj4sPriceStr = prod.TryGetProperty("mj4sPrice", out var m4p) ? m4p.GetString() : "";

        return new VinPartCard
        {
            Id = prod.TryGetProperty("cspuId", out var cspuId)
                ? long.TryParse(cspuId.GetString(), out var idVal) ? idVal : 0 : 0,
            Model = prod.TryGetProperty("cspuModel", out var model) ? model.GetString() : "",
            TenantBrandName = prod.TryGetProperty("brandName", out var bn) ? bn.GetString() : "",
            TenantCategoryName = prod.TryGetProperty("categoryName", out var cn) ? cn.GetString() : "",
            InstallationLocation = prod.TryGetProperty("placesName", out var pn) ? pn.GetString() : "",
            PartNumber = prod.TryGetProperty("partNumber", out var partNo) ? partNo.GetString() : "",
            VehicleComment = prod.TryGetProperty("vehicleComment", out var vc) ? vc.GetString() : "",
            ImgUrlList = imgUrlList,
            Price = decimal.TryParse(marketPriceStr, out var price) ? price : 0,
            GuidePrice = decimal.TryParse(mj4sPriceStr, out var gp) ? gp : 0,
            Stock = prod.TryGetProperty("stockQuantity", out var sq) ? sq.GetInt32() : 0,
            SourceName = "品秀"
        };
    }

    #endregion
}
