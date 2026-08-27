using System.Collections.Generic;
using System.Linq;

namespace QP11.Services.AI.Tools;

/// <summary>
/// 配件术语别名扩展器：根据数据库 part_data 表实际数据模式做口语化适配。
/// 核心映射规则：
/// 1. 左右方向：用户说"右"→ 数据库存"R"，"左"→"L"
/// 2. 变速箱：用户说"自动"→"AT"，"手动"→"MT"，"无级"→"CVT"
/// 3. 同义词："球笼"="球头"，"机脚"="机脚胶"，"减震器"数据库只存"减"或"减总成"
/// 4. 拼音首字母："bz"→"半轴"
/// </summary>
public static class PartTermExpander
{
    /// <summary>
    /// 同义词映射：用户口语 → 数据库实际名称
    /// 按键长度降序排列，优先匹配更长的词
    /// </summary>
    private static readonly (string alias, string[] targets)[] SynonymMap = new[]
    {
        // === 半轴 === 数据库存 "半轴总成R" "半轴总成L" "半轴总成AT/R" "半轴总成MT/L"
        ("右半轴", new[] { "半轴总成R", "半轴总成 R" }),
        ("左半轴", new[] { "半轴总成L", "半轴总成 L" }),
        ("半轴总成右", new[] { "半轴总成R", "半轴总成 R" }),
        ("半轴总成左", new[] { "半轴总成L", "半轴总成 L" }),
        ("后半轴", new[] { "后半轴总成" }),
        ("半轴", new[] { "半轴总成" }),

        // === 减震器 === 数据库存 "前减" "前减R" "前减总成R" "后减" "后减总成"
        // 数据库里不写"减震器"，直接写"减"或"减总成"
        ("右前减震器", new[] { "前减R", "前减总成R" }),
        ("左前减震器", new[] { "前减L", "前减总成L" }),
        ("右后减震器", new[] { "后减R", "后减总成R" }),
        ("左后减震器", new[] { "后减L", "后减总成L" }),
        ("前减震器", new[] { "前减" }),
        ("后减震器", new[] { "后减" }),
        ("前减震", new[] { "前减" }),
        ("后减震", new[] { "后减" }),
        ("减震器", new[] { "减" }),
        ("减振器", new[] { "减" }),
        ("右前减", new[] { "前减R", "前减总成R" }),
        ("左前减", new[] { "前减L", "前减总成L" }),
        ("右后减", new[] { "后减R", "后减总成R" }),
        ("左后减", new[] { "后减L", "后减总成L" }),

        // === 摆臂 === 数据库存 "下摆臂R" "下摆臂L" "上摆臂R"
        ("右下摆臂", new[] { "下摆臂R" }),
        ("左下摆臂", new[] { "下摆臂L" }),
        ("右上摆臂", new[] { "上摆臂R" }),
        ("左上摆臂", new[] { "上摆臂L" }),
        ("下摆臂右", new[] { "下摆臂R" }),
        ("下摆臂左", new[] { "下摆臂L" }),
        ("摆臂", new[] { "下摆臂", "上摆臂" }),

        // === 球头/球笼 === 数据库存 "外球头" "内球头" "下球头" "平衡杆球头"
        ("外球笼", new[] { "外球头" }),
        ("内球笼", new[] { "内球头" }),
        ("球笼", new[] { "球头" }),
        ("方向机球头", new[] { "拉杆球头", "横拉杆球头" }),

        // === 机脚 === 数据库存 "上机脚胶" "下机脚胶"
        ("上机脚", new[] { "上机脚胶" }),
        ("下机脚", new[] { "下机脚胶" }),
        ("机脚垫", new[] { "机脚胶" }),
        ("机脚", new[] { "机脚胶" }),
        ("右机脚", new[] { "机脚胶R" }),
        ("左机脚", new[] { "机脚胶L" }),

        // === 刹车/制动 ===
        ("刹车片", new[] { "制动片" }),
        ("刹车盘", new[] { "制动盘" }),
        ("刹车总泵", new[] { "制动总泵" }),
        ("刹车分泵", new[] { "制动分泵" }),
        ("制动片", new[] { "刹车片" }),

        // === 方向机/转向 ===
        ("方向机", new[] { "转向机", "转向器" }),
        ("方向机拉杆", new[] { "横拉杆" }),
        ("转向拉杆", new[] { "横拉杆" }),

        // === 离合 ===
        ("离合片", new[] { "离合器片" }),
        ("离合总泵", new[] { "离合器总泵" }),
        ("离合分泵", new[] { "离合器分泵" }),
        ("离合压盘", new[] { "离合器压盘" }),

        // === 顶胶/平面轴承 === 数据库存 "平面轴承" "前顶胶"
        ("顶胶", new[] { "平面轴承", "前顶胶" }),

        // === 防尘套 ===
        ("半轴防尘套", new[] { "半轴防尘套-内", "半轴防尘套-外" }),

        // === 滤芯 ===
        ("机滤", new[] { "机油滤芯", "机油滤清器" }),
        ("汽滤", new[] { "汽油滤芯", "汽油滤清器" }),
        ("空滤", new[] { "空气滤芯", "空气滤清器" }),
        ("空调滤", new[] { "空调滤芯", "空调滤清器" }),

        // === 水箱 ===
        ("水箱", new[] { "散热器" }),

        // === 其他 ===
        ("电瓶", new[] { "蓄电池" }),
        ("涨紧轮", new[] { "张紧轮" }),
        ("发电机", new[] { "交流发电机" }),
        ("起动机", new[] { "启动马达" }),
        ("雨刮", new[] { "雨刷" }),
        ("三元催化", new[] { "催化转换器", "三元催化器" }),
    };

