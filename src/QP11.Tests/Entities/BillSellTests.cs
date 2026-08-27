using QP11.Core.Constants;
using QP11.Core.Entities;

namespace QP11.Tests.Entities;

public class BillSellTests
{
    [Fact]
    public void CalculateTotal_SetsAllTotalFields()
    {
        var bill = new BillSell();
        var details = new List<DetailSell>
        {
            new() { Price = 100, Amount = 2 },
            new() { Price = 50, Amount = 3 }
        };

        bill.CalculateTotal(details, discountRate: 0.8m, yunfei: 10m);

        Assert.Equal(350m, bill.Total);         // 100*2 + 50*3
        Assert.Equal(280m, bill.BillTotal);      // 350 * 0.8
        Assert.Equal(290m, bill.TotalPayment);   // 280 + 10
        Assert.Equal(280m, bill.BillPayment);    // same as BillTotal
        Assert.Equal(0.8m, bill.DiscountRate);
    }

    [Fact]
    public void CalculateTotal_ZeroDiscount_SetsFullPrice()
    {
        var bill = new BillSell();
        var details = new List<DetailSell>
        {
            new() { Price = 200, Amount = 1 }
        };

        bill.CalculateTotal(details, discountRate: 1.0m);

        Assert.Equal(200m, bill.Total);
        Assert.Equal(200m, bill.BillTotal);
        Assert.Equal(200m, bill.TotalPayment);
    }

    [Fact]
    public void CalculateTotal_EmptyDetails_SetsZero()
    {
        var bill = new BillSell();
        var details = new List<DetailSell>();

        bill.CalculateTotal(details, discountRate: 1.0m);

        Assert.Equal(0m, bill.Total);
        Assert.Equal(0m, bill.BillTotal);
    }
}
