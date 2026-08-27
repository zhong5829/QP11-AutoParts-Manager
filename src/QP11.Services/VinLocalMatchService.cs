using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using QP11.Core.Entities;
using QP11.Core.Interfaces;
using Serilog;

namespace QP11.Services;

/// <summary>VIN配件本地库存匹配服务 — 编码+名称+车型三字段模糊匹配，含5分钟缓存</summary>
public class VinLocalMatchService : IVinLocalMatchService
{
    private readonly IDbConnectionFactory _dbFactory;

    // 本地配件缓存（5分钟过期，避免每次VIN查询全表扫描）
    private List<LocalPartRow>? _cachedParts;
    private DateTime _cacheTime = DateTime.MinValue;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public VinLocalMatchService(IDbConnectionFactory dbFactory)
    {
        _dbFactory = dbFactory;
    }

    /// <summary>对配件列表执行本地库存匹配，直接修改cards的LocalXxx字段</summary>
    public async Task EnrichWithLocalDataAsync(IEnumerable<VinPartCard> cards, VinDecodeResult vehicleInfo)
    {
        var cardList = cards.ToList();
        if (cardList.Count == 0) return;

        try
        {
            var allParts = await GetLocalPartsAsync();

            string vinSeries = vehicleInfo.Series ?? "";
            string vinModels = vehicleInfo.Models ?? "";

            Log.Information("VIN本地匹配: VIN车型={Brand} {Series} {Models}, 本地配件数={Count}, 待匹配={Cards}",
                vehicleInfo.Brand ?? "", vinSeries, vinModels, allParts.Count, cardList.Count);

            foreach (var card in cardList)
            {
                string cardModel = card.Model ?? "";
                string cardName = card.Name ?? "";
                string cardCategory = card.TenantCategoryName ?? "";

                // 编码前置匹配：编码不匹配直接判定失败
                if (string.IsNullOrEmpty(cardModel)) continue;

                var is318car = card.SourceName == "318car";
                var cardCores = is318car ? ExtractCoreCodes(cardModel) : [];
                var keywords = ExtractCoreKeywords(cardName);
                var catKeywords = ExtractCategoryKeywords(cardCategory);

                var candidates = FilterByCode(allParts, cardModel, is318car, cardCores, keywords, catKeywords);

                if (candidates.Count == 0) continue;

                // 对全部候选评分
                var scoredList = ScoreAllCandidates(candidates, cardModel, cardCores, keywords, catKeywords, vinModels, vinSeries, is318car);

                // 按车型优先排序
                if (!string.IsNullOrEmpty(vinModels) || !string.IsNullOrEmpty(vinSeries))
                {
                    var vinTypeTokens = (vinModels + " " + vinSeries)
                        .Split(new[] { '/', ' ', ',', '，', '+' }, StringSplitOptions.RemoveEmptyEntries)
                        .Where(t => t.Length >= 2).Distinct().ToList();
                    scoredList = scoredList
                        .OrderByDescending(c => vinTypeTokens.Any(vt => (c.CarType ?? "").Contains(vt, StringComparison.OrdinalIgnoreCase)) ? 1 : 0)
                        .ThenByDescending(c => c.Score)
                        .ToList();
                }
                else
                {
                    scoredList = scoredList.OrderByDescending(c => c.Score).ToList();
                }

                if (scoredList.Count == 0 || scoredList[0].Score == 0) continue;

                card.IsLocalMatched = true;
                card.LocalCandidates = scoredList;
                var best = scoredList[0];
                card.LocalPartId = best.PartId;
                card.LocalPartNo = best.PartNo;
                card.LocalName = best.Name;
                card.LsPrice = best.LsPrice;
                card.PfPrice = best.PfPrice;
                card.StockAmount = best.StockAmount;

                Log.Debug("VIN匹配成功: {Model} → {LocalPartNo} {LocalName} (score={Score}, 候选={Count})",
                    cardModel, best.PartNo, best.Name, best.Score, scoredList.Count);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "VIN本地匹配异常，不影响主流程");
        }
    }

    #region 本地配件缓存