    /// <summary>
    /// 方向映射：用户说"右/左"，数据库存"R/L"
    /// </summary>
    private static readonly (string spoken, string code)[] DirectionMap = new[]
    {
        ("右", "R"), ("左", "L"),
        ("前右", "前R"), ("前左", "前L"),
        ("后右", "后R"), ("后左", "后L"),
    };

    /// <summary>
    /// 变速箱映射：用户说"自动/手动/无级"，数据库存"AT/MT/CVT"
    /// </summary>
    private static readonly (string spoken, string code)[] GearboxMap = new[]
    {
        ("自动挡", "AT"), ("自动", "AT"),
        ("手动挡", "MT"), ("手动", "MT"),
        ("无级变速", "CVT"), ("无级", "CVT"),
    };

    /// <summary>
    /// 拼音首字母 → 中文关键词映射
    /// </summary>
    private static readonly (string pinyin, string chinese)[] PinyinMap = new[]
    {
        ("bz", "半轴"), ("bc", "保险杠"), ("bxt", "蓄电池"),
        ("cy", "刹车"), ("cyp", "刹车片"), ("clp", "离合片"),
        ("dfj", "发电机"), ("dsl", "电子扇"),
        ("fxj", "方向机"), ("fxc", "防尘套"),
        ("jzq", "减震器"), ("jq", "减"), ("jl", "机滤"),
        ("lhp", "离合片"), ("lg", "拉杆"),
        ("pg", "排气管"), ("qdj", "起动机"),
        ("qt", "球头"), ("srq", "散热器"),
        ("sgb", "上摆臂"), ("xgb", "下摆臂"),
        ("wg", "万向节"), ("xl", "空滤"),
        ("yg", "雨刮"), ("ybz", "半轴总成R"), ("ybp", "半轴总成R"),
        ("zbz", "半轴总成L"), ("zbp", "半轴总成L"),
        ("zpj", "转向机"), ("zjq", "张紧轮"),
        ("dj", "减"), ("qtj", "前减"), ("htj", "后减"),
    };

    /// <summary>
    /// 从关键词中展开所有搜索词（原始词 + 同义词 + 方向 + 变速箱 + 拼音展开）
    /// </summary>
    public static List<string> Expand(string keyword)
    {
        var terms = new List<string> { keyword };
        var expanded = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { keyword };

        // 1. 同义词展开
        foreach (var (alias, targets) in SynonymMap)
        {
            if (keyword.IndexOf(alias, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                foreach (var t in targets)
                {
                    var replaced = keyword.Replace(alias, t);
                    if (expanded.Add(replaced))
                        terms.Add(replaced);
                    if (expanded.Add(t))
                        terms.Add(t);
                }
            }
        }

        // 2. 方向展开："右半轴" → "半轴R" "半轴总成R"
        foreach (var (spoken, code) in DirectionMap)
        {
            if (keyword.Contains(spoken))
            {
                var replaced = keyword.Replace(spoken, code);
                if (expanded.Add(replaced))
                    terms.Add(replaced);
            }
        }

        // 3. 变速箱展开："自动半轴" → "AT半轴" "半轴AT"
        foreach (var (spoken, code) in GearboxMap)
        {
            if (keyword.Contains(spoken))
            {
                var replaced = keyword.Replace(spoken, code);
                if (expanded.Add(replaced))
                    terms.Add(replaced);
            }
        }

        // 4. 拼音首字母展开
        foreach (var (pinyin, chinese) in PinyinMap)
        {
            if (string.Equals(keyword, pinyin, StringComparison.OrdinalIgnoreCase))
            {
                if (expanded.Add(chinese))
                    terms.Add(chinese);
            }
            if (keyword.IndexOf(pinyin, StringComparison.OrdinalIgnoreCase) >= 0 && keyword.Length > pinyin.Length)
            {
                var replaced = keyword.Replace(pinyin, chinese);
                if (expanded.Add(replaced))
                    terms.Add(replaced);
            }
        }

        return terms;
    }

    /// <summary>
    /// 判断关键词是否可能是拼音首字母
    /// </summary>
    public static bool IsPinyinInitial(string keyword)
    {
        if (string.IsNullOrEmpty(keyword) || keyword.Length < 2 || keyword.Length > 6)
            return false;
        return keyword.All(c => c >= 'a' && c <= 'z');
    }
}
