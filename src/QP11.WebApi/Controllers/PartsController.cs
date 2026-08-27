using Microsoft.AspNetCore.Mvc;
using QP11.Core.Models;
using QP11.Data.Infrastructure;
using QP11.Core.Interfaces;
using System.Text.Json;

namespace QP11.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PartsController : ControllerBase
{
    private readonly IPartRepository _partRepo;
    private readonly IPartQueryService _partQuery;
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ILogger<PartsController> _logger;

    public PartsController(IPartRepository partRepo, IPartQueryService partQuery, IDbConnectionFactory dbFactory, ILogger<PartsController> logger)
    {
        _partRepo = partRepo;
        _partQuery = partQuery;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <summary>
    /// 诊断接口：检查数据库连接和基础数据
    /// </summary>
    [HttpGet("diag")]
    public async Task<IActionResult> Diagnose()
    {
        try
        {
            var info = new Dictionary<string, object>();

            // 数据库连接信息
            info["ConnectionMode"] = _dbFactory.ConnectionMode;
            info["Provider"] = _dbFactory.Provider;
            info["ConnectionString_Masked"] = MaskConnStr(DatabaseFactory.ConnectionString);
            info["LastError"] = DatabaseFactory.LastError;

            // 测试连接
            var connOk = DatabaseFactory.TestConnection(out string connMsg);
            info["TestConnection"] = connOk ? "OK" : "FAIL";
            info["TestMessage"] = connMsg;

            // 查询配件总数
            using var db = await _dbFactory.CreateAsync();
            try
            {
                var total = await Dapper.SqlMapper.ExecuteScalarAsync<int>(
                    db, "SELECT COUNT(*) FROM part_data WHERE (DEL IS NULL OR DEL = '0')");
                info["PartTotalCount"] = total;
            }
            catch (Exception ex)
            {
                info["PartTotalCount_Error"] = ex.Message;
            }

            // 查询前1条配件（验证数据可读）
            try
            {
                var sample = await Dapper.SqlMapper.QueryFirstOrDefaultAsync<string>(
                    db,
                    "SELECT TOP 1 partno + ' | ' + name AS sample FROM part_data WHERE (DEL IS NULL OR DEL = '0') ORDER BY partid");
                info["SamplePart"] = sample ?? "(空)";
            }
            catch (Exception ex)
            {
                info["SamplePart_Error"] = ex.Message;
            }

            // 测试 LIKE 搜索
            try
            {
                var likeCount = await Dapper.SqlMapper.ExecuteScalarAsync<int>(
                    db,
                    "SELECT COUNT(*) FROM part_data WHERE (DEL IS NULL OR DEL = '0') AND name LIKE '%配%'");
                info["LikeTest_name_包含配"] = likeCount;
            }
            catch (Exception ex)
            {
                info["LikeTest_Error"] = ex.Message;
            }

            // 检查 appsettings.json 路径
            info["BaseDirectory"] = AppDomain.CurrentDomain.BaseDirectory;

            return Ok(new { success = true, diag = info });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Diagnose] 诊断过程异常");
            return Ok(new { success = false, error = "诊断过程发生错误，请查看服务器日志" });
        }
    }

