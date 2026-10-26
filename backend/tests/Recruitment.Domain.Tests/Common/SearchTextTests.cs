using Recruitment.Domain.Common;
using Shouldly;

namespace Recruitment.Domain.Tests.Common;

public sealed class SearchTextTests
{
    [Theory]
    [InlineData("IŞIK", "işik")]
    [InlineData("ışık", "işik")]
    [InlineData("İstanbul", "istanbul")]
    [InlineData("  DEMİR  ", "demir")]
    public void Fold_TreatsAllVariantsOfIAsSameLetter(string input, string expected)
    {
        SearchText.Fold(input).ShouldBe(expected);
    }

    [Fact]
    public void Tokenize_SplitsOnAnythingThatIsNotLetterOrDigit()
    {
        SearchText.Tokenize("Ayşe.Yılmaz@Örnek.com, C# & Node.js (3 yıl)")
            .ShouldBe(["ayşe", "yilmaz", "örnek", "com", "c", "node", "js", "3", "yil"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("--- !!!")]
    public void Tokenize_WithNothingSearchable_ReturnsEmpty(string? input)
    {
        SearchText.Tokenize(input).ShouldBeEmpty();
    }

    [Fact]
    public void Build_JoinsPartsAndRespectsMaxLength()
    {
        SearchText.Build(["Ayşe", null, "Yılmaz", " "], maxLength: 100).ShouldBe("ayşe yilmaz");
        SearchText.Build(["abcdef", "ghijkl"], maxLength: 8).ShouldBe("abcdef g");
    }
}
