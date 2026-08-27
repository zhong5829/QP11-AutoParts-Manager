using System;
using System.Data.Common;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using QP11.Core.Entities;
using QP11.Core.Interfaces;

namespace QP11.Services;

public class AuthService : IAuthService
{
    private readonly IDbConnectionFactory _dbFactory;

    public event EventHandler<UserInfor>? LoginSucceeded;

    public AuthService(IDbConnectionFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<UserInfor?> LoginAsync(string username, string password)
    {
        using var db = await _dbFactory.CreateAsync();
        var hashedPwd = Md5Hash(password);
        var sql = "SELECT * FROM user_infor WHERE username = @Username AND password = @Password AND state = 1";
        var user = await db.QueryFirstOrDefaultAsync<UserInfor>(sql, new { Username = username, Password = hashedPwd });

        if (user != null)
        {
            LoginSucceeded?.Invoke(this, user);
        }

        return user;
    }

    public static string Md5Hash(string input)
    {
        var data = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(data).ToLowerInvariant();
    }
}
