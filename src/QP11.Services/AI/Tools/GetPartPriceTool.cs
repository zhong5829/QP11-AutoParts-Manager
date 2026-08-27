using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using QP11.Core.AI;
using QP11.Core.Interfaces;

namespace QP11.Services.AI.Tools;

public sealed class GetPartPriceTool : ChatToolBase
{
    private readonly IPartQueryService _partQuery;

    public GetPartPriceTool(IPartQueryService partQuery) => _partQuery = partQuery;

    public override string Name => "get_part_price";

    public override string Description =>
        "查询某配件历史销售价格区间（最高价、最低价）。需要配件ID。";

    public override JsonNode ParameterSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["partId"] = NumberSchema("配件ID（partid）")
        },
        ["required"] = new JsonArray("partId")
    };

    public override async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken cancellationToken = default)
    {
        var partId = GetLongArg(args, "partId");
        if (partId is null || partId <= 0)
            return ToolResult.Fail("partId 无效");

        var range = await _partQuery.GetPriceRangeAsync(partId.Value, cancellationToken);
        var payload = new JsonObject
        {
            ["partId"] = partId.Value,
            ["maxPrice"] = range.MaxPrice,
            ["minPrice"] = range.MinPrice
        };
        return ToolResult.Ok(payload);
    }
}
