using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QP11.Core.Interfaces;

namespace QP11.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ClientsController : ControllerBase
{
    private readonly IClientRepository _clientRepo;
    private readonly ILogger<ClientsController> _logger;
    private readonly IDbConnectionFactory _dbFactory;

    public ClientsController(IClientRepository clientRepo, ILogger<ClientsController> logger, IDbConnectionFactory dbFactory)
    {
        _clientRepo = clientRepo;
        _logger = logger;
        _dbFactory = dbFactory;
    }

    /// <summary>
    /// 搜索客户
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] string? keyword)
    {
        try
        {
            _logger.LogInformation("[ClientsController.Search] keyword='{Kw}'", keyword);

            using var db = await _dbFactory.CreateAsync();
            var sql = @"SELECT TOP 50 cid AS Cid, name AS Name, mobile AS Mobile, tel AS Tel,
                         linkman AS Linkman, address AS Address, level AS Level, credit AS Credit
                         FROM client_infor";
            IEnumerable<dynamic> result;

            if (!string.IsNullOrWhiteSpace(keyword))
            {
                var kw = $"%{keyword}%";
                sql += @" WHERE name LIKE @Kw OR name_py LIKE @Kw OR cid LIKE @Kw
                          OR mobile LIKE @Kw OR tel LIKE @Kw";
                result = await Dapper.SqlMapper.QueryAsync(db, sql, new { Kw = kw });
            }
            else
            {
                result = await Dapper.SqlMapper.QueryAsync(db, sql);
            }

            var data = result.Select(c => new
            {
                Cid = c.Cid,
                Name = c.Name,
                Mobile = c.Mobile,
                Tel = c.Tel,
                Linkman = c.Linkman,
                Address = c.Address,
                Level = c.Level,
                Credit = c.Credit
            });

            return Ok(new { data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ClientsController.Search] 异常: {Msg}", ex.Message);
            return BadRequest(new { error = "查询失败，请稍后重试" });
        }
    }

    /// <summary>
    /// 获取客户详情
    /// </summary>
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(string id)
    {
        try
        {
            var client = await _clientRepo.GetByIdAsync(id);
            if (client == null) return NotFound(new { error = "客户不存在" });

            // 查询欠款总额
            using var db = await _dbFactory.CreateAsync();
            var arrearTotal = await Dapper.SqlMapper.ExecuteScalarAsync<decimal?>(
                db,
                @"SELECT COALESCE(SUM(ISNULL(arrear, 0)), 0) FROM bill_sell
                  WHERE client = @Cid AND ISNULL(arrear, 0) > 0.01 AND ISNULL(flag, 0) <> -1",
                new { Cid = id });

            return Ok(new
            {
                client.Cid, client.Name, client.Mobile, client.Tel,
                client.Linkman, client.Address, client.Level, client.Credit,
                ArrearTotal = arrearTotal ?? 0
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[ClientsController.GetById] 异常");
            return BadRequest(new { error = "查询客户失败，请稍后重试" });
        }
    }
}

/// <summary>
/// 员工/业务员接口
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class WorkersController : ControllerBase
{
    private readonly ILogger<WorkersController> _logger;
    private readonly IDbConnectionFactory _dbFactory;

    public WorkersController(ILogger<WorkersController> logger, IDbConnectionFactory dbFactory)
    {
        _logger = logger;
        _dbFactory = dbFactory;
    }

    /// <summary>
    /// 获取业务员列表
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        try
        {
            _logger.LogInformation("[WorkersController.GetAll] 开始查询业务员");
            using var db = await _dbFactory.CreateAsync();
            var workers = await Dapper.SqlMapper.QueryAsync(
                db,
                "SELECT workid, name FROM work_infor ORDER BY workid");
            var list = workers.ToList();
            _logger.LogInformation("[WorkersController.GetAll] 返回 {Count} 条", list.Count);
            return Ok(new { data = list });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[WorkersController.GetAll] 异常");
            return BadRequest(new { error = "查询业务员失败，请稍后重试" });
        }
    }
}
