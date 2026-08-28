using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dapper;
using System.Data.SqlClient;
using System.Data.Odbc;
using System.Text.RegularExpressions;

// 本迁移工具刻意使用 System.Data.SqlClient（v4.9）以兼容 SQL Server 2000 源库，抑制其过时警告
#pragma warning disable CS0618

namespace QP11.Services;

/// <summary>
/// 第三方数据迁移服务
/// 负责将第三方软件数据库的数据迁移到目标数据库
/// 源库和目标库连接均由用户输入
/// </summary>
public class MigrationService
{
    private string _sourceConnStr = "";
    private string _targetConnStr = "";

    // ID映射表：存储源ID到目标ID的对应关系
    private readonly Dictionary<string, string> _partIdMapping = new();    // 源nno -> 目标partid
    private readonly Dictionary<string, string> _clientIdMapping = new();  // 源gno -> 目标cid
    private readonly Dictionary<string, string> _supplierIdMapping = new(); // 源gyno -> 目标sid

    // 单据号集合（用于去重）
    private readonly HashSet<string> _buySns = new();
    private readonly HashSet<string> _sellSns = new();

    /// <summary>GB2312编码，用于中文转拼音首字母（依赖宿主进程注册 CodePagesEncodingProvider）</summary>
    private static readonly Encoding Gb2312 = Encoding.GetEncoding("GB2312");

    // GB2312 二级汉字区(56-87区, 0xD8A1-0xF7FE) 共 3008 字的拼音首字母表。
    // 索引方式：区号(high-0xD8)*94 + 位号(low-0xA1)，由 pypinyin 权威注音数据生成。
    // 与 QP11.Wpf/Helpers/PinyinHelper.cs 的 Level2Initials 同源，修改需同步。
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

    /// <summary>迁移进度回调</summary>
    public event Action<string, int, int>? ProgressChanged;

    /// <summary>迁移日志回调</summary>
    public event Action<string>? LogMessage;

    /// <summary>异常回调</summary>
    public event Action<string>? ErrorOccurred;

    private void ReportProgress(string step, int current, int total)
    {
        ProgressChanged?.Invoke(step, current, total);
    }

    private void Log(string msg)
    {
        LogMessage?.Invoke(msg);
    }

    private void LogError(string msg)
    {
        ErrorOccurred?.Invoke(msg);
        Log($"[错误] {msg}");
    }

    #region 数据库连接

    /// <summary>
    /// 测试源库连接（SqlClient，适用于 SQL Server 2005+）。
    /// </summary>
    public bool TestConnection(string server, string database, string user, string password)
    {
        var connStr = BuildConnStr(server, database, user, password);
        try
        {
            using var conn = new SqlConnection(connStr);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            cmd.ExecuteScalar();
            Log("  SqlClient连接成功");
            return true;
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            LogError($"SqlClient连接失败: {msg}");
            return false;
        }
    }

    /// <summary>
    /// 测试目标库连接（ODBC，适用于 SQL Server 2000）。
    /// </summary>
    public bool TestTargetConnection(string server, string database, string user, string password)
    {
        var connStr = BuildOdbcConnStr(server, database, user, password);
        try
        {
            using var conn = new OdbcConnection(connStr);
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT 1";
            cmd.ExecuteScalar();
            Log("  ODBC连接成功");
            return true;
        }
        catch (Exception ex)
        {
            var msg = ex.InnerException?.Message ?? ex.Message;
            LogError($"ODBC连接失败: {msg}");
            return false;
        }
    }

    /// <summary>
    /// 构建SqlClient连接串（用于源库，SQL Server 2005+）
    /// </summary>
    private static string BuildConnStr(string server, string database, string user, string password)
    {
        return $"Server={server};Database={database};User Id={user};Password={password};";
    }

    /// <summary>
    /// 构建ODBC连接串（用于目标库，SQL Server 2000）
    /// </summary>
    private static string BuildOdbcConnStr(string server, string database, string user, string password)
    {
        return $"Driver={{SQL Server}};Server={server};Database={database};Uid={user};Pwd={password};";
    }

    private IDbConnection CreateSourceConnection()
    {
        var conn = new SqlConnection(_sourceConnStr);
        conn.Open();
        return conn;
    }

    private IDbConnection CreateTargetConnection()
    {
        var conn = new OdbcConnection(_targetConnStr);
        conn.Open();
        return conn;
    }

    #endregion

    #region 主迁移流程

    /// <summary>
    /// 执行完整数据迁移
    /// </summary>
    public async Task RunMigration(
        string sourceServer, string sourceDb, string sourceUser, string sourcePwd,
        string targetServer, string targetDb, string targetUser, string targetPwd)
    {
        _sourceConnStr = BuildConnStr(sourceServer, sourceDb, sourceUser, sourcePwd);
        _targetConnStr = BuildOdbcConnStr(targetServer, targetDb, targetUser, targetPwd);

        _partIdMapping.Clear();
        _clientIdMapping.Clear();
        _supplierIdMapping.Clear();
        _buySns.Clear();
        _sellSns.Clear();

        Log("========================================");
        Log("开始数据迁移...");
        Log($"源库: {sourceServer}/{sourceDb}");
        Log($"目标库: {targetServer}/{targetDb}");
        Log("========================================");

        try
        {
            // 第1步：迁移系统设置
            await MigrateSystemSettings();

            // 第2步：迁移基础数据（配件、客户、供应商、仓位）
            await MigrateParts();
            await MigrateClients();
            await MigrateSuppliers();
            await MigratePartPlaces();

            // 第3步：迁移库存
            await MigrateStock();

            // 第4步：迁移业务数据（采购、销售）
            await MigratePurchases();
            await MigrateSales();

            // 第5步：迁移财务数据
            await MigrateFinance();

            Log("========================================");
            Log("数据迁移完成！");
            Log("========================================");
        }
        catch (Exception ex)
        {
            LogError($"迁移过程中发生异常: {ex.Message}");
            throw;
        }
    }

    #endregion

    #region 系统设置迁移

