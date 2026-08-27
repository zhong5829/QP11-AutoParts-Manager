using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace QP11.Wpf.Services;

public class PrintColumnConfig
{
    public string Key { get; set; } = "";
    public string Header { get; set; } = "";
    public double Width { get; set; } = 80;
    public bool Visible { get; set; } = true;
    public int Order { get; set; } = 0;
    public string? DataField { get; set; }
    public string? Format { get; set; }
    public string? Alignment { get; set; } = "Left";

    public PrintColumnConfig Clone() => new()
    {
        Key = Key, Header = Header, Width = Width, Visible = Visible,
        Order = Order, DataField = DataField, Format = Format, Alignment = Alignment
    };
}

public class PrintSettings
{
    public BillPrintSettings BillPrint { get; set; } = new();
    public PagePrintSettings PagePrint { get; set; } = new();
}

public class BillPrintSettings
{
    public int PrintStyle { get; set; } = 1;
    public double PrintHeight1 { get; set; } = 85;
    public double PrintHeight2 { get; set; } = 80;
    public double PrintHeight3 { get; set; } = 80;
    public double PrintTop { get; set; } = 12;
    public double PrintBottom { get; set; } = 12;

    // 特别声明文字（空则不显示）
    public string Declaration { get; set; } = "品牌只对配件本身质量负责，不附带任何连带赔偿责任，原厂件装车后不予退货，拿货请仔细阅读该事项!";

    // 广告/宣传文字（抬头下方显示，加粗加大）
    public string AdText { get; set; } = "电话：{Phone}    地址：{Address}";

    public List<PrintColumnConfig> SellColumns { get; set; } = DefaultColumns();
    public List<PrintColumnConfig> BuyColumns { get; set; } = DefaultColumns();
    public List<PrintColumnConfig> ReturnColumns { get; set; } = DefaultColumns();

    // 旧字段兼容
    public string PrintItems { get; set; } = "";

    public List<PrintColumnConfig> GetColumns(string billType) => billType switch
    {
        "采购" => BuyColumns,
        "退货" => ReturnColumns,
        _ => SellColumns
    };

    public void SetColumns(string billType, List<PrintColumnConfig> columns)
    {
        switch (billType)
        {
            case "采购": BuyColumns = columns; break;
            case "退货": ReturnColumns = columns; break;
            default: SellColumns = columns; break;
        }
    }

    public static List<PrintColumnConfig> DefaultColumns() => new()
    {
        new() { Key="index",     Header="序号",     Width=40,  Visible=true,  Order=0,  DataField="Index",       Alignment="Center" },
        new() { Key="partno",    Header="零件编码", Width=100,  Visible=true,  Order=1,  DataField="PartNo",      Alignment="Left" },
        new() { Key="name",      Header="零件名",   Width=170, Visible=true,  Order=2,  DataField="PartName",    Alignment="Left" },
        new() { Key="cartype",   Header="车型",     Width=130, Visible=true,  Order=3,  DataField="Cartype",     Alignment="Left" },
        new() { Key="amount",    Header="出库数",   Width=50,  Visible=true,  Order=4,  DataField="Amount",      Alignment="Center" },
        new() { Key="price",     Header="售价",     Width=65,  Visible=true,  Order=5,  DataField="Price",       Alignment="Right", Format="N2" },
        new() { Key="subtotal",  Header="金额",     Width=70,  Visible=true,  Order=6,  DataField="Subtotal",    Alignment="Right", Format="N2" },
        new() { Key="place",     Header="仓位",     Width=38,  Visible=true,  Order=7,  DataField="Place",       Alignment="Left" },
        new() { Key="unit",      Header="单位",     Width=35,  Visible=true,  Order=8,  DataField="Unit",        Alignment="Center" },
        new() { Key="pfprice",   Header="批发价",   Width=48,  Visible=false, Order=9,  DataField="PfPrice",     Alignment="Right", Format="N2" },
        new() { Key="billprice", Header="不含税单价",Width=56,  Visible=false, Order=10, DataField="BillPrice",   Alignment="Right", Format="N2" },
        new() { Key="area",      Header="库位",     Width=38,  Visible=false, Order=11, DataField="Area",        Alignment="Left" },
        new() { Key="brand",     Header="品牌",     Width=80,  Visible=true,  Order=12, DataField="Brand",       Alignment="Left" },
        new() { Key="discount",  Header="折扣",     Width=36,  Visible=false, Order=13, DataField="DiscountRate",Alignment="Right" },
        new() { Key="memo",      Header="备注",     Width=50,  Visible=true,  Order=14, DataField="Memo",        Alignment="Left" },
    };
}

