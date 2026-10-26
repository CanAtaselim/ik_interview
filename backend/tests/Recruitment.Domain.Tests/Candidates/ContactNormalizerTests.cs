using Recruitment.Domain.Candidates;
using Shouldly;

namespace Recruitment.Domain.Tests.Candidates;

public sealed class ContactNormalizerTests
{
    [Theory]
    [InlineData(" Ayse.Yilmaz@Ornek.COM ", "ayse.yilmaz@ornek.com")]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void NormalizeEmail_TrimsAndLowercases(string? input, string? expected)
    {
        ContactNormalizer.NormalizeEmail(input).ShouldBe(expected);
    }

    [Theory]
    [InlineData("+90 (532) 123 45 67", "5321234567")]
    [InlineData("0532 123 4567", "5321234567")]
    [InlineData("5321234567", "5321234567")]
    [InlineData("0090-532-123-45-67", "5321234567")]
    [InlineData("444 0 123", "4440123")]
    [InlineData("dahili yok", null)]
    [InlineData(null, null)]
    public void NormalizePhone_KeepsLastTenDigits(string? input, string? expected)
    {
        ContactNormalizer.NormalizePhone(input).ShouldBe(expected);
    }
}