    private async Task MigrateSystemSettings()
    {
        Log("--- 迁移系统设置 ---");
        try
        {
            using var source = CreateSourceConnection();
            using var target = CreateTargetConnection();

            // 1. 迁移 syscontrol -> business_infor
            var syscontrol = await source.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT TOP 1 * FROM syscontrol");
            if (syscontrol != null)
            {
                var exists = await TargetQueryFirstOrDefaultAsync<int>(target,
                    "SELECT COUNT(1) FROM business_infor");
                if (exists == 0)
                {
                    await TargetExecuteAsync(target,
                        @"INSERT INTO business_infor (id, username, qc, jc, linkman, tel, mobile, fax, zip, address, email, tax, bank1, datetime)
                          VALUES (1, @username, @qc, @jc, @linkman, @tel, @mobile, @fax, @zip, @address, @email, @tax, @bank1, GETDATE())",
                        new
                        {
                            username = (string?)syscontrol.c_id ?? "admin",
                            qc = (string?)syscontrol.c_name ?? "",
                            jc = (string?)syscontrol.c_accna ?? "",
                            linkman = "",
                            tel = (string?)syscontrol.tel ?? "",
                            mobile = "",
                            fax = (string?)syscontrol.fax ?? "",
                            zip = "",
                            address = (string?)syscontrol.addr ?? "",
                            email = "",
                            tax = (string?)syscontrol.taxid ?? "",
                            bank1 = (string?)syscontrol.bank ?? ""
                        });
                    Log("  syscontrol -> business_infor: OK");
                }
            }

            // 2. 迁移 tbsysset -> 系统设置表（目标库如有同名表则迁移）
            var syssets = await source.QueryAsync<dynamic>("SELECT * FROM tbsysset");
            var count = 0;
            // 检查目标库是否存在 tbsysset 表
            var tableExists = await CheckTableExists(target, "tbsysset");
            if (tableExists)
            {
                foreach (var row in syssets)
                {
                    var name = (string?)row.name ?? "";
                    var dpm = (string?)row.dpm ?? "";

                    var exists = await TargetQueryFirstOrDefaultAsync<int>(target,
                        "SELECT COUNT(1) FROM tbsysset WHERE name=@name AND dpm=@dpm",
                        new { name, dpm });
                    if (exists > 0) continue;

                    await TargetExecuteAsync(target,
                        @"INSERT INTO tbsysset (name, type, valuec, valuen, valuel, valued, note, dpm, code, code1)
                          VALUES (@name, @type, @valuec, @valuen, @valuel, @valued, @note, @dpm, @code, @code1)",
                        new
                        {
                            name,
                            type = (string?)row.type ?? "",
                            valuec = (string?)row.valuec ?? "",
                            valuen = (decimal?)row.valuen ?? 0,
                            valuel = (bool?)row.valuel ?? false,
                            valued = (DateTime?)row.valued,
                            note = (string?)row.note ?? "",
                            dpm,
                            code = (string?)row.code ?? "",
                            code1 = (string?)row.code1 ?? ""
                        });
                    count++;
                }
                Log($"  tbsysset: 迁移了 {count} 条");
            }
            else
            {
                Log("  tbsysset: 目标库无此表，跳过");
            }
        }
        catch (Exception ex)
        {
            LogError($"系统设置迁移失败: {ex.Message}");
        }
    }

    private static async Task<bool> CheckTableExists(IDbConnection conn, string tableName)
    {
        try
        {
            var result = await TargetQueryFirstOrDefaultAsync<int>(conn,
                "SELECT COUNT(1) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME=@name",
                new { name = tableName });
            return result > 0;
        }
        catch
        {
            return false;
        }
    }

    #endregion

    #region 配件迁移

    private async Task MigrateParts()
    {
        Log("--- 迁移配件数据 ---");
        using var source = CreateSourceConnection();
        using var target = CreateTargetConnection();

        // 预加载 tbisto 仓位映射（按 nno 聚合，多仓位拼接），用于填充 part_data.place
        var stockRows = await source.QueryAsync<dynamic>("SELECT * FROM tbisto");
        var placeByNno = stockRows
            .Where(r => !string.IsNullOrWhiteSpace(((string?)r.nno ?? "").Trim()))
            .GroupBy(r => ((string?)r.nno ?? "").Trim())
            .ToDictionary(
                g => g.Key,
                g => string.Join(",", g
                    .Select(r => ((string?)r.posi ?? "").Trim())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)),
                StringComparer.OrdinalIgnoreCase);

        var parts = await source.QueryAsync<dynamic>(
            "SELECT * FROM tbprnoty WHERE nno IS NOT NULL AND nno != ''");
        var list = parts.ToList();
        int total = list.Count;
        int migrated = 0;
        int skipped = 0;

        for (int i = 0; i < total; i++)
        {
            var row = list[i];
            var nno = ((string?)row.nno ?? "").Trim();
            if (string.IsNullOrEmpty(nno)) continue;

            var existCount = await TargetQueryFirstOrDefaultAsync<int>(target,
                "SELECT COUNT(1) FROM part_data WHERE partno=@partno", new { partno = nno });
            if (existCount > 0)
            {
                skipped++;
                var existPartId = await TargetQueryFirstOrDefaultAsync<long>(target,
                    "SELECT partid FROM part_data WHERE partno=@partno", new { partno = nno });
                _partIdMapping[nno] = existPartId.ToString();
                continue;
            }

            var na1 = ((string?)row.na1 ?? "").Trim();
            var ty = ((string?)row.ty ?? "").Trim();
            var fa = ((string?)row.fa ?? "").Trim();
            var unit = ((string?)row.unit ?? "").Trim();
            var cxnno = ((string?)row.cxnno ?? "").Trim();
            var note = ((string?)row.note1 ?? "").Trim();
            var iprc = (decimal?)(double?)row.iprc ?? 0m;
            var iprj = (decimal?)(double?)row.iprj ?? 0m;
            var oprc = (decimal?)(double?)row.oprc_cp ?? (decimal?)(double?)row.oprc ?? 0m;
            var eng = ((string?)row.eng ?? "").Trim();

            var namePy = GetPinyinInitial(na1);
            var carnamePy = GetPinyinInitial(cxnno);
            var cartypePy = GetPinyinInitial(ty);

            // 从 tbisto 预加载映射获取仓位（多仓位已拼接为 "仓A,仓B"）
            var place = placeByNno.TryGetValue(nno, out var p) ? p : "";

            // part_data.partid 非自增列，需显式生成：取当前最大值+1
            var nextPartId = await TargetQueryFirstOrDefaultAsync<long>(target,
                "SELECT ISNULL(MAX(partid), 0) + 1 FROM part_data");

            await TargetExecuteAsync(target,
                @"INSERT INTO part_data (partid, partno, name, carname, cartype, unit, inprice, pfprice, lsprice, part_th, part_gg, memo, area, place, name_py, carname_py, cartype_py, isck)
                  VALUES (@partid, @partno, @name, @carname, @cartype, @unit, @inprice, @pfprice, @lsprice, @part_th, @part_gg, @memo, @area, @place, @name_py, @carname_py, @cartype_py, 1)",
                new
                {
                    partid = nextPartId,
                    partno = nno,
                    name = na1.Length > 200 ? na1[..200] : na1,
                    carname = cxnno.Length > 200 ? cxnno[..200] : cxnno,
                    cartype = ty.Length > 200 ? ty[..200] : ty,
                    unit = unit.Length > 30 ? unit[..30] : unit,
                    inprice = iprc,
                    pfprice = iprj,
                    lsprice = oprc,
                    part_th = fa.Length > 50 ? fa[..50] : fa,
                    part_gg = "",
                    memo = note.Length > 255 ? note[..255] : note,
                    area = eng.Length > 200 ? eng[..200] : eng,
                    place = place.Length > 60 ? place[..60] : place,
                    name_py = namePy,
                    carname_py = carnamePy,
                    cartype_py = cartypePy
                });

            _partIdMapping[nno] = nextPartId.ToString();
            migrated++;
            ReportProgress("配件数据", i + 1, total);
        }

