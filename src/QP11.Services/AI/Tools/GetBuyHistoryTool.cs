using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using QP11.Core.AI;
using QP11.Core.Interfaces;

namespace QP11.Services.AI.Tools;

public sealed class GetBuyHistoryTool : ChatToolBase
{
    private readonly IPartQueryService _partQuery;
    private const int DefaultTop = 10;

    public GetBuyHistoryTool(IPartQueryService partQuery) => _partQuery = partQuery;

    public override string Name => "get_buy_history";

    public override string Description =>
        "查询某配件近期采购流水（数量、采购单价、日期、供应商）。需要配件ID。";

    public override JsonNode ParameterSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["partId"] = NumberSchema("配件ID（partid）"),
            ["top"] = NumberSchema("返回条数上限，默认 10")
        },
        ["required"] = new JsonArray("partId")
    };

    public override async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken cancellationToken = default)
    {
        var partId = GetLongArg(args, "partId");
        if (partId is null || partId <= 0)
            return ToolResult.Fail("partId 无效");

        var top = GetIntArg(args, "top", DefaultTop) ?? DefaultTop;
        if (top <= 0) top = DefaultTop;

        var rows = await _partQuery.GetBuyHistoryAsync(partId.Value, top, cancellationToken);

        var items = rows.Select(r => new JsonObject
        {
            ["amount"] = r.Amount,
            ["inprice"] = r.Inprice,
            ["datetime"] = r.Datetime?.ToString("yyyy-MM-dd HH:mm") ?? "",
            ["supplierName"] = r.SupplierName ?? ""
        }).ToList();

        var payload = new JsonObject
        {
            ["count"] = items.Count,
            ["items"] = new JsonArray(items.Cast<JsonNode>().ToArray())
        };
        return ToolResult.Ok(payload);
    }
}
