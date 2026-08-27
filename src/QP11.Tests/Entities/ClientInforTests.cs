using QP11.Core.Entities;
using QP11.Core.Exceptions;

namespace QP11.Tests.Entities;

public class ClientInforTests
{
    [Fact]
    public void ValidateDiscount_VipWithinLimit_DoesNotThrow()
    {
        var client = new ClientInfor { Level = "VIP" };
        client.ValidateDiscount(0.70m); // Should not throw
    }

    [Fact]
    public void ValidateDiscount_VipOverLimit_ThrowsBusinessRuleException()
    {
        var client = new ClientInfor { Level = "VIP" };
        Assert.Throws<BusinessRuleException>(() => client.ValidateDiscount(0.71m));
    }

    [Fact]
    public void ValidateDiscount_NormalWithinLimit_DoesNotThrow()
    {
        var client = new ClientInfor { Level = "普通" };
        client.ValidateDiscount(0.85m);
    }

    [Fact]
    public void ValidateDiscount_NormalOverLimit_ThrowsBusinessRuleException()
    {
        var client = new ClientInfor { Level = "普通" };
        Assert.Throws<BusinessRuleException>(() => client.ValidateDiscount(0.86m));
    }

    [Fact]
    public void ValidateDiscount_DefaultLevelWithinLimit_DoesNotThrow()
    {
        var client = new ClientInfor { Level = "批发" };
        client.ValidateDiscount(0.95m);
    }

    [Fact]
    public void ValidateDiscount_DefaultLevelOverLimit_ThrowsBusinessRuleException()
    {
        var client = new ClientInfor { Level = "批发" };
        Assert.Throws<BusinessRuleException>(() => client.ValidateDiscount(0.96m));
    }
}
