using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using QP11.Core.AI;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using QP11.Core.Models;

namespace QP11.Services.AI.Tools;

public sealed class SearchPartsTool : ChatToolBase
{
    private readonly IPartRepository _partRepo;
    private const int MaxResults = 20;

    // 常见车型关键词（用于从用户输入中拆分车型和配件名）
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

    public SearchPartsTool(IPartRepository partRepo) => _partRepo = partRepo;

    public override string Name => "search_parts";

    public override string Description =>
        "按关键词模糊搜索配件档案（匹配配件名称、编号、拼音或车型）。" +
        "支持跨字段组合查询，如'新君威前减总成'会自动拆分为车型+配件名搜索。" +
        "用于用户口头描述配件时定位。";

    public override JsonNode ParameterSchema => new JsonObject
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["keyword"] = StringSchema("搜索关键词，可为配件名称、编号、拼音首字母或车型，支持车型+配件名组合")
        },
        ["required"] = new JsonArray("keyword")
    };

    public override async Task<ToolResult> ExecuteAsync(JsonObject args, CancellationToken cancellationToken = default)
    {
        var keyword = GetStringArg(args, "keyword").Trim();
        if (string.IsNullOrEmpty(keyword))
            return ToolResult.Fail("keyword 不能为空");

        // 术语展开：同义词 + 拼音首字母
        var expandedTerms = PartTermExpander.Expand(keyword);
        var seen = new HashSet<long>();

        // 策略1：对所有展开词做全字段模糊搜索（排除废品仓，去重）
        var results = new List<PartData>();
        foreach (var term in expandedTerms)
        {
            var parts = await _partRepo.SearchAsync(term);
            foreach (var p in parts.Where(p => p.Place != "废品仓"))
            {
                if (seen.Add(p.Partid))
                    results.Add(p);
            }
        }

        // 策略2：若策略1无结果，尝试拆分关键词走高级搜索
        if (results.Count == 0)
        {
            foreach (var term in expandedTerms)
            {
                var (carType, partName) = SplitKeyword(term);
                if (string.IsNullOrEmpty(carType) || string.IsNullOrEmpty(partName)) continue;

                // 先用完整 partName 搜
                var advanced = await _partRepo.GetStockListAdvancedAsync(
                    partName: partName,
                    cartype: carType,
                    queryMode: 3);
                foreach (var s in advanced.Where(s => s.Place != "废品仓"))
                {
                    if (seen.Add(s.PartId))
                        results.Add(MapStockToPartData(s));
                }

                // 若搜不到，用 partName 前2字短词再搜
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
                            results.Add(MapStockToPartData(s));
                    }
                }

                if (results.Count > 0) break;
            }
        }

        // 策略3：仅按配件名短词搜索（不限定车型），扩大召回
        if (results.Count == 0 && keyword.Length >= 2)
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
                        results.Add(MapStockToPartData(s));
                }
                if (results.Count > 0) break;
            }
        }

        // 策略4：2字滑动窗口
        if (results.Count == 0 && keyword.Length >= 4)
        {
            var seg1 = keyword.Substring(0, 2);
            var seg2 = keyword.Substring(2);
            if (seg2.Length >= 2)
            {
                var advanced2 = await _partRepo.GetStockListAdvancedAsync(
                    partName: seg2,
                    cartype: seg1,
                    queryMode: 3);
                foreach (var s in advanced2.Where(s => s.Place != "废品仓"))
                {
                    if (seen.Add(s.PartId))
                        results.Add(MapStockToPartData(s));
                }
            }
        }

        var list = results.Take(MaxResults).Select(p => new JsonObject
        {
            ["partId"] = p.Partid,
            ["partNo"] = p.Partno ?? "",
            ["name"] = p.Name ?? "",
            ["carType"] = p.Cartype ?? "",
            ["carName"] = p.Carname ?? "",
            ["unit"] = p.Unit ?? "",
            ["lsPrice"] = p.Lsprice ?? 0m,
            ["pfPrice"] = p.Pfprice ?? 0m,
            ["memo"] = p.Memo ?? ""
        }).ToList();

        var payload = new JsonObject
        {
            ["count"] = list.Count,
            ["items"] = new JsonArray(list.Cast<JsonNode>().ToArray())
        };
        return ToolResult.Ok(payload);
    }

    /// <summary>
    /// 从用户输入中拆分出车型关键词和配件名。
    /// 如 "新君威前减总成" → ("新君威", "前减总成")
    /// </summary>
    private static (string? carType, string? partName) SplitKeyword(string keyword)
    {
        // 按长度降序排列，优先匹配更精确的车型名
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

    /// <summary>
    /// 从关键词中提取配件名搜索提示（去掉已知车型后取前2字短词）。
    /// 如 "新君威前减总成" → "前减"（匹配"前减震器总成"等）
    /// </summary>
    private static string? ExtractPartNameHint(string keyword)
    {
        // 去掉已知车型词
        var remaining = keyword;
        foreach (var car in CarModelPrefixes.OrderByDescending(c => c.Length))
        {
            var idx = remaining.IndexOf(car, System.StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
                remaining = remaining.Remove(idx, car.Length);
        }
        remaining = remaining.Trim();
        if (remaining.Length < 2) return null;
        // 取前2字作为短词（最大召回率）
        return remaining.Substring(0, 2);
    }

    /// <summary>
    /// 将 PartStockDisplay 映射为 PartData（高级搜索返回的是库存视图，工具统一输出配件档案格式）
    /// </summary>
    private static PartData MapStockToPartData(PartStockDisplay s) => new()
    {
        Partid = s.PartId,
        Partno = s.PartNo,
        Name = s.Name,
        Cartype = s.CarType,
        Unit = s.Unit,
        Lsprice = s.LsPrice,
        Pfprice = s.PfPrice
    };
}
