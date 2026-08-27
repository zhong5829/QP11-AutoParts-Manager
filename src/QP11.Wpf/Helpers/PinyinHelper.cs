using System.Collections.Concurrent;
using System.Text;

namespace QP11.Wpf.Helpers
{
    public static class PinyinHelper
    {
        private static readonly Encoding Gb2312 = Encoding.GetEncoding("GB2312");
        private static readonly ConcurrentDictionary<string, string> _cache = new();

        // GB2312 二级汉字区(56-87区, 0xD8A1-0xF7FE) 共 3008 字的拼音首字母表。
        // 索引方式：区号(high-0xD8)*94 + 位号(low-0xA1)，由 pypinyin 权威注音数据生成。
        // 旧实现仅覆盖一级汉字区(0xB0A1-0xD7F9)，二级汉字(如"鑫"0xF6CE)被丢弃导致拼音码错误。
        // 注意：QP11.Services/MigrationService.cs 的 GetChineseInitial 有同源副本，修改此处需同步。
        private static readonly string Level2Initials =
                "CJWGNSPGCGNEGYPBTYYZDXYKYGTZJNMJQMBSGZSCYJSYYFPGKBZGYDYWJKGKLJSWKPJQHYJWRDZLSGMRYPYWWCCKZNKYYG" +
                "TTNGJEYKKZYTCJNMCYLQLYPYQFQRPZSLWBTGKJFYXJWZLTBNCXJJJJTXDTTSQZYCDXXHGCKBPHFFSSTYBGMXLPBYLLBHLX" +
                "SMZMYJHSOJNGHDZQYKLGJHSGQZHXQGKEZZWYSCSCJXYEYXADZPMDSSMZJZQJYZCJJFWQJBDZBXGZNZCPWHKXHQKMWFBPBY" +
                "DTJZZKQHYLYGXFPTYJYYZPSZLFCHMQSHGMXXSXJYQDCSBBQBEFSJYHWWGZKPYLQBGLDLCCTNMAYDDKSSNGYCSGXLYZAYPN" +
                "PTSDKDYLHGYMYLCXPYCJNDQJWQQXFYYFJLEJPZRXCCQWQQSBZKYMGPLBMJRQCFLNYMYQMSQTRBCJTHZTQFRXQHXMJJCJLX" +
                "XGJMSHZKBSWYEMYLTXFSYDSGLYCJQXSJNQBSCTYHBFTDCYJDJWYGHQFRXWCKQKXEBPTLPXJZSRMEBWHJLBJSLYYSMDXLCL" +
                "QKXLHXJRZJMFQHXHWYWSBHTRXXGLHQHFNMGYKLDYXZPYLGGSMTCFPAJJZYLJTYANJGBJPLQGDZYQYAXBKYSECJSZNSLYZH" +
                "ZXLZCGHPXZHZNYTDSBCJKDLZYYFWYDLEBBGQYZKGGLDNDNYSKJSHDLYXBCGHXYPKDJMMZNGMMCLGWZSZXZJFZNMLZZTHCS" +
                "YDBDLLSCDDNLKJYKJSYCJLKOHQASDKNHCSGANHDAASHTCPLCPQYBSDMPJLPCJOQLCDHJJYSPRCHNWJNLHLYYQYHWZPTCZG" +
                "WWMZFFJQQQQYXACLBHKDJXDGMMYDJXZLLSYGXGKJRYWZWYCLZMSSJZLDBYDCPCXYHLXCHYZJQSQQAGMNYXPFRKSSBJLYXY" +
                "SYGLNSCMHCWWMNZJJLXXHCHSYZSTTXRYCYXBYHCSMXJSZNPWGPXXTAYBGAJCXLYXDCCWZOCWKCCSBNHCPDYZNFCYYTYCKX" +
                "KYBSQKKYTQQXFCWCHCYKELZQBSQYJQCCLMTHSYWHMKTLKJLYCXWHEQQHTQHQPQSQSCFYMMDMGBWHWLGSLLYSTLMLXPTHMJ" +
                "HWLJZYHZJXHTXJLHXRSWLWZJCBXMHZQXSDZPMGFCSGLSXYMJSHXPJXWMYQKSMYPLRTHBXFTPMHYXLCHLHLZYLXGSSSSTCL" +
                "SLDCLRPBHZHXYYFHBMGDMYCNQQWLQHJJCYWJZYEJJDHPBLQXTQKWHLCHQXAGTLXLJXMSLJHTZKZJECXJCJNMFBYCSFYWYB" +
                "JZGNYSDZSQYRSLJPCLPWXSDWEJBJCBCNAYTWGMPAPCLYQPCLZXSBNMSGGFNZJJBZSFZYNDXHPLQKZCZWALSBCCJXSYZGWK" +
                "YPSGXFZFCDKHJGXTLQFSGDSLQWZKXTMHSBGZMJZRGLYJBPMLMSXLZJQQHZYJCZYDJWBWJKLDDPMJEGXYHYLXHLQYQHKYCW" +
                "CJMYYXNATJHYCCXZPCQLBZWWYTWBQCMLPMYRJCCCXFPZNZZLJPLXXYZTZLGDLDCKLYRZZGQTGJHHGJLJAXFGFJZSLCFDQZ" +
                "LCLGJDJZSNZLLJPJQDCCLCJXMYZFTSXGCGSBRZXJQQCTZHGYQTJQQLZXJYLYLBCYAMCSTYLPDJBYREGKLZYZHLYSZQLZNW" +
                "CZCLLWJQJJJKDGJZOLBBZPPGLGHTGZXYJHZMYCNQCYCYHBHGXKAMTXYXNBSKYZZGJZLQJDFCJXDYGJQJJPMGWGJJJPKQSB" +
                "GBMMCJSSCLPQPDXCDYYKYPCJDDYYGYWRHJRTGZNYQLDKLJSZZGZQZJGDYKSHPZMTLCPWNJYFYZDJCNMWESCYGLBTZCGMSS" +
                "LLYXYSXSBSJSBBSGGHFJLYPMZJNLYYWDQSHZXTYYWHMCYHYWDBXBTLMSYYYFSXJCBDXXLHJHFSSXZQHFZMZCZTQCXZXRTT" +
                "DJHNNYZQQMTQDMMGYYDXMJGDHCDYZBFFALLZTDLTFXMXQZDNGWQDBDCDJDXBZGSQQDDJCMBKZFFXMKDMDSYYSZCMLJDSYN" +
                "SPRSKMKMPCKLGTBQTFZSWTFGGLYPLLJZHGJJGYPZLTCSMCNBTJBQFKTHBYZGKPBBYMTDSSXTBNPDKLEYCJNYDDYKZDDHQH" +
                "SDZSCTARLLTKZLGECLLKJLQJAQNBDKKGHPJTZQKSECSHALQFMMGJNLYJBBTMLYZXDCJPLDLPCQDHZYCBZSCZBZMSLJFLKR" +
                "ZJSNFRGJHXPDHYJYBZGDLQCSEZGXLBLGYXTWMABCHECMWYJYZLLJJYHLGNDJLSLYGKDZPZXJYYZLWCXSZFGWYYDLYHCLJS" +
                "CMBJHBLYZLYCBLYDPDQYSXQZBYTDKYXJYYCNRJMPDJGKLCLJBCTBJDDBBLBLCZQRPPXJCJLZCSHLTOLJNMDDDLNGKATHQH" +
                "JHYKHEZNMSHRPHQQJCHGMFPRXHJGDYCHGHLYRZQLCYQJNZSQTKQJYMSZSWLCFQQQXYFGGYPTQWLMCRNFKKFSYYLQBMQAMM" +
                "MYXCTPSHCPTXXZZSMPHPSHMCLMLDQFYQXSZYJDJJZZHQPDSZGLSTJBCKBXYQZYSGPSXQZQZRQTBDKYXZKHHGFLBCSMDLDG" +
                "DZDBLZYYCXNNCSYBZBFGLZZXSWMSCCMQNJQSBDQSJTXXMBLTXZCLZSHZCXRQJGJYLXZFJPHYMZQQYDFQJJLZZNZJSDGZYG" +
                "CTXMZYSCTLKPHTXHTLBJXJLXSCDQXCBBTJFQZFSLTJBTKQBXXJJLJCHCZDBZJDCZJDCPRNPQCJPFCZLCLZXZDMXMPHJSGZ" +
                "GSZZQJYLWTJPFSYAXMCJBTZKYCWMYTZSJJLQCQLWZMALBXYFBPNLSFHTGJWEJJXXGLLJSTGSHJQLZFKCGNNDSZFDEQFHBS" +
                "AQTGYLBXMMYGSZLDYDQMJJRGBJTKGDHGKBLQKBDMBYLXWCXYTTYBKMRTJZXQJBHLMHMJJZMQASLDCYXYQDLQCAFYWYXQHZ";