        Log($"  tbprnoty: 迁移 {migrated} 条, 跳过 {skipped} 条 (共 {total} 条)");
    }

    #endregion

    #region 客户迁移

    private async Task MigrateClients()
    {
        Log("--- 迁移客户数据 ---");
        using var source = CreateSourceConnection();
        using var target = CreateTargetConnection();

        var clients = await source.QueryAsync<dynamic>("SELECT * FROM tbgu");
        var list = clients.ToList();
        int total = list.Count;
        int migrated = 0;
        int skipped = 0;

        for (int i = 0; i < total; i++)
        {
            var row = list[i];
            var gno = ((string?)row.gno ?? "").Trim();
            if (string.IsNullOrEmpty(gno)) continue;

            var exists = await TargetQueryFirstOrDefaultAsync<int>(target,
                "SELECT COUNT(1) FROM client_infor WHERE cid=@cid", new { cid = gno });
            if (exists > 0)
            {
                skipped++;
                _clientIdMapping[gno] = gno;
                continue;
            }

            var gname = ((string?)row.gname ?? "").Trim();
            var namePy = GetPinyinInitial(gname);

            await TargetExecuteAsync(target,
                @"INSERT INTO client_infor (cid, name, address, linkman, tel, fax, mobile, zip, bank, note, name_py, class)
                  VALUES (@cid, @name, @address, @linkman, @tel, @fax, @mobile, @zip, @bank, @note, @name_py, @class)",
                new
                {
                    cid = gno.Length > 30 ? gno[..30] : gno,
                    name = gname.Length > 100 ? gname[..100] : gname,
                    address = ((string?)row.adr ?? "").Trim().Length > 50 ? ((string?)row.adr ?? "").Trim()[..50] : ((string?)row.adr ?? "").Trim(),
                    linkman = ((string?)row.linkman ?? "").Trim().Length > 30 ? ((string?)row.linkman ?? "").Trim()[..30] : ((string?)row.linkman ?? "").Trim(),
                    tel = ((string?)row.tel ?? "").Trim().Length > 30 ? ((string?)row.tel ?? "").Trim()[..30] : ((string?)row.tel ?? "").Trim(),
                    fax = ((string?)row.fax ?? "").Trim().Length > 30 ? ((string?)row.fax ?? "").Trim()[..30] : ((string?)row.fax ?? "").Trim(),
                    mobile = ((string?)row.mobile ?? "").Trim().Length > 30 ? ((string?)row.mobile ?? "").Trim()[..30] : ((string?)row.mobile ?? "").Trim(),
                    zip = ((string?)row.zip ?? "").Trim().Length > 30 ? ((string?)row.zip ?? "").Trim()[..30] : ((string?)row.zip ?? "").Trim(),
                    bank = ((string?)row.bank ?? "").Trim().Length > 30 ? ((string?)row.bank ?? "").Trim()[..30] : ((string?)row.bank ?? "").Trim(),
                    note = ((string?)row.note ?? "").Trim().Length > 200 ? ((string?)row.note ?? "").Trim()[..200] : ((string?)row.note ?? "").Trim(),
                    name_py = namePy,
                    @class = ((string?)row.type1 ?? "").Trim().Length > 10 ? ((string?)row.type1 ?? "").Trim()[..10] : ((string?)row.type1 ?? "").Trim()
                });

            _clientIdMapping[gno] = gno;
            migrated++;
            ReportProgress("客户数据", i + 1, total);
        }

        Log($"  tbgu: 迁移 {migrated} 条, 跳过 {skipped} 条 (共 {total} 条)");
    }

    #endregion

    #region 供应商迁移

    private async Task MigrateSuppliers()
    {
        Log("--- 迁移供应商数据 ---");
        using var source = CreateSourceConnection();
        using var target = CreateTargetConnection();

        // 第1步：从 tbgugys 读取供应商主数据（含地址、电话等详细信息）
        var gysList = (await source.QueryAsync<dynamic>("SELECT * FROM tbgugys")).ToList();
        var gysByGyno = new Dictionary<string, dynamic>(StringComparer.OrdinalIgnoreCase);
        var gysByName = new Dictionary<string, dynamic>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in gysList)
        {
            var gyno = ((string?)row.gyno ?? "").Trim();
            var gyname = ((string?)row.gyname ?? "").Trim();
            if (!string.IsNullOrEmpty(gyno))
                gysByGyno[gyno] = row;
            if (!string.IsNullOrEmpty(gyname))
                gysByName[gyname] = row;
        }

        // 第2步：从 tbistoed 提取去重的供应商（gyno+gyname），这是采购单据实际引用的编号
        var distinctSuppliers = (await source.QueryAsync<dynamic>(
            @"SELECT DISTINCT LTRIM(RTRIM(gyno)) AS gyno, LTRIM(RTRIM(gyname)) AS gyname
              FROM tbistoed
              WHERE cno IS NOT NULL AND cno <> ''")).ToList();

        // 第3步：合并 tbgugys 中 gyno 非空但 tbistoed 中未出现的供应商
        foreach (var gys in gysList)
        {
            var gyno = ((string?)gys.gyno ?? "").Trim();
            if (string.IsNullOrEmpty(gyno)) continue;
            if (!distinctSuppliers.Any(s => ((string?)s.gyno ?? "").Equals(gyno, StringComparison.OrdinalIgnoreCase)))
            {
                distinctSuppliers.Add(new System.Dynamic.ExpandoObject());
                var dict = (IDictionary<string, object?>)distinctSuppliers[^1];
                dict["gyno"] = gyno;
                dict["gyname"] = ((string?)gys.gyname ?? "").Trim();
            }
        }

        int total = distinctSuppliers.Count;
        int migrated = 0;
        int skipped = 0;
        int genCounter = 1;

        for (int i = 0; i < total; i++)
        {
            var row = distinctSuppliers[i];
            var gyno = ((string?)row.gyno ?? "").Trim();
            var gyname = ((string?)row.gyname ?? "").Trim();

            // gyno 为空时，尝试用名称在 tbgugys 中查找对应的详细信息
            if (string.IsNullOrEmpty(gyno))
            {
                if (!string.IsNullOrEmpty(gyname) && gysByName.TryGetValue(gyname, out var gysRow))
                {
                    // tbgugys 中有同名的供应商，用其 gyno（若 tbgugys 的 gyno 也为空则用生成的 sid）
                    gyno = ((string?)gysRow.gyno ?? "").Trim();
                    if (string.IsNullOrEmpty(gyno))
                        gyno = "S" + gysRow.id;
                }
                else if (!string.IsNullOrEmpty(gyname))
                {
                    // gyno 为空且 tbgugys 中无匹配：先检查目标库是否已有同名供应商
                    var existSid = await TargetQueryFirstOrDefaultAsync<string>(target,
                        "SELECT TOP 1 sid FROM supplier_infor WHERE name=@name",
                        new { name = gyname.Length > 100 ? gyname[..100] : gyname });
                    if (existSid != null)
                    {
                        skipped++;
                        continue;
                    }
                    // 生成唯一 sid
                    gyno = "G" + genCounter.ToString();
                    genCounter++;
                }
                else
                {
                    // gyno 和 gyname 都为空，无法迁移
                    skipped++;
                    continue;
                }
            }

            var exists = await TargetQueryFirstOrDefaultAsync<int>(target,
                "SELECT COUNT(1) FROM supplier_infor WHERE sid=@sid", new { sid = gyno });
            if (exists > 0)
            {
                skipped++;
                _supplierIdMapping[gyno] = gyno;
                continue;
            }

            var namePy = GetPinyinInitial(gyname);

            // 从 tbgugys 中查找同 gyno 的详细信息
            string address = "", linkman = "", tel = "", fax = "", mobile = "", zip = "", bank = "", lb = "";
            if (gysByGyno.TryGetValue(gyno, out var gysInfo) ||
                (!string.IsNullOrEmpty(gyname) && gysByName.TryGetValue(gyname, out gysInfo)))
            {
                address = ((string?)gysInfo.adr ?? "").Trim();
                linkman = ((string?)gysInfo.linkman ?? "").Trim();
                tel = ((string?)gysInfo.tel ?? "").Trim();
                fax = ((string?)gysInfo.fax ?? "").Trim();
                mobile = ((string?)gysInfo.mobile ?? "").Trim();
                zip = ((string?)gysInfo.zip ?? "").Trim();
                bank = ((string?)gysInfo.bank ?? "").Trim();
                lb = ((string?)gysInfo.lb ?? "").Trim();
            }

            await TargetExecuteAsync(target,
                @"INSERT INTO supplier_infor (sid, name, address, linkman, tel, fax, mobile, zip, bank, name_py, class)
                  VALUES (@sid, @name, @address, @linkman, @tel, @fax, @mobile, @zip, @bank, @name_py, @class)",
                new
                {
                    sid = gyno.Length > 20 ? gyno[..20] : gyno,
                    name = gyname.Length > 100 ? gyname[..100] : gyname,
                    address = address.Length > 50 ? address[..50] : address,
                    linkman = linkman.Length > 30 ? linkman[..30] : linkman,
                    tel = tel.Length > 30 ? tel[..30] : tel,
                    fax = fax.Length > 20 ? fax[..20] : fax,
                    mobile = mobile.Length > 20 ? mobile[..20] : mobile,
                    zip = zip.Length > 10 ? zip[..10] : zip,
                    bank = bank.Length > 30 ? bank[..30] : bank,
                    name_py = namePy,
                    @class = lb.Length > 10 ? lb[..10] : lb
                });

            _supplierIdMapping[gyno] = gyno;
            migrated++;
            ReportProgress("供应商数据", i + 1, total);
        }

        Log($"  供应商: 迁移 {migrated} 条, 跳过 {skipped} 条 (共 {total} 条)");
    }

    #endregion

    #region 仓位迁移

    private async Task MigratePartPlaces()
    {
        Log("--- 迁移仓位数据 ---");
        using var source = CreateSourceConnection();
        using var target = CreateTargetConnection();

        var places = await source.QueryAsync<dynamic>("SELECT DISTINCT posi FROM tbposi WHERE posi IS NOT NULL AND posi != ''");
        int migrated = 0;

        foreach (var row in places)
        {
            var posi = ((string?)row.posi ?? "").Trim();
            if (string.IsNullOrEmpty(posi)) continue;

            var exists = await TargetQueryFirstOrDefaultAsync<int>(target,
                "SELECT COUNT(1) FROM part_place WHERE place=@place", new { place = posi });
            if (exists > 0) continue;

            await TargetExecuteAsync(target,
                "INSERT INTO part_place (place, place_nm) VALUES (@place, @place_nm)",
                new { place = posi, place_nm = posi });
            migrated++;
        }

        Log($"  tbposi: 迁移 {migrated} 条仓位");
    }

    #endregion

    #region 库存迁移

    private async Task MigrateStock()
    {
        Log("--- 迁移库存数据 ---");
        using var source = CreateSourceConnection();
        using var target = CreateTargetConnection();

        var stocks = await source.QueryAsync<dynamic>(
            "SELECT * FROM tbisto");
        var list = stocks.ToList();
        int total = list.Count;

        // part_stock 主键为 partid 单列（一配件一行），源库同一配件可能有多仓位多行
        // 按 nno 分组：数量求和、仓位拼接为 "仓A,仓B,仓C"（去重去空，保持原顺序）
        var grouped = list
            .Where(r => !string.IsNullOrWhiteSpace(((string?)r.nno ?? "").Trim()))
            .GroupBy(r => ((string?)r.nno ?? "").Trim())
            .Select(g => new
            {
                nno = g.Key,
                // 库存数量求和，但不允许为负（kcamount<=0 的配件也迁移，amount 存为0）
                kcamount = Math.Max(0, g.Sum(r => (long?)(double?)r.kcamount ?? 0)),
                // 多仓位拼接：去空去重，按首次出现顺序，逗号分隔
                posi = string.Join(",", g
                    .Select(r => ((string?)r.posi ?? "").Trim())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .Distinct(StringComparer.OrdinalIgnoreCase)),
                iprj = (decimal?)(double?)g.First().iprj ?? 0m,
                oprc = (decimal?)(double?)g.First().oprc ?? 0m
            })
            .ToList();

        int groupTotal = grouped.Count;
        int migrated = 0;
        int skipped = 0;

        for (int i = 0; i < groupTotal; i++)
        {
            var row = grouped[i];
            var nno = row.nno;

            if (!_partIdMapping.TryGetValue(nno, out var partIdStr))
            {
                var pid = await TargetQueryFirstOrDefaultAsync<long?>(target,
                    "SELECT partid FROM part_data WHERE partno=@partno", new { partno = nno });
                if (pid == null)
                {
                    skipped++;
                    continue;
                }
                partIdStr = pid.Value.ToString();
                _partIdMapping[nno] = partIdStr;
            }

            var partid = long.Parse(partIdStr);

            // 主键 partid 已存在则跳过（重复迁移时不覆盖现有库存）
            var exist = await TargetQueryFirstOrDefaultAsync<int>(target,
                "SELECT COUNT(1) FROM part_stock WHERE partid=@partid",
                new { partid });
            if (exist > 0)
            {
                skipped++;
                continue;
            }

            await TargetExecuteAsync(target,
                @"INSERT INTO part_stock (partid, place, amount, pfprice, lsprice, warning)
                  VALUES (@partid, @place, @amount, @pfprice, @lsprice, 0)",
                new
                {
                    partid,
                    place = row.posi.Length > 20 ? row.posi[..20] : row.posi,
                    amount = row.kcamount,
                    pfprice = row.iprj,
                    lsprice = row.oprc
                });
            migrated++;
            ReportProgress("库存数据", i + 1, groupTotal);
        }

        Log($"  tbisto: 迁移 {migrated} 条 (合并自 {total} 行), 跳过 {skipped} 条 (共 {groupTotal} 个配件)");
    }

    #endregion

    #region 采购迁移

    private async Task MigratePurchases()
    {
        Log("--- 迁移采购入库数据 ---");
        using var source = CreateSourceConnection();
        using var target = CreateTargetConnection();

        var details = await source.QueryAsync<dynamic>(
            "SELECT * FROM tbistoed WHERE cno IS NOT NULL AND cno != '' ORDER BY cno, item");
        var list = details.ToList();

        var groups = list.GroupBy(r => ((string?)r.cno ?? "").Trim()).ToList();
        int migratedBills = 0;
        int migratedDetails = 0;

        for (int g = 0; g < groups.Count; g++)
        {
            var group = groups[g];
            var cno = group.Key;
            if (string.IsNullOrEmpty(cno) || _buySns.Contains(cno)) continue;

            var first = group.First();
            var gyno = ((string?)first.gyno ?? "").Trim();
            var gyname = ((string?)first.gyname ?? "").Trim();
            var per = ((string?)first.per ?? "").Trim();
            var indate = (DateTime?)first.indate;

            // 退货单识别：源库无退货类型字段，通过明细数量全为负数标识整单退货。
            // 退货单 flag=2(BillFlag.Returned), type=2(退货)；正常单 flag=1, type=1。
            var isReturn = group.All(r => ((long?)(double?)r.jkamount ?? 0) < 0);
            var billFlag = isReturn ? 2 : 1;
            var billType = isReturn ? 2 : 1;

            // gyno 为空时，用 gyname 在目标库查找供应商 sid
            if (string.IsNullOrEmpty(gyno) && !string.IsNullOrEmpty(gyname))
            {
                var sidByName = await TargetQueryFirstOrDefaultAsync<string>(target,
                    "SELECT TOP 1 sid FROM supplier_infor WHERE name=@name",
                    new { name = gyname.Length > 100 ? gyname[..100] : gyname });
                if (sidByName != null)
                    gyno = sidByName;
            }

            var sn = GenerateBuySn(cno);

            var existSn = await TargetQueryFirstOrDefaultAsync<int>(target,
                "SELECT COUNT(1) FROM bill_buy WHERE sn=@sn", new { sn });
            if (existSn > 0) continue;

            decimal totalAmount = 0;
            int detailCount = 0;

            using var transaction = target.BeginTransaction();
            try
            {
                await TargetExecuteAsync(target,
                    @"INSERT INTO bill_buy (sn, supplier, worker, operator, memo, flag, type, datetime, total, cash, checks, arrear)
                      VALUES (@sn, @supplier, @worker, @operator, @memo, @flag, @type, @datetime, @total, 0, 0, @total)",
                    new
                    {
                        sn,
                        supplier = gyno.Length > 30 ? gyno[..30] : gyno,
                        worker = per.Length > 20 ? per[..20] : per,
                        @operator = per.Length > 20 ? per[..20] : per,
                        memo = "",
                        flag = billFlag,
                        type = billType,
                        datetime = indate ?? DateTime.Now,
                        total = 0m
                    }, transaction);

                foreach (var row in group)
                {
                    var nno = ((string?)row.nno ?? "").Trim();
                    var na1 = ((string?)row.na1 ?? "").Trim();
                    var fa = ((string?)row.fa ?? "").Trim();
                    var ty = ((string?)row.ty ?? "").Trim();
                    var cxnno = ((string?)row.cxnno ?? "").Trim();
                    var unit = ((string?)row.unit ?? "").Trim();
                    var jkamount = (long?)(double?)row.jkamount ?? 0;
                    var iprc = (decimal?)(double?)row.iprc ?? 0m;
                    var iprj = (decimal?)(double?)row.iprj ?? 0m;
                    var oprc = (decimal?)(double?)row.oprc ?? 0m;
                    var posi = ((string?)row.posi ?? "").Trim();

                    var lineTotal = iprc * jkamount;
                    totalAmount += lineTotal;

                    long? partid = null;
                    if (_partIdMapping.TryGetValue(nno, out var pidStr))
                        partid = long.Parse(pidStr);
                    else
                    {
                        partid = await TargetQueryFirstOrDefaultAsync<long?>(target,
                            "SELECT partid FROM part_data WHERE partno=@partno", new { partno = nno }, transaction);
                        if (partid != null)
                            _partIdMapping[nno] = partid.Value.ToString();
                    }

                    await TargetExecuteAsync(target,
                        @"INSERT INTO detail_buy (sn, partid, partno, name, amount, unit, carname, cartype, inprice, intotal, pfprice, lsprice, place, memo, type, datetime, part_th, part_gg)
                          VALUES (@sn, @partid, @partno, @name, @amount, @unit, @carname, @cartype, @inprice, @intotal, @pfprice, @lsprice, @place, @memo, @type, @datetime, @part_th, @part_gg)",
                        new
                        {
                            sn,
                            partid = partid ?? 0,
                            partno = nno.Length > 200 ? nno[..200] : nno,
                            name = na1.Length > 200 ? na1[..200] : na1,
                            amount = jkamount,
                            unit = unit.Length > 10 ? unit[..10] : unit,
                            carname = cxnno.Length > 200 ? cxnno[..200] : cxnno,
                            cartype = ty.Length > 200 ? ty[..200] : ty,
                            inprice = iprc,
                            intotal = lineTotal,
                            pfprice = iprj,
                            lsprice = oprc,
                            place = posi.Length > 30 ? posi[..30] : posi,
                            memo = "",
                            type = billType,
                            datetime = indate ?? DateTime.Now,
                            part_th = fa.Length > 50 ? fa[..50] : fa,
                            part_gg = ""
                        }, transaction);

                    detailCount++;
                }

                await TargetExecuteAsync(target,
                    "UPDATE bill_buy SET total=@total, arrear=@total WHERE sn=@sn",
                    new { sn, total = totalAmount }, transaction);

                transaction.Commit();
                _buySns.Add(cno);
                migratedBills++;
                migratedDetails += detailCount;
            }
            catch (Exception ex)
            {
                LogError($"采购单 {sn} 迁移失败: {ex.Message}");
                try { transaction.Rollback(); }
                catch { /* ODBC 驱动在 SQL 错误后可能已自动回滚事务 */ }
                throw;
            }

            ReportProgress("采购入库", g + 1, groups.Count);
        }

        Log($"  tbistoed: 迁移 {migratedBills} 张采购单, {migratedDetails} 条明细");
    }

    #endregion

    #region 销售迁移

    private async Task MigrateSales()
    {
        Log("--- 迁移销售出库数据 ---");
        using var source = CreateSourceConnection();
        using var target = CreateTargetConnection();

        var details = await source.QueryAsync<dynamic>(
            "SELECT * FROM tbsada WHERE paper IS NOT NULL AND paper != '' ORDER BY paper, xno");
        var list = details.ToList();

        var groups = list.GroupBy(r => ((string?)r.paper ?? "").Trim()).ToList();
        int migratedBills = 0;
        int migratedDetails = 0;

        for (int g = 0; g < groups.Count; g++)
        {
            var group = groups[g];
            var paper = group.Key;
            if (string.IsNullOrEmpty(paper) || _sellSns.Contains(paper)) continue;

            var first = group.First();
            var gno = ((string?)first.gno ?? "").Trim();
            var per = ((string?)first.per ?? "").Trim();
            var outdate = (DateTime?)first.outdate;

            // 退货单识别：源库无退货类型字段，通过明细数量全为负数标识整单退货。
            // 退货单 flag=2(BillFlag.Returned), type=2(退货)；正常单 flag=1, type=1。
            var isReturn = group.All(r => ((long?)(double?)r.ckamount ?? 0) < 0);
            var billFlag = isReturn ? 2 : 1;
            var billType = isReturn ? 2 : 1;

            var sn = GenerateSellSn(paper);

            var existSn = await TargetQueryFirstOrDefaultAsync<int>(target,
                "SELECT COUNT(1) FROM bill_sell WHERE sn=@sn", new { sn });
            if (existSn > 0) continue;

            decimal totalAmount = 0;
            int detailCount = 0;

            using var transaction = target.BeginTransaction();
            try
            {
                await TargetExecuteAsync(target,
                    @"INSERT INTO bill_sell (sn, client, worker, operator, total, bill_total, discount_rate, total_payment, bill_payment, cash, collection, checks, arrear, memo, flag, type, datetime)
                      VALUES (@sn, @client, @worker, @operator, @total, @total, 1, @total, @total, 0, 0, 0, @total, '', @flag, @type, @datetime)",
                    new
                    {
                        sn,
                        client = gno.Length > 20 ? gno[..20] : gno,
                        worker = per.Length > 20 ? per[..20] : per,
                        @operator = per.Length > 20 ? per[..20] : per,
                        total = 0m,
                        flag = billFlag,
                        type = billType,
                        datetime = outdate ?? DateTime.Now
                    }, transaction);

                foreach (var row in group)
                {
                    var nno = ((string?)row.nno ?? "").Trim();
                    var na1 = ((string?)row.na1 ?? "").Trim();
                    var fa = ((string?)row.fa ?? "").Trim();
                    var ty = ((string?)row.ty ?? "").Trim();
                    var unit = ((string?)row.unit ?? "").Trim();
                    var ckamount = (long?)(double?)row.ckamount ?? 0;
                    var oprct = (decimal?)(double?)row.oprct ?? 0m;
                    var iprc = (decimal?)(double?)row.iprc ?? 0m;
                    var posi = ((string?)row.posi ?? "").Trim();

                    // 退货明细：通过 ypaper 字段（原销售单号）设置 tsn，关联到原销售单
                    // 源库 tbsada.ypaper 存储了原销售单号，迁移时转换为目标库 sn 格式
                    string tsn = "";
                    if (isReturn)
                    {
                        var ypaper = ((string?)row.ypaper ?? "").Trim();
                        if (!string.IsNullOrEmpty(ypaper))
                            tsn = GenerateSellSn(ypaper);
                    }

                    var lineTotal = oprct * ckamount;
                    totalAmount += lineTotal;

                    long? partid = null;
                    if (_partIdMapping.TryGetValue(nno, out var pidStr))
                        partid = long.Parse(pidStr);
                    else
                    {
                        partid = await TargetQueryFirstOrDefaultAsync<long?>(target,
                            "SELECT partid FROM part_data WHERE partno=@partno", new { partno = nno }, transaction);
                        if (partid != null)
                            _partIdMapping[nno] = partid.Value.ToString();
                    }

                    await TargetExecuteAsync(target,
                        @"INSERT INTO detail_sell (sn, partid, partno, name, amount, unit, cartype, price, bill_price, stotal, btotal, place, memo, type, flag, datetime, cb, tsn, part_th, part_gg)
                          VALUES (@sn, @partid, @partno, @name, @amount, @unit, @cartype, @price, @price, @stotal, @stotal, @place, @memo, @type, @flag, @datetime, @cb, @tsn, @part_th, @part_gg)",
                        new
                        {
                            sn,
                            partid = partid ?? 0,
                            partno = nno.Length > 200 ? nno[..200] : nno,
                            name = na1.Length > 200 ? na1[..200] : na1,
                            amount = ckamount,
                            unit = unit.Length > 10 ? unit[..10] : unit,
                            cartype = ty.Length > 200 ? ty[..200] : ty,
                            price = oprct,
                            stotal = lineTotal,
                            place = posi.Length > 50 ? posi[..50] : posi,
                            memo = "",
                            flag = billFlag,
                            type = billType,
                            datetime = outdate ?? DateTime.Now,
                            cb = iprc,
                            tsn = tsn.Length > 15 ? tsn[..15] : tsn,
                            part_th = fa.Length > 50 ? fa[..50] : fa,
                            part_gg = ""
                        }, transaction);

                    detailCount++;
                }

                await TargetExecuteAsync(target,
                    "UPDATE bill_sell SET total=@total, bill_total=@total, total_payment=@total, bill_payment=@total, arrear=@total WHERE sn=@sn",
                    new { sn, total = totalAmount }, transaction);

                transaction.Commit();
                _sellSns.Add(paper);
                migratedBills++;
                migratedDetails += detailCount;
            }
            catch (Exception ex)
            {
                LogError($"销售单 {sn} 迁移失败: {ex.Message}");
                try { transaction.Rollback(); }
                catch { /* ODBC 驱动在 SQL 错误后可能已自动回滚事务 */ }
                throw;
            }

            ReportProgress("销售出库", g + 1, groups.Count);
        }

        Log($"  tbsada: 迁移 {migratedBills} 张销售单, {migratedDetails} 条明细");
    }

    #endregion

    #region 财务迁移

    private async Task MigrateFinance()
    {
        Log("--- 迁移财务数据 ---");
        using var source = CreateSourceConnection();
        using var target = CreateTargetConnection();

        // 财务三表为流水表，无业务唯一键，迁移前清空避免重复
        await TargetExecuteAsync(target, "DELETE FROM arrearage");
        await TargetExecuteAsync(target, "DELETE FROM pays");
        await TargetExecuteAsync(target, "DELETE FROM account");
        Log("  已清空 arrearage/pays/account 三表");

        // 1. 迁移应收应付（tbysyf -> arrearage）
        var yfList = await source.QueryAsync<dynamic>("SELECT * FROM tbysyf");
        var yfArr = yfList.ToList();
        int arrearMigrated = 0;

        for (int i = 0; i < yfArr.Count; i++)
        {
            var row = yfArr[i];
            var gno = ((string?)row.gno ?? "").Trim();
            var ys = (decimal?)(double?)row.ys ?? 0m;
            var yf = (decimal?)(double?)row.yf ?? 0m;
            var datime = (DateTime?)row.datime;
            var per = ((string?)row.per ?? "").Trim();

            if (ys > 0)
            {
                await TargetExecuteAsync(target,
                    @"INSERT INTO arrearage (bid, sn, total, charge, operator, type, btype, datetime)
                      VALUES (@bid, @sn, @total, @charge, @operator, 2, 2, @datetime)",
                    new
                    {
                        bid = gno.Length > 30 ? gno[..30] : gno,
                        sn = "",
                        total = ys,
                        charge = ys,
                        @operator = per.Length > 20 ? per[..20] : per,
                        datetime = datime ?? DateTime.Now
                    });
                arrearMigrated++;
            }

            if (yf > 0)
            {
                await TargetExecuteAsync(target,
                    @"INSERT INTO arrearage (bid, sn, total, charge, operator, type, btype, datetime)
                      VALUES (@bid, @sn, @total, @charge, @operator, 1, 1, @datetime)",
                    new
                    {
                        bid = gno.Length > 30 ? gno[..30] : gno,
                        sn = "",
                        total = yf,
                        charge = yf,
                        @operator = per.Length > 20 ? per[..20] : per,
                        datetime = datime ?? DateTime.Now
                    });
                arrearMigrated++;
            }

            ReportProgress("应收应付", i + 1, yfArr.Count);
        }
        Log($"  tbysyf -> arrearage: 迁移 {arrearMigrated} 条");

        // 2. 迁移付款/收款记录（tbgath -> pays）
        var gathList = await source.QueryAsync<dynamic>("SELECT * FROM tbgath WHERE amt IS NOT NULL AND amt != 0");
        var gathArr = gathList.ToList();
        int paysMigrated = 0;

        for (int i = 0; i < gathArr.Count; i++)
        {
            var row = gathArr[i];
            var amt = (decimal?)(double?)row.amt ?? 0m;
            var gdate = (DateTime?)row.gdate;
            var per = ((string?)row.per ?? "").Trim();
            var cno = ((string?)row.cno ?? "").Trim();
            var type = ((string?)row.type ?? "").Trim();

            int btype = type == "销售" ? 2 : 1;

            await TargetExecuteAsync(target,
                @"INSERT INTO pays (bid, sn, pay, operator, flag, btype, datetime)
                  VALUES (@bid, @sn, @pay, @operator, 1, @btype, @datetime)",
                new
                {
                    bid = cno.Length > 20 ? cno[..20] : cno,
                    sn = cno.Length > 20 ? cno[..20] : cno,
                    pay = amt,
                    @operator = per.Length > 20 ? per[..20] : per,
                    btype,
                    datetime = gdate ?? DateTime.Now
                });
            paysMigrated++;

            ReportProgress("付款记录", i + 1, gathArr.Count);
        }
        Log($"  tbgath -> pays: 迁移 {paysMigrated} 条");

        // 3. 迁移账户流水（tblsz -> account）
        var lszList = await source.QueryAsync<dynamic>("SELECT * FROM tblsz");
        var lszArr = lszList.ToList();
        int accountMigrated = 0;

        for (int i = 0; i < lszArr.Count; i++)
        {
            var row = lszArr[i];
            var ys = (decimal?)(double?)row.ys ?? 0m;
            var yf = (decimal?)(double?)row.yf ?? 0m;
            var lsDate = (DateTime?)row.ls_date;
            var gname = ((string?)row.gname ?? "").Trim();
            var paper = ((string?)row.paper ?? "").Trim();
            var ytu = ((string?)row.ytu ?? "").Trim();

            if (ys > 0)
            {
                await TargetExecuteAsync(target,
                    @"INSERT INTO account (name, sn, charge, type, operator, flag, btype, memo, datetime)
                      VALUES (@name, @sn, @charge, @type, @operator, 1, 2, @memo, @datetime)",
                    new
                    {
                        name = gname.Length > 50 ? gname[..50] : gname,
                        sn = paper.Length > 20 ? paper[..20] : paper,
                        charge = ys,
                        type = "收入",
                        @operator = "",
                        memo = ytu.Length > 100 ? ytu[..100] : ytu,
                        datetime = lsDate ?? DateTime.Now
                    });
                accountMigrated++;
            }

            if (yf > 0)
            {
                await TargetExecuteAsync(target,
                    @"INSERT INTO account (name, sn, charge, type, operator, flag, btype, memo, datetime)
                      VALUES (@name, @sn, @charge, @type, @operator, 1, 1, @memo, @datetime)",
                    new
                    {
                        name = gname.Length > 50 ? gname[..50] : gname,
                        sn = paper.Length > 20 ? paper[..20] : paper,
                        charge = -yf,
                        type = "支出",
                        @operator = "",
                        memo = ytu.Length > 100 ? ytu[..100] : ytu,
                        datetime = lsDate ?? DateTime.Now
                    });
                accountMigrated++;
            }

            ReportProgress("账户流水", i + 1, lszArr.Count);
        }
        Log($"  tblsz -> account: 迁移 {accountMigrated} 条");
    }

    #endregion

    #region ODBC参数转换辅助

    private static readonly Regex _paramRegex = new(@"@(?!@)(\w+)", RegexOptions.Compiled);

    /// <summary>
    /// 将命名参数SQL（@param）转换为ODBC位置参数SQL（?），并按出现顺序提取参数值。
    /// SQL Server 2000的ODBC驱动不支持@命名参数，必须使用?占位符。
    /// </summary>
    private static (string odbcSql, OdbcParameter[] parameters) ConvertToOdbcParameters(string sql, object? param)
    {
        if (param == null)
            return (sql, Array.Empty<OdbcParameter>());

        var props = param.GetType().GetProperties();
        var propDict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in props)
            propDict[p.Name] = p.GetValue(param);

        var paramList = new List<OdbcParameter>();
        var odbcSql = _paramRegex.Replace(sql, match =>
        {
            var name = match.Groups[1].Value;
            if (propDict.TryGetValue(name, out var value))
            {
                var p = new OdbcParameter();
                // ODBC驱动对bool类型支持不佳，转换为整数0/1
                if (value is bool b)
                    p.Value = b ? 1 : 0;
                else
                    p.Value = value ?? DBNull.Value;
                paramList.Add(p);
                return "?";
            }
            return match.Value;
        });

        return (odbcSql, paramList.ToArray());
    }

    /// <summary>
    /// 在目标库（ODBC）上执行INSERT/UPDATE/DELETE，使用?位置参数。
    /// </summary>
    private static async Task<int> TargetExecuteAsync(IDbConnection conn, string sql, object? param = null, IDbTransaction? transaction = null)
    {
        var odbcConn = (OdbcConnection)conn;
        var (odbcSql, parameters) = ConvertToOdbcParameters(sql, param);
        using var cmd = odbcConn.CreateCommand();
        cmd.CommandText = odbcSql;
        if (transaction != null)
            cmd.Transaction = (OdbcTransaction)transaction;
        foreach (var p in parameters)
            cmd.Parameters.Add(p);
        return await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// 在目标库（ODBC）上查询首行首列值，使用?位置参数。
    /// </summary>
    private static async Task<T> TargetQueryFirstOrDefaultAsync<T>(IDbConnection conn, string sql, object? param = null, IDbTransaction? transaction = null)
    {
        var odbcConn = (OdbcConnection)conn;
        var (odbcSql, parameters) = ConvertToOdbcParameters(sql, param);
        using var cmd = odbcConn.CreateCommand();
        cmd.CommandText = odbcSql;
        if (transaction != null)
            cmd.Transaction = (OdbcTransaction)transaction;
        foreach (var p in parameters)
            cmd.Parameters.Add(p);
        var result = await cmd.ExecuteScalarAsync();
        if (result == null || result == DBNull.Value)
            return default!;
        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        return (T)Convert.ChangeType(result, targetType);
    }

    #endregion

    #region 辅助方法

    public static int GetEstimatedTotalSteps()
    {
        return 9; // 系统设置 + 配件 + 客户 + 供应商 + 仓位 + 库存 + 采购 + 销售 + 财务
    }

    private string GenerateBuySn(string sourceSn)
    {
        var clean = sourceSn.Replace("-", "").Replace(" ", "").Replace("/", "");
        return "MIG" + (clean.Length > 12 ? clean[..12] : clean.PadRight(12, '0'));
    }

    private string GenerateSellSn(string sourcePaper)
    {
        var clean = sourcePaper.Replace("-", "").Replace(" ", "").Replace("/", "");
        return "MIS" + (clean.Length > 12 ? clean[..12] : clean.PadRight(12, '0'));
    }

    /// <summary>
    /// 获取中文文本的拼音首字母（简化版）
    /// </summary>
    private static string GetPinyinInitial(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var result = "";
        foreach (var c in text)
        {
            if (c >= 0x4e00 && c <= 0x9fff)
            {
                result += GetChineseInitial(c);
            }
            else if (char.IsLetter(c))
            {
                result += char.ToUpper(c);
            }
            else if (char.IsDigit(c))
            {
                // 保留数字（如车型 W164 中的 164），否则拼音码会丢失关键信息
                result += c;
            }
        }
        return result.Length > 30 ? result[..30] : result;
    }

    private static char GetChineseInitial(char ch)
    {
        // GB2312 编码范围 0xB0A1-0xD7F9 对应拼音首字母 A-Z
        // 必须将 Unicode 字符转为 GB2312 字节再比较，否则中文 Unicode 码点(0x4e00-0x9fff)无法匹配
        var bytes = Gb2312.GetBytes(ch.ToString());
        if (bytes.Length < 2) return '?';
        int code = (bytes[0] << 8) + bytes[1];
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
        if (bytes[0] >= 0xD8 && bytes[0] <= 0xF7 && bytes[1] >= 0xA1 && bytes[1] <= 0xFE)
        {
            int index = (bytes[0] - 0xD8) * 94 + (bytes[1] - 0xA1);
            if (index < Level2Initials.Length) return Level2Initials[index];
        }
        return '?';
    }

    #endregion
}

#pragma warning restore CS0618