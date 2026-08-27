using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using Serilog;

namespace QP11.Services;

/// <summary>318car平台VIN查询服务 — 真实API实现</summary>
public class VinQueryService : IVinDataSource
{
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _config;
    private string? _accessToken;
    private string? _refreshToken;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private DateTime _lastRefreshTime = DateTime.MinValue;

    // Token持久化文件路径
    private static readonly string TokenFilePath = Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "Data", "vin_token_318car.json");

    public string SourceName => "318car";

    public bool IsLoggedIn => !string.IsNullOrEmpty(_accessToken);

    public event EventHandler? LoginStatusChanged;

    /// <summary>获取Token预计到期时间（解析JWT exp字段）</summary>
    public DateTime? GetTokenExpiryTime()
    {
        try
        {
            if (string.IsNullOrEmpty(_accessToken)) return null;
            var parts = _accessToken.Split('.');
            if (parts.Length < 2) return null;
            var payload = parts[1];
            var padded = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("exp", out var expEl))
            {
                var exp = expEl.GetInt64();
                return DateTimeOffset.FromUnixTimeSeconds(exp).LocalDateTime;
            }
            return null;
        }
        catch { return null; }
    }

    public VinQueryService(HttpClient httpClient, IConfiguration config)
    {
        _httpClient = httpClient;
        _config = config;

        // 优先从本地持久化文件加载Token，其次从配置
        LoadTokenFromFile();
        if (!IsLoggedIn)
        {
            _accessToken = config["VinQuery:AccessToken"];
            _refreshToken = config["VinQuery:RefreshToken"];
        }

        var timeout = int.TryParse(config["VinQuery:RequestTimeoutSeconds"], out var t) ? t : 10;
        _httpClient.Timeout = TimeSpan.FromSeconds(timeout);
    }

    /// <summary>启动时主动续期：accessToken过期但refreshToken仍有效时，刷新Token避免重新登录</summary>
    public async Task StartupRefreshAsync()
    {
        if (string.IsNullOrEmpty(_accessToken)) return;

        var expiry = GetTokenExpiryTime();
        // Token未过期或无法判断过期时间，无需刷新
        if (expiry.HasValue && expiry.Value > DateTime.Now) return;

        // accessToken已过期，尝试用refreshToken刷新
        Log.Information("318car启动时发现accessToken已过期，尝试续期");
        var refreshed = await RefreshTokenAsync();
        if (refreshed)
        {
            _lastRefreshTime = DateTime.Now;
            LoginStatusChanged?.Invoke(this, EventArgs.Empty);
            Log.Information("318car启动续期成功");
        }
        else
        {
            // refreshToken也过期了，清空Token
            _accessToken = null;
            _refreshToken = null;
            SaveTokenToFile();
            LoginStatusChanged?.Invoke(this, EventArgs.Empty);
            Log.Information("318car启动续期失败，refreshToken也已过期，需重新登录");
        }
    }

    /// <summary>从本地文件加载Token</summary>
    private void LoadTokenFromFile()
    {
        try
        {
            // 兼容旧版本：v2.1.8及之前Token保存在 vin_token.json，迁移到新文件名
            var oldPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "vin_token.json");
            if (!File.Exists(TokenFilePath) && File.Exists(oldPath))
            {
                try
                {
                    var dir = Path.GetDirectoryName(TokenFilePath)!;
                    Directory.CreateDirectory(dir);
                    File.Move(oldPath, TokenFilePath);
                    Log.Information("已迁移旧Token文件: {Old} → {New}", oldPath, TokenFilePath);
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "迁移旧Token文件失败，尝试直接读取");
                }
            }

            var path = File.Exists(TokenFilePath) ? TokenFilePath
                      : File.Exists(oldPath) ? oldPath : null;
            if (path == null) return;

            var json = File.ReadAllText(path);
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            _accessToken = root.TryGetProperty("accessToken", out var at) ? at.GetString() : null;
            _refreshToken = root.TryGetProperty("refreshToken", out var rt) ? rt.GetString() : null;
        }
        catch { /* 加载失败不影响启动 */ }
    }

    /// <summary>保存Token到本地文件</summary>
    private void SaveTokenToFile()
    {
        try
        {
            var dir = Path.GetDirectoryName(TokenFilePath)!;
            Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(new { accessToken = _accessToken, refreshToken = _refreshToken });
            File.WriteAllText(TokenFilePath, json);
        }
        catch { /* 保存失败不影响主流程 */ }
    }

    private string ApiBase => _config["VinQuery:ApiBaseUrl"] ?? "https://mp.318car.com";
    private string TenantId => _config["VinQuery:TenantId"] ?? "226";

    /// <summary>设置认证Headers</summary>
    private void SetAuthHeaders(HttpRequestMessage request)
    {
        request.Headers.Remove("Authorization");
        request.Headers.Remove("refreshToken");
        request.Headers.Remove("Tenant");
        if (!string.IsNullOrEmpty(_accessToken))
            request.Headers.Add("Authorization", $"Bearer {_accessToken}");
        if (!string.IsNullOrEmpty(_refreshToken))
            request.Headers.Add("refreshToken", $"Bearer {_refreshToken}");
        request.Headers.Add("Tenant", TenantId);
    }

    /// <summary>发送带认证的请求，Token快过期时主动续期，401时刷新重试</summary>
    private async Task<HttpResponseMessage> SendWithAuthAsync(HttpRequestMessage request, CancellationToken ct)
    {
        // 主动续期：Token距过期不足2小时时提前刷新，避免使用中过期
        await TryPreRefreshAsync(ct);

        SetAuthHeaders(request);
        var response = await _httpClient.SendAsync(request, ct);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            // 尝试刷新Token
            var refreshed = await RefreshTokenAsync(ct);
            if (refreshed)
            {
                // 重试原请求
                var retry = new HttpRequestMessage(request.Method, request.RequestUri);
                // 复制body（如果有）
                if (request.Content != null)
                    retry.Content = request.Content;
                SetAuthHeaders(retry);
                response = await _httpClient.SendAsync(retry, ct);
            }
            else
            {
                // Token刷新失败，标记为未登录
                Log.Warning("318car Token刷新失败，标记为未登录");
                _accessToken = null;
                _refreshToken = null;
                SaveTokenToFile();
                LoginStatusChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        return response;
    }

    /// <summary>解析318car响应</summary>
    private async Task<T?> ParseResponseAsync<T>(HttpResponseMessage response) where T : class
    {
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var code = root.TryGetProperty("code", out var c) ? c.GetInt32() : 0;
        if (code != 10200)
        {
            var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "未知错误";
            throw new InvalidOperationException($"318car API错误: code={code}, message={msg}");
        }

        if (root.TryGetProperty("data", out var data) && data.ValueKind != JsonValueKind.Null)
        {
            return JsonSerializer.Deserialize<T>(data.GetRawText(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            });
        }

        return null;
    }

    public async Task<bool> SendSmsAsync(string phone, CancellationToken ct = default)
    {
        var url = $"{ApiBase}/app/sms/sendSms?phone={Uri.EscapeDataString(phone)}";
        var response = await _httpClient.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("code", out var c) && c.GetInt32() == 10200;
    }

    public async Task<bool> LoginAsync(string phone, string smsCode, CancellationToken ct = default)
    {
        var url = $"{ApiBase}/app/smsLogin?username={Uri.EscapeDataString(phone)}&smsCode={Uri.EscapeDataString(smsCode)}";
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        request.Headers.Add("Tenant", TenantId);

        var response = await _httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var code = root.TryGetProperty("code", out var c) ? c.GetInt32() : 0;
        if (code != 10200) return false;

        // 提取Token
        if (root.TryGetProperty("data", out var data))
        {
            _accessToken = data.TryGetProperty("accessToken", out var at) ? at.GetString() : null;
            _refreshToken = data.TryGetProperty("refreshToken", out var rt) ? rt.GetString() : null;
        }

        // 登录成功后持久化Token
        if (IsLoggedIn) SaveTokenToFile();

        return IsLoggedIn;
    }

    public async Task<VinDecodeResult?> DecodeVinAsync(string vin, CancellationToken ct = default)
    {
        var url = $"{ApiBase}/app/product/getVehicleByVin?vin={Uri.EscapeDataString(vin)}&tenantId={TenantId}";
        var request = new HttpRequestMessage(HttpMethod.Post, url);
        var response = await SendWithAuthAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var code = root.TryGetProperty("code", out var c) ? c.GetInt32() : 0;
        if (code != 10200)
        {
            var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "未知错误";
            throw new InvalidOperationException($"318car VIN解码错误: code={code}, message={msg}");
        }

        if (!root.TryGetProperty("data", out var data) || data.ValueKind == JsonValueKind.Null)
            return null;

        // 318car getVehicleByVin 返回 data.list[] 数组，车辆信息在 list[0] 中
        // data 结构: { list: [车辆对象], errorCorrection: {...}, multipleVehicle: bool }
        JsonElement vehicleElement = default;
        bool found = false;

        if (data.TryGetProperty("list", out var list) && list.ValueKind == JsonValueKind.Array && list.GetArrayLength() > 0)
        {
            vehicleElement = list[0];
            found = true;
        }
        else
        {
            // 兼容：如果data本身就是车辆对象（Brand不为空）
            vehicleElement = data;
            found = true;
        }

        if (!found) return null;

        var result = JsonSerializer.Deserialize<VinDecodeResult>(vehicleElement.GetRawText(), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // 确保 VIN 字段填充
        if (result != null && string.IsNullOrEmpty(result.Vin))
            result.Vin = vin;

        return result;
    }

    public async Task<VinPartPageResult?> GetPartCardsAsync(string vin, VinDecodeResult vehicleInfo, int page = 1, CancellationToken ct = default)
    {
        var url = $"{ApiBase}/app/product/user/pageProduct";
        var body = new
        {
            vin = vin,
            vehicleIds = vehicleInfo.VehicleIds,
            brand = vehicleInfo.Brand,
            manufacturers = vehicleInfo.Manufacturers,
            series = vehicleInfo.Series,
            models = vehicleInfo.Models,
            chassisCode4 = vehicleInfo.ChassisCode4,
            displacementWithT = vehicleInfo.DisplacementWithT,
            engineModel = vehicleInfo.EngineModel,
            yearRange = vehicleInfo.YearRange,
            generation = vehicleInfo.Generation,
            vehicleAttributes = vehicleInfo.VehicleAttributes,
            driveModel = vehicleInfo.DriveModel,
            transmissionDescription = vehicleInfo.TransmissionDescription,
            queryType = 5,
            querySource = 1,
            tenantId = int.Parse(TenantId),
            isCard = 1,
            current = page,
            size = 15,
            str = ""
        };

        var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = JsonContent.Create(body)
        };
        var response = await SendWithAuthAsync(request, ct);
        response.EnsureSuccessStatusCode();

        // 手动解析，因为响应结构嵌套较深
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var code = root.TryGetProperty("code", out var c) ? c.GetInt32() : 0;
        if (code != 10200) return null;

        if (!root.TryGetProperty("data", out var data)) return null;

        var result = new VinPartPageResult
        {
            Total = data.TryGetProperty("total", out var total) ? total.GetInt32() : 0,
            Pages = data.TryGetProperty("pages", out var pages) ? pages.GetInt32() : 0,
            Current = data.TryGetProperty("current", out var cur) ? cur.GetInt32() : 1,
            AdaptQueryRecordId = data.TryGetProperty("adaptQueryRecordId", out var aqri) ? aqri.GetInt64() : 0
        };

        // 解析 empowerTenantProductList
        if (data.TryGetProperty("empowerTenantProductList", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var group in list.EnumerateArray())
            {
                var categoryGroup = new VinPartCategoryGroup
                {
                    TenantCategoryId = group.TryGetProperty("tenantCategoryId", out var tci) ? tci.GetInt64() : 0,
                    CategoryName = group.TryGetProperty("categoryName", out var cn) ? cn.GetString() : ""
                };

                if (group.TryGetProperty("productList", out var productList) && productList.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in productList.EnumerateArray())
                    {
                        var card = JsonSerializer.Deserialize<VinPartCard>(p.GetRawText(), new JsonSerializerOptions
                        {
                            PropertyNameCaseInsensitive = true
                        });
                        if (card != null)
                        {
                            card.SourceName = "318car";
                            // VehicleComment留给品秀的vehicleComment字段，318car不再覆盖（避免与Notes重复）
                            categoryGroup.Products.Add(card);
                        }
                    }
                }

                result.Categories.Add(categoryGroup);
            }
        }

        return result;
    }

    /// <summary>主动预刷新：Token距过期不足2小时时提前续期（加锁防并发重复刷新）</summary>
    private async Task TryPreRefreshAsync(CancellationToken ct)
    {
        var expiry = GetTokenExpiryTime();
        if (!expiry.HasValue || expiry.Value - DateTime.Now >= TimeSpan.FromHours(2)) return;

        // 5分钟内已刷新过则跳过
        if (DateTime.Now - _lastRefreshTime < TimeSpan.FromMinutes(5)) return;

        await _refreshLock.WaitAsync(ct);
        try
        {
            // double-check：拿到锁后再检查一次
            if (DateTime.Now - _lastRefreshTime < TimeSpan.FromMinutes(5)) return;

            Log.Information("318car Token即将过期（{Expiry}），主动续期", expiry.Value);
            var refreshed = await RefreshTokenAsync(ct);
            if (refreshed)
            {
                _lastRefreshTime = DateTime.Now;
                LoginStatusChanged?.Invoke(this, EventArgs.Empty);
            }
        }
        finally { _refreshLock.Release(); }
    }

    public async Task<bool> RefreshTokenAsync(CancellationToken ct = default)
    {
        try
        {
            // 官方抓包：Token续期用 /app/refreshToken（不是 /app/user/saastoken）
            // saastoken需要有效的accessToken，过期后调saastoken必失败
            // refreshToken接口即使accessToken过期，只要refreshToken有效就能续期
            var url = $"{ApiBase}/app/refreshToken";
            var request = new HttpRequestMessage(HttpMethod.Post, url);
            // 官方抓包请求头：Authorization（可过期）+ RefreshToken（大写R，必须有效）
            // 不设Tenant头（官方抓包refreshToken请求无Tenant）
            if (!string.IsNullOrEmpty(_accessToken))
                request.Headers.Add("Authorization", $"Bearer {_accessToken}");
            if (!string.IsNullOrEmpty(_refreshToken))
                request.Headers.Add("RefreshToken", $"Bearer {_refreshToken}");

            var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode) return false;

            var json = await response.Content.ReadAsStringAsync();
            var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var code = root.TryGetProperty("code", out var c) ? c.GetInt32() : 0;
            if (code != 10200) return false;

            if (root.TryGetProperty("data", out var data))
            {
                var newAccessToken = data.TryGetProperty("accessToken", out var at) ? at.GetString() : null;
                var newRefreshToken = data.TryGetProperty("refreshToken", out var rt) ? rt.GetString() : null;
                if (!string.IsNullOrEmpty(newAccessToken))
                {
                    _accessToken = newAccessToken;
                    _refreshToken = newRefreshToken ?? _refreshToken;
                    SaveTokenToFile();
                    return true;
                }
            }

            return false;
        }
        catch
        {
            _accessToken = null;
            _refreshToken = null;
            return false;
        }
    }

}
