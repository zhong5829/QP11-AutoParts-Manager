using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using QP11.Core.AI;
using QP11.Core.Interfaces;
using QP11.Core.Models;

namespace QP11.Services.AI.Tools;

public sealed class GetStockTool : ChatToolBase
{
    private readonly IPartRepository _partRepo;
    private const int DefaultTop = 30;
    private const int MaxTop = 100;

    // 复用 SearchPartsTool 的车型关键词拆分逻辑
    private static readonly string[] CarModelPrefixes = new[]
    {
        "新君威", "君威", "新君越", "君越", "新凯越", "凯越", "新英朗", "英朗",
        "新赛欧", "赛欧", "新爱唯欧", "爱唯欧", "新昂科拉", "昂科拉", "新昂科威", "昂科威",
        "新迈锐宝", "迈锐宝", "新科鲁兹", "科鲁兹", "新科沃兹", "科沃兹",
        "新景程", "景程", "新爱丽舍", "爱丽舍", "新速腾", "速腾", "新朗逸", "朗逸",
        "新宝来", "宝来", "新迈腾", "迈腾", "新帕萨特", "帕萨特", "新桑塔纳", "桑塔纳",
        "新捷达", "捷达", "新polo", "polo", "新朗行", "朗行", "新凌渡", "凌渡",
        "新途观", "途观", "新途昂", "途昂", "新途安", "途安", "新辉昂", "辉昂",
        "哈弗H6", "哈弗H2", "哈弗H9", "哈弗", "五菱宏光", "五菱",
        "长安CS75", "长安CS35", "长安", "吉利博越", "吉利", "比亚迪",
        "本田", "丰田", "日产", "马自达", "现代", "起亚", "福特", "奥迪",
        "宝马", "奔驰", "大众", "别克", "雪佛兰", "凯迪拉克"
    };

    public GetStockTool(IPartRepository partRepo) => _partRepo = partRepo;

    public override string Name => "get_stock";

    public override string Description =>
        "查询配件库存列表（含实时库存数量、库位、零售价、批发价）。" +
        "支持车型+配件名组合查询，如'新君威前减总成'会自动拆分搜索。" +
        "不传关键词返回前若干条。";

    public override JsonNode ParameterSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["keyword"] = StringSchema("可选过滤关键词（配件名称/编号/拼音/车型），支持组合查询"),
            ["top"] = NumberSchema("返回条数上限，默认 30，最大 100")
        }
    };

    public override async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken cancellationToken = default)
    {
        var keyword = GetStringArg(args, "keyword").Trim();
        var top = GetIntArg(args, "top", DefaultTop) ?? DefaultTop;
        if (top <= 0) top = DefaultTop;
        if (top > MaxTop) top = MaxTop;

        // 术语展开
        var expandedTerms = PartTermExpander.Expand(keyword);
        var seen = new HashSet<long>();

        // 策略1：对所有展开词做库存列表搜索（排除废品仓，去重）
        var results = new List<PartStockDisplay>();
        foreach (var term in expandedTerms)
        {
            var stocks = await _partRepo.GetStockListAsync(string.IsNullOrEmpty(term) ? null : term, top);
            foreach (var s in stocks.Where(s => s.Place != "废品仓"))
            {
                if (seen.Add(s.PartId))
                    results.Add(s);
            }
        }

        // 策略2：若策略1无结果，尝试拆分关键词走高级搜索
        if (results.Count == 0 && !string.IsNullOrEmpty(keyword))
        {
            foreach (var term in expandedTerms)
            {
                var (carType, partName) = SplitKeyword(term);
                if (string.IsNullOrEmpty(carType) || string.IsNullOrEmpty(partName)) continue;

                var advanced = await _partRepo.GetStockListAdvancedAsync(
                    partName: partName,
                    cartype: carType,
                    queryMode: 3);
                foreach (var s in advanced.Where(s => s.Place != "废品仓"))
                {
                    if (seen.Add(s.PartId))
                        results.Add(s);
                }

                // 短词兜底
                if (results.Count == 0 && partName.Length > 2)
                {
                    var shortName = partName.Substring(0, 2);
                    var advancedShort = await _partRepo.GetStockListAdvancedAsync(
                        partName: shortName,
                        cartype: carType,
                        queryMode: 3);
                    foreach (var s in advancedShort.Where(s => s.Place != "废品仓"))
                    {
                        if (seen.Add(s.PartId))
                            results.Add(s);
                    }
                }

                if (results.Count > 0) break;
            }
        }

        // 策略3：仅按配件名短词搜索（不限定车型），扩大召回
        if (results.Count == 0 && !string.IsNullOrEmpty(keyword) && keyword.Length >= 2)
        {
            foreach (var term in expandedTerms)
            {
                var nameHint = ExtractPartNameHint(term);
                if (string.IsNullOrEmpty(nameHint)) continue;
                var byNameOnly = await _partRepo.GetStockListAdvancedAsync(
                    partName: nameHint,
                    queryMode: 3);
                foreach (var s in byNameOnly.Where(s => s.Place != "废品仓"))
                {
                    if (seen.Add(s.PartId))
                        results.Add(s);
                }
                if (results.Count > 0) break;
            }
        }

        // 策略4：2字滑动窗口
        if (results.Count == 0 && !string.IsNullOrEmpty(keyword) && keyword.Length >= 4)
        {
            var seg1 = keyword.Substring(0, 2);
            var seg2 = keyword.Substring(2);
            if (seg2.Length >= 2)
            {
                var advanced2 = await _partRepo.GetStockListAdvancedAsync(
                    partName: seg2,
                    cartype: seg1,
                    queryMode: 3);
                results = advanced2.Where(s => s.Place != "废品仓").Take(top).ToList();
            }
        }

        var list = results.Select(s => new JsonObject
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

    private static (string? carType, string? partName) SplitKeyword(string keyword)
    {
        foreach (var car in CarModelPrefixes.OrderByDescending(c => c.Length))
        {
            var idx = keyword.IndexOf(car, System.StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var carType = keyword.Substring(idx, car.Length);
                var partName = keyword.Remove(idx, car.Length).Trim();
                if (!string.IsNullOrEmpty(partName))
                    return (carType, partName);
            }
        }
        return (null, null);
    }

    private static string? ExtractPartNameHint(string keyword)
    {
        var remaining = keyword;
        foreach (var car in CarModelPrefixes.OrderByDescending(c => c.Length))
        {
            var idx = remaining.IndexOf(car, System.StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
                remaining = remaining.Remove(idx, car.Length);
        }
        remaining = remaining.Trim();
        if (remaining.Length < 2) return null;
        return remaining.Substring(0, 2);
    }
}
