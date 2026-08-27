using QP11.Core.Constants;
using QP11.Core.Entities;

namespace QP11.Tests.Entities;

public class DetailSellTests
{
    [Fact]
    public void ApplyFlag_NormalSale_SetsFlag1()
    {
        var detail = new DetailSell();
        detail.ApplyFlag(isReturn: false);
        Assert.Equal((int)BusinessConstants.BillFlag.Confirmed, detail.Flag);
    }

    [Fact]
    public void ApplyFlag_Return_SetsFlag2()
    {
        var detail = new DetailSell();
        detail.ApplyFlag(isReturn: true);
        Assert.Equal((int)BusinessConstants.BillFlag.Returned, detail.Flag);
    }

    [Fact]
    public void ApplyFlag_Exchange_SetsFlag3()
    {
        var detail = new DetailSell();
        detail.ApplyFlag(isReturn: false, isExchange: true);
        Assert.Equal((int)BusinessConstants.BillFlag.Voided, detail.Flag);
    }

    [Fact]
    public void ApplyFlag_ExchangeOverridesReturn_SetsFlag3()
    {
        var detail = new DetailSell();
        detail.ApplyFlag(isReturn: true, isExchange: true);
        Assert.Equal((int)BusinessConstants.BillFlag.Voided, detail.Flag);
    }
}
