using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using QP11.Core.AI;
using QP11.Core.Interfaces;

namespace QP11.Services.AI.Tools;

public sealed class GetSellHistoryTool : ChatToolBase
{
    private readonly IPartQueryService _partQuery;
    private const int DefaultTop = 10;

    public GetSellHistoryTool(IPartQueryService partQuery) => _partQuery = partQuery;

    public override string Name => "get_sell_history";

    public override string Description =>
        "查询某配件近期销售流水（单号、数量、单价、开单金额、日期、客户）。可按客户名过滤。";

    public override JsonNode ParameterSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["partId"] = NumberSchema("配件ID（partid）"),
            ["clientName"] = StringSchema("可选客户名称过滤"),
            ["top"] = NumberSchema("返回条数上限，默认 10")
        },
        ["required"] = new JsonArray("partId")
    };

    public override async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken cancellationToken = default)
    {
        var partId = GetLongArg(args, "partId");
        if (partId is null || partId <= 0)
            return ToolResult.Fail("partId 无效");

        var clientName = GetStringArg(args, "clientName").Trim();
        var top = GetIntArg(args, "top", DefaultTop) ?? DefaultTop;
        if (top <= 0) top = DefaultTop;

        var rows = await _partQuery.GetSellHistoryAsync(partId.Value,
            string.IsNullOrEmpty(clientName) ? null : clientName, top, cancellationToken);

        var items = rows.Select(r => new JsonObject
        {
            ["sn"] = r.Sn ?? "",
            ["amount"] = r.Amount,
            ["price"] = r.Price,
            ["billPrice"] = r.BillPrice,
            ["datetime"] = r.Datetime?.ToString("yyyy-MM-dd HH:mm") ?? "",
            ["clientName"] = r.ClientName ?? ""
        }).ToList();

        var payload = new JsonObject
        {
            ["count"] = items.Count,
            ["items"] = new JsonArray(items.Cast<JsonNode>().ToArray())
        };
        return ToolResult.Ok(payload);
    }
}