    /// <summary>获取本地配件数据（5分钟缓存）</summary>
    private async Task<List<LocalPartRow>> GetLocalPartsAsync()
    {
        if (_cachedParts != null && DateTime.Now - _cacheTime < CacheDuration)
            return _cachedParts;

        using var db = await _dbFactory.CreateAsync();

        var rows = (await db.QueryAsync<LocalPartRow>(
            @"SELECT d.partid AS PartId, d.partno AS PartNo, d.name AS Name, d.carname AS CarName,
                     d.cartype AS CarType, d.part_tm AS PartTm,
                     ISNULL(SUM(s.amount),0) AS Amount,
                     ISNULL(MAX(CASE WHEN s.lsprice>0 THEN s.lsprice END),0) AS StockLsPrice,
                     ISNULL(MAX(CASE WHEN s.pfprice>0 THEN s.pfprice END),0) AS StockPfPrice
              FROM part_data d
              INNER JOIN part_stock s ON d.partid=s.partid AND ISNULL(s.place,'')<>'废品仓'
              WHERE ISNULL(d.DEL,'0')='0'
              GROUP BY d.partid, d.partno, d.name, d.carname, d.cartype, d.part_tm")).ToList();

        _cachedParts = rows;
        _cacheTime = DateTime.Now;

        Log.Information("VIN本地配件缓存刷新: {Count}条", rows.Count);
        return rows;
    }

    /// <summary>本地配件行（Dapper直接映射，避免dynamic）</summary>
    private class LocalPartRow
    {
        public long PartId { get; set; }
        public string? PartNo { get; set; }
        public string? Name { get; set; }
        public string? CarName { get; set; }
        public string? CarType { get; set; }
        public string? PartTm { get; set; }
        public int Amount { get; set; }
        public decimal StockLsPrice { get; set; }
        public decimal StockPfPrice { get; set; }
    }

    #endregion

    #region 编码过滤

    /// <summary>按编码过滤本地配件（精确/包含/核心编号匹配）</summary>
    private static List<LocalPartRow> FilterByCode(
        List<LocalPartRow> allParts, string cardModel, bool is318car,
        List<string> cardCores, List<string> keywords, List<string> catKeywords)
    {
        return allParts.Where(p =>
        {
            var partno = (p.PartNo ?? "").Trim();
            var partTm = (p.PartTm ?? "").Trim();

            // 精确/包含匹配
            if (!string.IsNullOrEmpty(partno))
            {
                if (string.Equals(partno, cardModel, StringComparison.OrdinalIgnoreCase) ||
                    partno.Contains(cardModel, StringComparison.OrdinalIgnoreCase) ||
                    (!is318car && cardModel.Contains(partno, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
            if (!string.IsNullOrEmpty(partTm))
            {
                if (string.Equals(partTm, cardModel, StringComparison.OrdinalIgnoreCase) ||
                    partTm.Contains(cardModel, StringComparison.OrdinalIgnoreCase) ||
                    (!is318car && cardModel.Contains(partTm, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }

            // 仅318car：核心编号交集匹配 + 名称/类别交叉验证
            if (is318car && cardCores.Count > 0)
            {
                var partCores = ExtractCoreCodes(partno);
                var tmCores = ExtractCoreCodes(partTm);
                bool coreMatch = cardCores.Any(cc => partCores.Contains(cc, StringComparer.OrdinalIgnoreCase))
                              || cardCores.Any(cc => tmCores.Contains(cc, StringComparer.OrdinalIgnoreCase));
                if (coreMatch)
                {
                    var localName = (p.Name ?? "").Trim();
                    bool nameHit = keywords.Any(kw => !string.IsNullOrEmpty(kw) && localName.Contains(kw, StringComparison.OrdinalIgnoreCase))
                                || catKeywords.Any(ck => MatchWithSynonym(ck, localName));
                    return nameHit;
                }
            }

            return false;
        }).ToList();
    }

    #endregion

    #region 评分

    /// <summary>对编码已命中的候选配件全部评分</summary>
    private static List<VinLocalMatch> ScoreAllCandidates(
        List<LocalPartRow> candidates, string cardModel, List<string> cardCores,
        List<string> keywords, List<string> catKeywords,
        string vinModels, string vinSeries, bool is318car)
    {
        var results = new List<VinLocalMatch>();

        foreach (var part in candidates)
        {
            int score = 0;
            string localPartno = part.PartNo ?? "";
            string localName = part.Name ?? "";
            string localCartype = part.CarType ?? "";
            string localPartTm = part.PartTm ?? "";

            // 规则0: 编码精确度加分
            if (string.Equals(localPartno, cardModel, StringComparison.OrdinalIgnoreCase)) score += 50;
            else if (string.Equals(localPartTm, cardModel, StringComparison.OrdinalIgnoreCase)) score += 40;
            else if (localPartno.Contains(cardModel, StringComparison.OrdinalIgnoreCase) ||
                     cardModel.Contains(localPartno, StringComparison.OrdinalIgnoreCase)) score += 20;
            else if (is318car && cardCores.Count > 0)
            {
                var pnCores = ExtractCoreCodes(localPartno);
                var ptCores = ExtractCoreCodes(localPartTm);
                if (cardCores.Any(cc => pnCores.Contains(cc, StringComparer.OrdinalIgnoreCase)) ||
                    cardCores.Any(cc => ptCores.Contains(cc, StringComparer.OrdinalIgnoreCase)))
                    score += 15;
            }

            // 规则1: 名称模糊匹配（同义词扩展，最高80分）
            foreach (var kw in keywords)
            {
                if (string.IsNullOrEmpty(kw)) continue;
                if (localName.Contains(kw, StringComparison.OrdinalIgnoreCase))
                { score += Math.Min(80, 20 * kw.Length); goto nameDone; }
                if (MatchWithSynonym(kw, localName))
                { score += 40; goto nameDone; }
            }
            nameDone:

            // 规则1b: 类别关键词匹配（最高30分）
            foreach (var ck in catKeywords)
            {
                if (MatchWithSynonym(ck, localName))
                { score += 30; break; }
            }

            // 规则2: 车型整词匹配（最高25分）
            if (!string.IsNullOrEmpty(vinModels) || !string.IsNullOrEmpty(vinSeries))
            {
                var vinTypeTokens = (vinModels + " " + vinSeries)
                    .Split(new[] { '/', ' ', ',', '，', '+' }, StringSplitOptions.RemoveEmptyEntries);
                var localTypeTokens = localCartype
                    .Split(new[] { '/', ' ', ',', '，', '+' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var vt in vinTypeTokens.Distinct())
                    if (localTypeTokens.Any(lt => string.Equals(lt, vt, StringComparison.OrdinalIgnoreCase)))
                    { score += 25; goto typeDone; }
                foreach (var vt in vinTypeTokens.Where(t => t.Length >= 2).Distinct())
                    if (localCartype.Contains(vt, StringComparison.OrdinalIgnoreCase))
                    { score += 15; goto typeDone; }
                typeDone:;
            }

            if (score > 0)
            {
                results.Add(new VinLocalMatch
                {
                    PartId = part.PartId,
                    PartNo = localPartno,
                    Name = localName,
                    CarName = part.CarName ?? "",
                    CarType = localCartype,
                    StockAmount = part.Amount,
                    LsPrice = part.StockLsPrice,
                    PfPrice = part.StockPfPrice,
                    Score = score
                });
            }
        }
        return results;
    }

    #endregion

    #region 同义词 & 关键词提取

    /// <summary>配件名称同义词组（同组内任一词匹配均视为匹配）</summary>
    private static readonly string[][] _synonymGroups =
    [
        ["避震器", "减振器", "减震器", "减"],
        ["悬挂", "控制臂", "摆臂"],
        ["衬套", "胶套", "衬胶", "胶垫"],
        ["平衡杆", "稳定杆"],
        ["开口胶", "开口衬套"],
        ["拉杆", "拉臂"],
        ["支臂", "支杆"],
        ["轮毂单元", "轮轴承", "轮毂轴承"],
        ["机脚", "机脚胶", "机脚垫", "减震机脚"],
    ];

    /// <summary>检查关键词是否能通过同义词匹配目标字符串</summary>
    public static bool MatchWithSynonym(string keyword, string target)
    {
        if (target.Contains(keyword, StringComparison.OrdinalIgnoreCase)) return true;
        foreach (var group in _synonymGroups)
        {
            bool kwInGroup = false;
            foreach (var syn in group)
                if (keyword.Contains(syn, StringComparison.OrdinalIgnoreCase) ||
                    syn.Contains(keyword, StringComparison.OrdinalIgnoreCase)) { kwInGroup = true; break; }
            if (!kwInGroup) continue;
            foreach (var syn in group)
                if (target.Contains(syn, StringComparison.OrdinalIgnoreCase)) return true;
            break;
        }
        return false;
    }

    /// <summary>从category中提取配件类型关键词</summary>
    public static List<string> ExtractCategoryKeywords(string? category)
    {
        if (string.IsNullOrEmpty(category)) return [];
        var catKw = new[] { "前减","后减","减振","减震","避震器","顶胶","平面轴承","平衡杆",
            "球头","控制臂","悬挂","半轴","防尘套","衬套","胶套","拉杆","助力泵",
            "弹簧","轴承","摆臂","拉臂","支臂","转向","稳定杆","摇臂","连杆","开口胶",
            "轮毂","机脚","轮胎","螺丝","螺栓"};
        return catKw.Where(ck => category.Contains(ck)).ToList();
    }

    /// <summary>提取配件名称中的核心关键词（去掉编号和品牌）</summary>
    public static List<string> ExtractCoreKeywords(string? name)
    {
        if (string.IsNullOrEmpty(name)) return [];
        var result = new List<string>();
        var brands = new[] { "瀚图", "携豹", "恒稳", "洲龙", "随风", "维德罗", "SOFT", "PM-A",
            "FIRSTOO", "依必艾", "通用品牌", "BMF", "L", "R", "L/R" };
        foreach (var seg in name.Split(new[] { '~', ' ', '-', '（', '）', '(', ')' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = seg.Trim();
            if (trimmed.All(c => c < 128 && char.IsLetterOrDigit(c))) continue;
            if (brands.Any(b => trimmed.Equals(b, StringComparison.OrdinalIgnoreCase))) continue;
            if (trimmed.Length < 2) continue;
            result.Add(trimmed);
        }
        return result;
    }

    /// <summary>
    /// 提取编码中的核心编号候选列表（去前缀），用于跨数据源编码匹配。
    /// 示例：PM-A204→[A204], A076--630001→[A076,630001], ZC A204→[A204]
    /// </summary>
    public static List<string> ExtractCoreCodes(string? code)
    {
        var result = new List<string>();
        if (string.IsNullOrEmpty(code)) return result;
        code = code.Trim();

        // 按空格拆分
        var spaceParts = code.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (spaceParts.Length > 1)
        {
            foreach (var part in spaceParts)
            {
                var trimmed = part.Trim('-', ' ');
                if (trimmed.Any(char.IsDigit))
                    result.Add(trimmed);
            }
            return result.Count > 0 ? result : [spaceParts[^1].Trim('-', ' ')];
        }

        // 按-拆分
        var dashParts = code.Split('-', StringSplitOptions.RemoveEmptyEntries);
        if (dashParts.Length > 1)
        {
            foreach (var part in dashParts)
            {
                var trimmed = part.Trim();
                if (trimmed.Any(char.IsDigit))
                    result.Add(trimmed);
            }
            return result.Count > 0 ? result : [dashParts[^1].Trim()];
        }

        // 无分隔符：去掉开头纯字母前缀
        int firstDigit = -1;
        for (int i = 0; i < code.Length; i++)
        {
            if (char.IsDigit(code[i])) { firstDigit = i; break; }
        }
        if (firstDigit < 0) { result.Add(code); return result; }

        int start = firstDigit;
        if (start > 0 && char.IsLetter(code[start - 1])) start--;
        result.Add(code[start..]);
        return result;
    }

    #endregion
}
