using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using QP11.Core.Interfaces;

namespace QP11.Wpf.Views;

/// <summary>单据类型</summary>
public enum OrderType { Sell, Buy }

/// <summary>明细项（用于 DataGrid 绑定）</summary>
public class OrderDetailItem
{
    public string? Partno { get; set; }
    public string? Name { get; set; }
    public string? Cartype { get; set; }
    public decimal? Amount { get; set; }
    public decimal? Price { get; set; }
    public decimal? CostPrice { get; set; }  // 销售单=成本cb, 采购单=进价inprice
    public decimal? Subtotal { get; set; }
}

/// <summary>只读单据详情查看窗口</summary>
public partial class OrderDetailWindow : Window
{
    private readonly string _sn;
    private readonly OrderType _orderType;

    public OrderDetailWindow(string sn, OrderType orderType)
    {
        InitializeComponent();
        _sn = sn;
        _orderType = orderType;

        Title = orderType == OrderType.Sell ? $"销售单详情 - {sn}" : $"采购单详情 - {sn}";
        txtTitle.Text = Title;

        Loaded += (_, _) =>
        {
            if (_orderType == OrderType.Sell)
                LoadSellOrder(sn);
            else
                LoadBuyOrder(sn);
        };
    }

    private async void LoadSellOrder(string sn)
    {
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();

            // 查询销售单头信息
            // flag=3 为报损单，无真实客户，显示"配件报损"（与销售历史列表保持一致）
            var headSql = @"SELECT sn,
                            CASE WHEN ISNULL(bill_sell.flag, 0) = 3 THEN '配件报损' ELSE client_infor.name END AS ClientName,
                            worker, [operator], checkno,
                            total, bill_total, discount_rate,
                            cash, collection, checks, arrear,
                            zhifubao, weixin, yunfei, memo, datetime
                           FROM bill_sell
                           LEFT JOIN client_infor ON client_infor.cid = bill_sell.client
                           WHERE bill_sell.sn = @Sn";
            var head = await db.QueryFirstOrDefaultAsync<dynamic>(headSql, new { Sn = sn });

            if (head != null)
            {
                txtSn.Text = head.sn?.ToString() ?? sn;
                txtDate.Text = ((DateTime)head.datetime).ToString("yyyy-MM-dd HH:mm:ss");
                txtPartner.Text = head.ClientName?.ToString() ?? "";
                txtWorker.Text = head.worker?.ToString() ?? "";
                txtOperator.Text = head.operator_?.ToString() ?? "";
                txtDiscountRate.Text = head.discount_rate != null
                    ? ((decimal)head.discount_rate).ToString() : "0";
                txtBillTotal.Text = head.bill_total != null
                    ? ((decimal)head.bill_total).ToString("N2") : "0.00";

                // 收款情况汇总
                var parts = new List<string>();
                if (head.cash != null && (decimal)head.cash > 0)
                    parts.Add($"现金:{(decimal)head.cash:N2}");
                if (head.checks != null && (decimal)head.checks > 0)
                    parts.Add($"支票:{(decimal)head.checks:N2}");
                if (head.arrear != null && (decimal)head.arrear > 0)
                    parts.Add($"欠款:{(decimal)head.arrear:N2}");
                if (head.zhifubao != null && (decimal)head.zhifubao > 0)
                    parts.Add($"支付宝:{(decimal)head.zhifubao:N2}");
                if (head.weixin != null && (decimal)head.weixin > 0)
                    parts.Add($"微信:{(decimal)head.weixin:N2}");
                if (head.yunfei != null && (decimal)head.yunfei > 0)
                    parts.Add($"运费:{(decimal)head.yunfei:N2}");
                txtPaymentInfo.Text = parts.Count > 0 ? string.Join("  ", parts) : "无";

                var memo = head.memo?.ToString();
                if (!string.IsNullOrEmpty(memo))
                {
                    lblMemoLabel.Visibility = Visibility.Visible;
                    txtMemo.Visibility = Visibility.Visible;
                    txtMemo.Text = memo;
                }
            }

            // 查询销售明细
            var detailSql = @"SELECT partno, name, cartype, amount,
                              ISNULL(price, 0) AS Price,
                              ISNULL(cb, 0) AS CostPrice,
                              ISNULL(stotal, amount * price) AS Subtotal
                             FROM detail_sell WHERE sn = @Sn ORDER BY id";
            var details = await db.QueryAsync<OrderDetailItem>(detailSql, new { Sn = sn });
            dgDetails.ItemsSource = details;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载销售单失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void LoadBuyOrder(string sn)
    {
        try
        {
            var dbFactory = App.ServiceProvider.GetRequiredService<IDbConnectionFactory>();
            using var db = await dbFactory.CreateAsync();

            // 查询采购单头信息
            var headSql = @"SELECT sn, supplier_infor.name AS SupplierName,
                            worker, [operator], invoice,
                            total, cash, checks, arrear,
                            zhifubao, weixin, yunfei, memo, datetime
                           FROM bill_buy
                           LEFT JOIN supplier_infor ON supplier_infor.sid = bill_buy.supplier
                           WHERE bill_buy.sn = @Sn";
            var head = await db.QueryFirstOrDefaultAsync<dynamic>(headSql, new { Sn = sn });

            if (head != null)
            {
                txtSn.Text = head.sn?.ToString() ?? sn;
                txtDate.Text = ((DateTime)head.datetime).ToString("yyyy-MM-dd HH:mm:ss");
                txtPartner.Text = head.SupplierName?.ToString() ?? "";
                txtWorker.Text = head.worker?.ToString() ?? "";
                txtOperator.Text = head.operator_?.ToString() ?? "";

                // 采购单无折扣率，隐藏该行或显示"-"
                txtDiscountRate.Text = "-";
                txtBillTotal.Text = head.total != null
                    ? ((decimal)head.total).ToString("N2") : "0.00";

                // 付款情况汇总
                var parts = new List<string>();
                if (head.cash != null && (decimal)head.cash > 0)
                    parts.Add($"现金:{(decimal)head.cash:N2}");
                if (head.checks != null && (decimal)head.checks > 0)
                    parts.Add($"支票:{(decimal)head.checks:N2}");
                if (head.arrear != null && (decimal)head.arrear > 0)
                    parts.Add($"欠款:{(decimal)head.arrear:N2}");
                if (head.zhifubao != null && (decimal)head.zhifubao > 0)
                    parts.Add($"支付宝:{(decimal)head.zhifubao:N2}");
                if (head.weixin != null && (decimal)head.weixin > 0)
                    parts.Add($"微信:{(decimal)head.weixin:N2}");
                if (head.yunfei != null && (decimal)head.yunfei > 0)
                    parts.Add($"运费:{(decimal)head.yunfei:N2}");
                txtPaymentInfo.Text = parts.Count > 0 ? string.Join("  ", parts) : "无";

                var invoice = head.invoice?.ToString();
                if (!string.IsNullOrEmpty(invoice))
                {
                    lblMemoLabel.Text = "发票号:";
                    lblMemoLabel.Visibility = Visibility.Visible;
                    txtMemo.Visibility = Visibility.Visible;
                    txtMemo.Text = invoice;
                }

                var memo = head.memo?.ToString();
                if (!string.IsNullOrEmpty(memo))
                {
                    lblMemoLabel.Text = "备注:";
                    lblMemoLabel.Visibility = Visibility.Visible;
                    txtMemo.Visibility = Visibility.Visible;
                    txtMemo.Text = memo;
                }
            }

            // 查询采购明细（detail_buy 无 price/stotal 列，只有 inprice）
            var detailSql = @"SELECT partno, name, cartype, amount,
                              inprice AS Price,
                              inprice AS CostPrice,
                              (amount * inprice) AS Subtotal
                             FROM detail_buy WHERE sn = @Sn ORDER BY id";
            var details = await db.QueryAsync<OrderDetailItem>(detailSql, new { Sn = sn });
            dgDetails.ItemsSource = details;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"加载采购单失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
        base.OnKeyDown(e);
    }
}
