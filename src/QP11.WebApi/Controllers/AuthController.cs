using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using QP11.Core.Interfaces;
using QP11.WebApi.Services;
using System.Security.Cryptography;
using System.Text;

namespace QP11.WebApi.Controllers;

/// <summary>
/// Web端登录认证控制器
/// 复用桌面端 AuthService 的验证逻辑：MD5(password) 比对 user_infor 表
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : Controller
{
    private readonly IAuthService _authService;
    private readonly IUserRepository _userRepo;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, IUserRepository userRepo, ILogger<AuthController> logger)
    {
        _authService = authService;
        _userRepo = userRepo;
        _logger = logger;
    }

    /// <summary>
    /// 登录接口
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                return BadRequest(new { error = "用户名和密码不能为空" });

            var user = await _authService.LoginAsync(request.Username!, request.Password!);

            if (user != null)
            {
                // 生成 Token（随机32位hex，存内存，8小时过期）
                var token = GenerateToken();
                var expireAt = DateTime.UtcNow.AddHours(8);
                TokenStore.Add(token, new TokenInfo
                {
                    UserId = request.Username, // 主键是 username
                    Username = user.Username ?? request.Username,
                    Name = user.Name ?? "",
                    ExpireAt = expireAt
                });

                // 记录在线连接
                ConnectionCounter.OnLogin(token);

                _logger.LogInformation("[Auth] 用户 {User} 登录成功", request.Username);
                return Ok(new
                {
                    success = true,
                    token,
                    username = user.Username,
                    name = user.Name,
                    expireAt
                });
            }
            else
            {
                return Unauthorized(new { error = "用户名或密码错误" });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Auth] 登录异常: {Msg}", ex.Message);
            return BadRequest(new { error = "登录失败，请稍后重试" });
        }
    }

    /// <summary>
    /// 验证 Token 是否有效（前端定时调用心跳检测）
    /// </summary>
    [HttpGet("verify")]
    public IActionResult Verify()
    {
        var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
        if (string.IsNullOrEmpty(token))
            return Unauthorized(new { error = "未登录" });

        var info = TokenStore.Get(token);
        if (info == null)
        {
            ConnectionCounter.OnLogout(token);
            return Unauthorized(new { error = "登录已过期，请重新登录" });
        }

        // 更新在线心跳
        ConnectionCounter.OnHeartbeat(token);

        return Ok(new { valid = true, username = info.Username });
    }

    /// <summary>
    /// 获取用户列表（登录页面下拉框用，无需认证）
    /// </summary>
    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        try
        {
            var users = await _userRepo.GetAllAsync();
            return Ok(new { data = users.Select(u => new { u.Username, u.Name }).ToList() });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Auth] 获取用户列表失败: {Msg}", ex.Message);
            return BadRequest(new { error = "获取用户列表失败，请稍后重试" });
        }
    }

    // ========== 工具方法 ==========

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(16); // 128位随机数
        return Convert.ToHexString(bytes).ToLowerInvariant(); // 32字符hex
    }
}

// ========== 请求模型 ==========

public class LoginRequest
{
    public string? Username { get; set; }
    public string? Password { get; set; }
}

// ========== 内存 Token 存储（进程内有效）==========

public static class TokenStore
{
    // token → 用户信息
    private static readonly Dictionary<string, TokenInfo> _tokens = new();
    // 定期清理过期token（每10分钟）
    private static readonly System.Threading.Timer _cleanupTimer = new(_ => CleanupExpired(), null, TimeSpan.FromMinutes(10), TimeSpan.FromMinutes(10));

    public static void Add(string token, TokenInfo info)
    {
        lock (_tokens)
        {
            _tokens[token] = info;
            CleanupExpiredInternal();
        }
    }

    public static TokenInfo? Get(string token)
    {
        lock (_tokens)
        {
            if (_tokens.TryGetValue(token, out var info))
            {
                if (info.ExpireAt < DateTime.UtcNow)
                {
                    _tokens.Remove(token);
                    ConnectionCounter.OnLogout(token);
                    return null;
                }
                return info;
            }
            return null;
        }
    }

    public static void Remove(string token)
    {
        lock (_tokens) { _tokens.Remove(token); }
        ConnectionCounter.OnLogout(token);
    }

    private static void CleanupExpired()
    {
        lock (_tokens) { CleanupExpiredInternal(); }
    }

    private static void CleanupExpiredInternal()
    {
        var expired = _tokens.Where(k => k.Value.ExpireAt < DateTime.UtcNow).Select(k => k.Key).ToList();
        foreach (var k in expired)
        {
            _tokens.Remove(k);
            ConnectionCounter.OnLogout(k);
        }
    }
}

public class TokenInfo
{
    public string UserId { get; set; } = "";
    public string Username { get; set; } = "";
    public string Name { get; set; } = "";
    public DateTime ExpireAt { get; set; }
}