    /// <summary>
    /// 搜索配件列表（含库存信息）—— 对齐桌面端 GetStockListAdvancedAsync 逻辑
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? keyword,
        [FromQuery] string? classId,
        [FromQuery] string? partNo,
        [FromQuery] string? partName,
        [FromQuery] string? cartype,
        [FromQuery] int matchMode = 3, // 0=精确 1=左匹配 2=右匹配 3=包含(默认)
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        try
        {
            _logger.LogInformation("[PartsController.Search] partNo='{Pn}' partName='{Name}' cartype='{Ct}' matchMode={Mm}", partNo, partName, cartype, matchMode);

            IEnumerable<object>? data;

            // 有具体搜索条件时，使用高级搜索（对齐桌面端逻辑）
            if (!string.IsNullOrWhiteSpace(partNo) || !string.IsNullOrWhiteSpace(partName) || !string.IsNullOrWhiteSpace(cartype))
            {
                _logger.LogInformation("[Search] 使用 GetStockListAdvancedAsync 高级搜索");

                // 对齐桌面端：纯ASCII输入视为拼音搜索，自动生成拼音参数
                // 桌面端用 PinyinHelper.GetPinyinInitials()，但纯ASCII输入经转换后结果就是原值的小写形式
                var partNamePy = !string.IsNullOrEmpty(partName) && IsPureAscii(partName) ? partName.ToLowerInvariant() : null;
                var cartypePy = !string.IsNullOrEmpty(cartype) && IsPureAscii(cartype) ? cartype.ToLowerInvariant() : null;

                var parts = await _partRepo.GetStockListAdvancedAsync(
                    partNo: partNo,
                    partName: partName,
                    partNamePy: partNamePy,
                    cartype: cartype,
                    cartypePy: cartypePy,
                    queryMode: matchMode);
                var partsList = parts.ToList();
                _logger.LogInformation("[Search] 高级搜索返回 {Count} 条", partsList.Count);

                // 精简字段：15→10（列表只需核心字段，Stock 实时查库不缓存）
                data = partsList.Select(p => new
                {
                    p.PartId, p.PartNo, p.Name, p.CarType,
                    p.Unit, p.Place,
                    LsPrice = p.LsPrice ?? 0,
                    PfPrice = p.PfPrice ?? 0,
                    Stock = p.Amount ?? 0,
                    NamePy = p.NamePy
                }).ToList();
            }
            else if (!string.IsNullOrWhiteSpace(keyword))
            {
                // 兼容旧接口：单关键词搜索
                _logger.LogInformation("[Search] 使用 SearchAsync 单关键词, keyword={Kw}", keyword);
                var parts = await _partRepo.SearchAsync(keyword);
                var partsList = parts.ToList();
                _logger.LogInformation("[Search] SearchAsync 返回 {Count} 条", partsList.Count);

                var partIds = partsList.Select(p => p.Partid).ToList();
                var stockDict = await GetStockDict(partIds);

                data = partsList.Select(p =>
                {
                    stockDict.TryGetValue(p.Partid, out var stock);
                    return new
                    {
                        PartId = p.Partid, PartNo = p.Partno, Name = p.Name,
                        CarType = p.Cartype,
                        Unit = p.Unit, Place = p.Place,
                        LsPrice = p.Lsprice, PfPrice = p.Pfprice,
                        NamePy = p.NamePy,
                        Stock = stock?.Amount ?? 0
                    };
                }).ToList();
            }
            else
            {
                // 全部为空：加载默认库存列表（对齐桌面端默认行为）
                _logger.LogInformation("[Search] 无条件，加载默认库存列表");
                var parts = await _partRepo.GetStockListAsync(null, 200);
                var partsList = parts.ToList();
                _logger.LogInformation("[Search] 默认列表返回 {Count} 条", partsList.Count);

                data = partsList.Select(p => new
                {
                    p.PartId, p.PartNo, p.Name, p.CarType,
                    p.Unit, p.Place,
                    LsPrice = p.LsPrice ?? 0,
                    PfPrice = p.PfPrice ?? 0,
                    Stock = p.Amount ?? 0,
                    NamePy = p.NamePy
                }).ToList();
            }

            var finalData = ((IEnumerable<dynamic>)data!).ToList();
            _logger.LogInformation("[Search] 最终返回 {Count} 条", finalData.Count);

            return Ok(new { data = finalData });
        }
        catch (Exception ex)
    {
        _logger.LogError(ex, "[Search] 异常: {Msg}", ex.Message);
        return BadRequest(new { error = "搜索失败，请稍后重试" });
    }
    }

    /// <summary>
    /// 获取配件详情（含历史售价）
    /// </summary>
    [HttpGet("{partId}/sell-history")]
    public async Task<IActionResult> SellHistory(long partId, [FromQuery] string? clientId)
    {
        try
        {
            var history = await _partQuery.GetSellHistoryAsync(partId, clientId, 20);
            return Ok(new { data = history });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[SellHistory] 异常");
            return BadRequest(new { error = "查询销售历史失败，请稍后重试" });
        }
    }

    /// <summary>
    /// 获取配件价格区间（对齐桌面端 BtnHistory_Click）
    /// </summary>
    [HttpGet("{partId}/price-range")]
    public async Task<IActionResult> PriceRange(long partId)
    {
        try
        {
            var range = await _partQuery.GetPriceRangeAsync(partId);
            return Ok(new { data = range });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PriceRange] 异常");
            return BadRequest(new { error = "查询价格区间失败，请稍后重试" });
        }
    }

    /// <summary>
    /// 获取配件采购历史（对齐桌面端 LoadBuyHistoryAsync 第200-216行）
    /// </summary>
    [HttpGet("{partId}/buy-history")]
    public async Task<IActionResult> BuyHistory(long partId)
    {
        try
        {
            var data = await _partQuery.GetBuyHistoryAsync(partId, 20);
            return Ok(new { data = data });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[BuyHistory] 异常");
            return BadRequest(new { error = "查询采购历史失败，请稍后重试" });
        }
    }

    private async Task<Dictionary<long, dynamic>> GetStockDict(List<long> partIds)
    {
        if (partIds.Count == 0) return new();
        using var db = await _dbFactory.CreateAsync();
        var rows = await Dapper.SqlMapper.QueryAsync(
                db,
            "SELECT partid, amount FROM part_stock WHERE partid IN @Ids",
            new { Ids = partIds });
        var dict = new Dictionary<long, dynamic>();
        foreach (var r in rows) dict[(long)r.partid] = r;
        return dict;
    }

    /// <summary>判断字符串是否为纯ASCII字符（拼音/编号搜索意图）</summary>
    private static bool IsPureAscii(string text)
    {
        foreach (char c in text)
            if (c > 127) return false;
        return true;
    }

    private static string MaskConnStr(string s)
    {
        if (string.IsNullOrEmpty(s)) return "(空)";
        var patterns = new[] { "Pwd=", "PWD=", "Password=", "password=" };
        foreach (var p in patterns)
        {
            var idx = s.IndexOf(p, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var start = idx + p.Length;
                var end = s.IndexOf(';', start);
                if (end < 0) end = s.Length;
                return s.Substring(0, start) + "***" + s.Substring(end);
            }
        }
        // 只显示前30字符隐藏敏感信息
        return s.Length > 30 ? s.Substring(0, 30) + "..." : s;
    }
}
