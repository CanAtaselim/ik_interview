using Recruitment.Domain.Common;
using Shouldly;

namespace Recruitment.Domain.Tests.Common;

public sealed class MoneyTests
{
    [Fact]
    public void Create_WithDefaults_IsNetTurkishLira()
    {
        var money = new Money(85000);

        money.Amount.ShouldBe(85000);
        money.Currency.ShouldBe("TRY");
        money.IsNet.ShouldBeTrue();
    }

    [Fact]
    public void Create_NormalizesCurrencyCode()
    {
        new Money(1000, " usd ", isNet: false).Currency.ShouldBe("USD");
    }

    [Fact]
    public void Create_WithNegativeAmount_Throws()
    {
        Should.Throw<DomainException>(() => new Money(-1));
    }

    [Fact]
    public void Create_WithInvalidCurrency_Throws()
    {
        Should.Throw<DomainException>(() => new Money(1, "LIRA"));
    }

    [Fact]
    public void Equality_IsByValue()
    {
        new Money(100, "TRY", true).ShouldBe(new Money(100, "TRY", true));
        new Money(100, "TRY", true).ShouldNotBe(new Money(100, "TRY", false));
    }
}
