using Recruitment.Domain.Users;
using Shouldly;

namespace Recruitment.Domain.Tests.Users;

public sealed class UsernameNormalizerTests
{
    [Theory]
    [InlineData("ayse.yilmaz", "ayse.yilmaz")]
    [InlineData("  Ayse.Yilmaz  ", "ayse.yilmaz")]
    [InlineData(@"SIRKET\Ayse.Yilmaz", "ayse.yilmaz")]
    [InlineData("ayse.yilmaz@sirket.local", "ayse.yilmaz")]
    [InlineData(@"SIRKET\ayse.yilmaz@sirket.local", "ayse.yilmaz")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void Normalize_WithDomainDecorations_ReturnsBareLowercaseName(string? input, string expected)
    {
        UsernameNormalizer.Normalize(input).ShouldBe(expected);
    }
}