public class PagePrintSettings
{
    public string? PrinterName { get; set; }
    public string PaperSize { get; set; } = "241x93";
    public string Orientation { get; set; } = "Portrait";
    public double MarginTop { get; set; } = 5;
    public double MarginBottom { get; set; } = 5;
    public double MarginLeft { get; set; } = 8;
    public double MarginRight { get; set; } = 8;
    public int Copies { get; set; } = 1;
    public bool Color { get; set; } = false;
}

public static class PrintSettingsService
{
    private static readonly string SettingsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "printsettings.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public static PrintSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var settings = JsonSerializer.Deserialize<PrintSettings>(json, JsonOptions) ?? new PrintSettings();
                MigrateIfNeeded(settings);
                return settings;
            }
        }
        catch { }
        return new PrintSettings();
    }

    public static void Save(PrintSettings settings)
    {
        try
        {
            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(SettingsPath, json);
        }
        catch { }
    }

    private static void MigrateIfNeeded(PrintSettings settings)
    {
        var bill = settings.BillPrint;
        // 如果新字段为空但旧字段有值，进行迁移
        if ((bill.SellColumns == null || bill.SellColumns.Count == 0) &&
            !string.IsNullOrEmpty(bill.PrintItems))
        {
            var oldItems = bill.PrintItems.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => int.TryParse(s.Trim(), out var n) ? n : 0)
                .Where(n => n >= 1 && n <= 17)
                .ToHashSet();

            if (oldItems.Count > 0)
            {
                bill.SellColumns = MigrateFromOldItems(oldItems);
                bill.BuyColumns = MigrateFromOldItems(oldItems);
                bill.ReturnColumns = MigrateFromOldItems(oldItems);
            }
            else
            {
                bill.SellColumns = BillPrintSettings.DefaultColumns();
                bill.BuyColumns = BillPrintSettings.DefaultColumns();
                bill.ReturnColumns = BillPrintSettings.DefaultColumns();
            }
        }
        else
        {
            bill.SellColumns ??= BillPrintSettings.DefaultColumns();
            bill.BuyColumns ??= BillPrintSettings.DefaultColumns();
            bill.ReturnColumns ??= BillPrintSettings.DefaultColumns();
        }
    }

    private static List<PrintColumnConfig> MigrateFromOldItems(HashSet<int> oldItems)
    {
        var columns = BillPrintSettings.DefaultColumns();
        // 旧映射: 1=编号, 2=零售价, 3=批发价, 4=单位, 5=仓位, 7=库位, 8=备注, 9=品牌, 14=不含税单价, 17=折扣
        var oldToKey = new Dictionary<int, string>
        {
            { 1, "partno" }, { 2, "price" }, { 3, "pfprice" }, { 4, "unit" },
            { 5, "place" }, { 7, "area" }, { 8, "memo" }, { 9, "brand" },
            { 14, "billprice" }, { 17, "discount" }
        };
        foreach (var col in columns)
        {
            // 序号/名称/车型/数量/金额 始终显示
            if (col.Key is "index" or "name" or "cartype" or "amount" or "subtotal")
                continue;
            // 根据旧设置决定可见性
            var entry = oldToKey.FirstOrDefault(kv => kv.Value == col.Key);
            if (entry.Key > 0)
                col.Visible = oldItems.Contains(entry.Key);
        }
        return columns;
    }
}
