using QP11.Core.Entities;
using QP11.Core.Exceptions;
using QP11.Core.Models;
using QP11.Services;

namespace QP11.Tests.Services;

public class CalcServiceTests
{
    private readonly CalcService _sut = new();

    [Fact]
    public void CalculateLineSubtotal_FullPrice_ReturnsPriceTimesAmount()
    {
        var result = _sut.CalculateLineSubtotal(100m, 3m, 1.0m);
        Assert.Equal(300m, result);
    }

    [Fact]
    public void CalculateLineSubtotal_SeventyPercent_ReturnsDiscountedTotal()
    {
        var result = _sut.CalculateLineSubtotal(100m, 2m, 0.7m);
        Assert.Equal(140m, result);
    }

    [Fact]
    public void CalculateLineSubtotal_ZeroAmount_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _sut.CalculateLineSubtotal(100m, 0m, 1.0m));
    }

    [Fact]
    public void CalculateLineSubtotal_InvalidDiscount_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _sut.CalculateLineSubtotal(100m, 1m, 1.5m));
    }

    [Fact]
    public void CalculateSellOrderSummary_FullDiscount_ReturnsOriginalTotal()
    {
        var details = new List<DetailSell>
        {
            new() { BillPrice = 100, Amount = 2 },
            new() { BillPrice = 50, Amount = 1 }
        };

        var result = _sut.CalculateSellOrderSummary(details, orderDiscountRate: 1.0m);

        Assert.Equal(250m, result.OriginalTotal);
        Assert.Equal(250m, result.DiscountedTotal);
        Assert.Equal(0m, result.Yunfei);
        Assert.Equal(250m, result.TotalPayment);
    }

    [Fact]
    public void CalculateSellOrderSummary_WithDiscountAndYunfei_ReturnsCorrectTotals()
    {
        var details = new List<DetailSell>
        {
            new() { BillPrice = 200, Amount = 1 }
        };

        var result = _sut.CalculateSellOrderSummary(details, orderDiscountRate: 0.8m, yunfei: 15m);

        Assert.Equal(200m, result.OriginalTotal);
        Assert.Equal(160m, result.DiscountedTotal);
        Assert.Equal(15m, result.Yunfei);
        Assert.Equal(175m, result.TotalPayment);
    }

    [Fact]
    public void CalculateArrear_FullPayment_ReturnsZero()
    {
        var payment = new PaymentInfo { Cash = 100, Checks = 0, CardPay = 0, Zhifubao = 0, Weixin = 0 };
        var result = _sut.CalculateArrear(100m, payment);
        Assert.Equal(0m, result);
    }

    [Fact]
    public void CalculateArrear_PartialPayment_ReturnsRemainder()
    {
        var payment = new PaymentInfo { Cash = 50, Checks = 20, CardPay = 0, Zhifubao = 10, Weixin = 0 };
        var result = _sut.CalculateArrear(100m, payment);
        Assert.Equal(20m, result);
    }

    [Fact]
    public void CalculateArrear_OverPayment_ReturnsZero()
    {
        var payment = new PaymentInfo { Cash = 150, Checks = 0, CardPay = 0, Zhifubao = 0, Weixin = 0 };
        var result = _sut.CalculateArrear(100m, payment);
        Assert.Equal(0m, result);
    }

    [Fact]
    public void ValidateDiscountRate_DelegatesToClient()
    {
        var client = new ClientInfor { Level = "VIP" };
        _sut.ValidateDiscountRate(client, 0.70m); // Should not throw
        Assert.Throws<BusinessRuleException>(() => _sut.ValidateDiscountRate(client, 0.71m));
    }
}