        public static string GetPinyinInitials(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return _cache.GetOrAdd(text, ComputePinyin);
        }

        private static string ComputePinyin(string text)
        {
            var result = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                if (c >= 'a' && c <= 'z') result.Append(c);
                else if (c >= 'A' && c <= 'Z') result.Append(char.ToLower(c));
                else if (c >= '0' && c <= '9') result.Append(c);
                else if (c >= 0x4e00 && c <= 0x9fff)
                {
                    var initial = GetChinesePinyinInitial(c);
                    if (initial.HasValue) result.Append(char.ToLower(initial.Value));
                }
            }
            return result.ToString();
        }

        /// <summary>
        /// 获取单个汉字的拼音首字母(大写)。一级汉字(0xB0A1-0xD7F9)用码点区间，
        /// 二级汉字(0xD8A1-0xF7FE)查 Level2Initials 表；无法识别返回 null。
        /// </summary>
        private static char? GetChinesePinyinInitial(char c)
        {
            var bytes = Gb2312.GetBytes(c.ToString());
            if (bytes.Length < 2) return null;
            int high = bytes[0];
            int low = bytes[1];
            int code = (high << 8) + low;
            // 一级汉字区 0xB0A1-0xD7F9（按拼音排序，码点区间）
            if (code >= 0xB0A1 && code <= 0xB0C4) return 'A';
            if (code >= 0xB0C5 && code <= 0xB2C0) return 'B';
            if (code >= 0xB2C1 && code <= 0xB4ED) return 'C';
            if (code >= 0xB4EE && code <= 0xB6E9) return 'D';
            if (code >= 0xB6EA && code <= 0xB7A1) return 'E';
            if (code >= 0xB7A2 && code <= 0xB8C0) return 'F';
            if (code >= 0xB8C1 && code <= 0xB9FD) return 'G';
            if (code >= 0xB9FE && code <= 0xBBF6) return 'H';
            if (code >= 0xBBF7 && code <= 0xBFA5) return 'J';
            if (code >= 0xBFA6 && code <= 0xC0AB) return 'K';
            if (code >= 0xC0AC && code <= 0xC2E7) return 'L';
            if (code >= 0xC2E8 && code <= 0xC4C2) return 'M';
            if (code >= 0xC4C3 && code <= 0xC5B5) return 'N';
            if (code >= 0xC5B6 && code <= 0xC5BD) return 'O';
            if (code >= 0xC5BE && code <= 0xC6D9) return 'P';
            if (code >= 0xC6DA && code <= 0xC8BA) return 'Q';
            if (code >= 0xC8BB && code <= 0xC8F5) return 'R';
            if (code >= 0xC8F6 && code <= 0xCBF0) return 'S';
            if (code >= 0xCBF1 && code <= 0xCDD9) return 'T';
            if (code >= 0xCDDA && code <= 0xCEF3) return 'W';
            if (code >= 0xCEF4 && code <= 0xD1B8) return 'X';
            if (code >= 0xD1B9 && code <= 0xD4D0) return 'Y';
            if (code >= 0xD4D1 && code <= 0xD7F9) return 'Z';
            // 二级汉字区 0xD8A1-0xF7FE（按部首排序，查表）
            if (high >= 0xD8 && high <= 0xF7 && low >= 0xA1 && low <= 0xFE)
            {
                int index = (high - 0xD8) * 94 + (low - 0xA1);
                if (index < Level2Initials.Length) return Level2Initials[index];
            }
            return null;
        }

        /// <summary>清理缓存（内存紧张时调用）</summary>
        public static void ClearCache() => _cache.Clear();
    }
}
