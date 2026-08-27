using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using QP11.Core.AI;
using QP11.Core.Interfaces;

namespace QP11.Services.AI.Tools;

public sealed class GetStockAdvancedTool : ChatToolBase
{
    private readonly IPartRepository _partRepo;
    private const int MaxResults = 30;

    public GetStockAdvancedTool(IPartRepository partRepo) => _partRepo = partRepo;

    public override string Name => "search_stock_advanced";

    public override string Description =>
        "按多条件组合查询库存（配件编号、名称、车型、品牌分类）。匹配模式：0=精确,1=左匹配,2=右匹配,3=包含(默认)。";

    public override JsonNode ParameterSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["partNo"] = StringSchema("配件编号"),
            ["partName"] = StringSchema("配件名称"),
            ["cartype"] = StringSchema("车型"),
            ["className"] = StringSchema("品牌分类"),
            ["matchMode"] = NumberSchema("匹配模式：0=精确,1=左匹配,2=右匹配,3=包含(默认)")
        }
    };

    public override async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken cancellationToken = default)
    {
        var partNo = GetStringArg(args, "partNo").Trim();
        var partName = GetStringArg(args, "partName").Trim();
        var cartype = GetStringArg(args, "cartype").Trim();
        var className = GetStringArg(args, "className").Trim();
        var matchMode = GetIntArg(args, "matchMode", 3) ?? 3;

        if (string.IsNullOrEmpty(partNo) && string.IsNullOrEmpty(partName) &&
            string.IsNullOrEmpty(cartype) && string.IsNullOrEmpty(className))
        {
            return ToolResult.Fail("至少提供一个查询条件（partNo/partName/cartype/className）");
        }

        var stocks = await _partRepo.GetStockListAdvancedAsync(
            partNo: string.IsNullOrEmpty(partNo) ? null : partNo,
            partName: string.IsNullOrEmpty(partName) ? null : partName,
            cartype: string.IsNullOrEmpty(cartype) ? null : cartype,
            className: string.IsNullOrEmpty(className) ? null : className,
            queryMode: matchMode);

        var list = stocks.Where(s => s.Place != "废品仓").Take(MaxResults).Select(s => new JsonObject
        {
            ["partId"] = s.PartId,
            ["partNo"] = s.PartNo ?? "",
            ["name"] = s.Name ?? "",
            ["carType"] = s.CarType ?? "",
            ["carName"] = s.CarName ?? "",
            ["unit"] = s.Unit ?? "",
            ["place"] = s.Place ?? "",
            ["amount"] = s.Amount ?? 0L,
            ["lsPrice"] = s.LsPrice ?? 0m,
            ["pfPrice"] = s.PfPrice ?? 0m
        }).ToList();

        var payload = new JsonObject
        {
            ["count"] = list.Count,
            ["items"] = new JsonArray(list.Cast<JsonNode>().ToArray())
        };
        return ToolResult.Ok(payload);
    }
}
