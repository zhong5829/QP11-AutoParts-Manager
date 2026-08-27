using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using QP11.Core.Entities;
using QP11.Core.Constants;
using QP11.Core.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace QP11.WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SellController : ControllerBase
{
    private readonly ISellService _sellService;
    private readonly ISellRepository _sellRepo;
    private readonly IPartRepository _partRepo;
    private readonly IArrearageRepository _arrearRepo;
    private readonly IDbConnectionFactory _dbFactory;
    private readonly ILogger<SellController> _logger;

    public SellController(
        ISellService sellService,
        ISellRepository sellRepo,
        IPartRepository partRepo,
        IArrearageRepository arrearRepo,
        IDbConnectionFactory dbFactory,
        ILogger<SellController> logger)
    {
        _sellService = sellService;
        _sellRepo = sellRepo;
        _partRepo = partRepo;
        _arrearRepo = arrearRepo;
        _dbFactory = dbFactory;
        _logger = logger;
    }

    /// <summary>
    /// 创建销售单 — 委托 SellService.CreateSellOrderAsync 执行核心事务
    /// </summary>
    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder([FromBody] CreateSellOrderRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(kvp => kvp.Value!.Errors.Count > 0)
                    .Select(kvp => $"{kvp.Key}: {string.Join("; ", kvp.Value!.Errors.Select(e => e.ErrorMessage))}");
                _logger.LogWarning("[CreateOrder] 模型验证失败! {Errors}", string.Join(" | ", errors));
                return BadRequest(new { error = "请求参数有误", details = errors.ToList() });
            }

            _logger.LogInformation("[CreateOrder] ClientId={ClientId} Details={Cnt} Worker={W}",
                request.ClientId, request.Details?.Count ?? 0, request.WorkerId);

            if (string.IsNullOrWhiteSpace(request.ClientId))
                return BadRequest(new { error = "请选择客户" });
            if (request.Details == null || request.Details.Count == 0)
                return BadRequest(new { error = "销售明细不能为空" });

            // 计算合计（对齐桌面端逻辑：discountRate=0表示无折扣/原价）
            var totalAmount = request.Details.Sum(d => d.Price * d.Amount);
            var discountRate = request.DiscountRate;
            var billTotal = discountRate > 0 ? Math.Round(totalAmount * discountRate, 2) : totalAmount;
            var cash = request.Cash ?? 0;
            var weixin = request.Weixin ?? 0;
            var zhifubao = request.Zhifubao ?? 0;
            var checks = request.Checks ?? 0;
            var totalPaid = cash + weixin + zhifubao + checks;

            // 批量查询配件进价
            var partIds = request.Details.Select(d => d.PartId).Where(p => p.HasValue).Select(p => p!.Value).Distinct().ToList();
            var partCostMap = await _partRepo.GetByIdsAsync(partIds);

            // 构建单据头
            var bill = new BillSell
            {
                Client = request.ClientId,
                Worker = request.WorkerId,
                Operator = "WebAPI",
                Datetime = DateTime.Now,
                DiscountRate = discountRate,
                BillPayment = billTotal,
                Collection = 0,
                Cash = cash,
                Weixin = weixin,
                Zhifubao = zhifubao,
                Checks = checks,
                Arrear = Math.Max(0, billTotal - totalPaid),
                Yunfei = 0,
                Flag = (int)BusinessConstants.BillFlag.Confirmed,
                Memo = request.Memo ?? "",
                Checkno = request.Checkno ?? ""
            };

            // 构建明细
            var detailList = request.Details.Select(d =>
            {
                var cb = d.PartId.HasValue && partCostMap.TryGetValue(d.PartId.Value, out var p) ? (p.Inprice ?? 0m) : 0m;
                return new DetailSell
                {
                    Partid = d.PartId,
                    Partno = d.PartNo,
                    Name = d.PartName,
                    Amount = (long)d.Amount,
                    Price = d.Price,
                    BillPrice = d.BillPrice,
                    Stotal = Math.Round(d.Price * d.Amount, 2),
                    Btotal = Math.Round(d.BillPrice * d.Amount, 2),
                    Cb = cb,
                    Place = d.Place,
                    Cartype = d.Cartype,
                    CarMark = d.CarMark,
                    Memo = d.Memo,
                    Flag = (int)BusinessConstants.BillFlag.Confirmed,
                    Type = BusinessConstants.DetailType.Normal
                };
            }).ToList();

            // 委托 SellService 执行事务（单据+明细+库存+欠款原子操作）
            var billNo = await _sellService.CreateSellOrderAsync(bill, detailList, cash, weixin, zhifubao, 0);

            var arrear = billTotal - totalPaid;
            return Ok(new { success = true, sn = billNo, total = billTotal, paid = totalPaid, arrear });
        }
        catch (QP11.Core.Exceptions.BusinessRuleException ex)
        {
            _logger.LogWarning("[CreateOrder] 业务规则校验失败: {Msg}", ex.Message);
            return BadRequest(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[CreateOrder] 异常! Msg={Msg}", ex.Message);
            return BadRequest(new { error = "操作失败，请稍后重试" });
        }
    }

    /// <summary>
    /// 查询销售单列表
    /// </summary>
    [HttpGet("orders")]
    public async Task<IActionResult> GetOrders(
        [FromQuery] DateTime? start,
        [FromQuery] DateTime? end,
        [FromQuery] string? client,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        try
        {
            var (data, total) = await _sellRepo.GetPagedOrdersAsync(start, end, client, page, pageSize);

            if (total == 0)
                return Ok(new { data = Array.Empty<object>(), total = 0, page, pageSize });

            var list = data.Select(b => new
            {
                b.Sn,
                Client = b.ClientName ?? "",
                Worker = b.WorkerName ?? "",
                b.Total, b.BillTotal,
                b.Flag, b.Datetime,
                FlagText = BusinessConstants.GetFlagText((int)b.Flag)
            }).ToList();

            return Ok(new { data = list, total, page, pageSize });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GetOrders] 异常: {Msg}", ex.Message);
            return BadRequest(new { error = "查询失败，请稍后重试" });
        }
    }

    /// <summary>
    /// 获取销售单详情（含明细）
    /// </summary>
    [HttpGet("orders/{sn}")]
    public async Task<IActionResult> GetOrder(string sn)
    {
        try
        {
            var bill = await _sellRepo.GetBySnAsync(sn);
            if (bill == null) return NotFound(new { error = "单据不存在" });

            using var db = await _dbFactory.CreateAsync();
            dynamic? clientName = await Dapper.SqlMapper.QueryFirstOrDefaultAsync<string>(
                db, "SELECT name FROM client_infor WHERE cid = @Id", new { Id = bill.Client });
            dynamic? workerName = await Dapper.SqlMapper.QueryFirstOrDefaultAsync<string>(
                db, "SELECT name FROM work_infor WHERE workid = @Id", new { Id = bill.Worker });

            var details = await _sellRepo.GetDetailsAsync(sn);
            return Ok(new
            {
                bill = new
                {
                    bill.Sn,
                    Client = clientName ?? bill.Client,
                    Worker = workerName ?? bill.Worker,
                    bill.Operator,
                    bill.Total, bill.BillTotal, bill.DiscountRate,
                    bill.Cash, bill.Weixin, bill.Zhifubao, bill.Cardpay,
                    bill.Checks, bill.Collection, bill.Arrear, bill.Yunfei,
                    bill.Flag, bill.Datetime, bill.Memo, bill.Checkno
                },
                details = details.Select(d => new
                {
                    d.Id, d.Sn, d.Partid, d.Partno, d.Name, d.Unit,
                    d.Place, d.Amount, d.Amount2, d.Price, d.BillPrice,
                    d.Stotal, d.Btotal, d.Cartype, d.Area, d.CarMark,
                    d.Memo, d.Tsn, d.Type, d.Cb, d.PartGg,
                    d.DiscountRate, d.Datetime
                })
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[GetOrder] 异常: {Msg}", ex.Message);
            return BadRequest(new { error = "查询失败，请稍后重试" });
        }
    }

    /// <summary>
    /// 作废销售单 — 委托 SellService.VoidSellOrderAsync（回补库存+删除欠款，事务内原子操作）
    /// </summary>
    [HttpDelete("orders/{sn}")]
    public async Task<IActionResult> VoidOrder(string sn)
    {
        try
        {
            var details = (await _sellRepo.GetDetailsAsync(sn)).ToList();

            // 委托 SellService 执行作废事务（库存回补+删除欠款，在同一事务内原子执行）
            await _sellService.VoidSellOrderAsync(sn, details);

            return Ok(new { success = true, message = $"单据 {sn} 已作废，库存已调整，欠款已清除" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[VoidOrder] 异常: {Msg}", ex.Message);
            return BadRequest(new { error = "操作失败，请稍后重试" });
        }
    }

    /// <summary>
    /// 静默打印销售单
    /// </summary>
    [HttpPost("print/{sn}")]
    public async Task<IActionResult> PrintBill(string sn)
    {
        try
        {
            var wpfAsm = Assembly.Load("QP11.Wpf");
            var svcType = wpfAsm.GetType("QP11.Wpf.Services.WebPrintService")
                ?? throw new Exception("未找到 WebPrintService 类型");
            var method = svcType.GetMethods()
                .FirstOrDefault(m => m.Name == "SilentPrintBill" && m.IsStatic && m.GetParameters().Length == 1)
                ?? throw new Exception("未找到 SilentPrintBill 方法");

            string? errorMsg = null;
            InvokeOnDispatcher(() =>
            {
                var task = (Task<string?>)method.Invoke(null, new object[] { sn })!;
                task.GetAwaiter().GetResult();
                errorMsg = task.Result;
            });

            if (errorMsg != null)
                return BadRequest(new { error = $"打印失败: {errorMsg}" });

            _logger.LogInformation("[PrintBill] 单据 {Sn} 已发送至打印机", sn);
            return Ok(new { success = true, message = $"单据 {sn} 已发送打印" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[PrintBill] 异常: {Msg}", ex.Message);
            return BadRequest(new { error = "打印失败，请稍后重试" });
        }
    }

    private static void InvokeOnDispatcher(Action action)
    {
        var pfAsm = Assembly.Load("PresentationFramework");
        var appType = pfAsm.GetType("System.Windows.Application")!;
        var app = appType.GetProperty("Current")!.GetValue(null);
        if (app == null) throw new Exception("Application.Current 为空，无法访问UI线程");

        var dispatcher = app.GetType().GetProperty("Dispatcher")!.GetValue(app)!;
        var invokeMethod = dispatcher.GetType().GetMethods()
            .FirstOrDefault(m => m.Name == "Invoke" && m.GetParameters().Length == 1)
            ?? throw new Exception("未找到 Dispatcher.Invoke 方法");

        invokeMethod.Invoke(dispatcher, new object[] { action });
    }
}

/// <summary>
/// 创建销售单请求体
/// </summary>
public class CreateSellOrderRequest
{
    [Required]
    public string ClientId { get; set; } = "";

    public string? WorkerId { get; set; }

    public decimal DiscountRate { get; set; } = 0m;

    public decimal? Cash { get; set; } = 0;
    public decimal? Weixin { get; set; } = 0;
    public decimal? Zhifubao { get; set; } = 0;
    public decimal? Checks { get; set; } = 0;

    public string? Memo { get; set; }
    public string? Checkno { get; set; }

    [Required]
    public List<SellDetailItem> Details { get; set; } = new();
}

/// <summary>
/// 销售明细项
/// </summary>
public class SellDetailItem
{
    public long? PartId { get; set; }
    public string? PartNo { get; set; }
    public string? PartName { get; set; }
    public decimal Amount { get; set; } = 1;
    public decimal Price { get; set; }
    public decimal BillPrice { get; set; }
    public decimal DiscountRate { get; set; } = 1m;
    public string? Cartype { get; set; }
    public string? CarMark { get; set; }
    public string? Place { get; set; }
    public string? Memo { get; set; }
}
