using System;
using System.Collections.Generic;
using System.Linq;
using QP11.Core.Entities;
using QP11.Core.Exceptions;
using QP11.Core.Interfaces;
using QP11.Core.Models;

namespace QP11.Services;

public class CalcService : ICalcService
{
    /// <summary>
    /// 计算行小计。discountRate 表示支付比例：1.0 为全价，0.7 为七折（即支付 70%）
    /// </summary>
    public decimal CalculateLineSubtotal(decimal price, decimal amount, decimal discountRate)
    {
        if (amount <= 0) throw new ArgumentException("数量必须大于0");
        if (discountRate <= 0 || discountRate > 1) throw new ArgumentException("支付比例必须在0-1之间（1.0=全价，0.7=七折）");
        return Math.Round(price * amount * discountRate, 2);
    }

    /// <summary>
    /// 计算销售订单汇总。orderDiscountRate 表示支付比例：1.0 为全价，0.7 为七折（即支付 70%）
    /// </summary>
    public SellOrderSummary CalculateSellOrderSummary(IEnumerable<DetailSell> details, decimal orderDiscountRate, decimal yunfei = 0)
    {
        var originalTotal = details.Sum(d => (d.BillPrice ?? 0m) * (d.Amount ?? 0));
        var discountedTotal = Math.Round(originalTotal * orderDiscountRate, 2);
        var totalPayment = discountedTotal + yunfei;

        return new SellOrderSummary
        {
            OriginalTotal = originalTotal,
            DiscountedTotal = discountedTotal,
            Yunfei = yunfei,
            TotalPayment = totalPayment
        };
    }

    public decimal CalculateArrear(decimal totalPayment, PaymentInfo payment)
    {
        var paidAmount = payment.Cash + payment.Checks + payment.CardPay + payment.Zhifubao + payment.Weixin;
        var arrear = totalPayment - paidAmount;
        if (arrear < 0) arrear = 0;
        return Math.Round(arrear, 2);
    }

    public void ValidateDiscountRate(ClientInfor client, decimal requestedDiscount)
    {
        client.ValidateDiscount(requestedDiscount);
    }
}
