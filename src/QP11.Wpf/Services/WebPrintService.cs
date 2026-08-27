using QP11.Core.Interfaces;
using Dapper;
using Microsoft.Extensions.DependencyInjection;

namespace QP11.Wpf.Services;

/// <summary>
/// 供 WebApi 调用的静默打印服务
/// WebApi 通过反射调用 SilentPrintBill(string sn) 即可完成静默打印
/// </summary>
public static class WebPrintService
{
    /// <summary>
    /// 静默打印销售单（无预览窗口，直接输出到默认打印机）
    /// </summary>
    public static async Task<string?> SilentPrintBill(string sn)
    {
        try
        {
            var sellRepo = App.ServiceProvider.GetRequiredService<ISellRepository>();
            var bill = await sellRepo.GetBySnAsync(sn);
            if (bill == null) return "单据不存在";

            var details = (await sellRepo.GetDetailsAsync(sn)).ToList();

            // 查询客户名
            string clientName = "";
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();
            var cRow = await db.QueryFirstOrDefaultAsync<dynamic>(
                "SELECT name FROM client_infor WHERE cid = @Id", new { Id = bill.Client });
            if (cRow != null) clientName = cRow.name?.ToString() ?? "";

            // 构建打印数据（对齐桌面端 BtnPrintBill_Click）
            var billData = new Views.BillPrintData
            {
                BillType = "销售",
                Sn = bill.Sn,
                DateText = bill.Datetime?.ToString("yyyy-MM-dd") ?? "",
                PartnerName = clientName,
                WorkerName = bill.Worker ?? "",
                TotalAmount = bill.Total ?? 0,
                Cash = bill.Cash ?? 0,
                Weixin = bill.Weixin ?? 0,
                Zhifubao = bill.Zhifubao ?? 0,
                Arrearage = bill.Arrear ?? 0,
                Memo = bill.Memo ?? "",
                DeliveryMethod = "自提"
            };
            await billData.LoadCompanyInfoAsync();

            int idx = 1;
            foreach (var d in details)
            {
                billData.Items.Add(new Views.BillPrintItem
                {
                    Index = idx++,
                    PartNo = d.Partno,
                    PartName = d.Name,
                    Cartype = d.Cartype,
                    Unit = d.Unit,
                    Price = d.Price ?? 0,
                    PfPrice = 0,
                    BillPrice = d.BillPrice ?? 0,
                    Amount = (int)(d.Amount ?? 0),
                    Subtotal = d.Stotal ?? 0,
                    Place = d.Place,
                    Area = d.Area ?? "",
                    Brand = "",
                    DiscountRate = d.DiscountRate ?? 0,
                    Memo = d.Memo
                });
            }

            // 构建 FlowDocument（复用桌面端打印格式）
            var settings = PrintSettingsService.Load();
            var columns = settings.BillPrint.GetColumns("销售");
            var doc = Views.BillDocumentBuilder.Build(billData, columns, settings);

            // 静默打印：通过 PrintQueue + XpsDocumentWriter 直接输出到打印机
            var pd = ((System.Windows.Documents.IDocumentPaginatorSource)doc).DocumentPaginator;
            using var queue = new System.Printing.LocalPrintServer().DefaultPrintQueue;
            var writer = System.Printing.PrintQueue.CreateXpsDocumentWriter(queue);
            writer.Write(pd);

            return null; // null 表示成功
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }
}
