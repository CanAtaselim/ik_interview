using Recruitment.Infrastructure.Identity;
using Shouldly;

namespace Recruitment.Api.IntegrationTests.Auth;

public sealed class LdapFilterEscapingTests
{
    [Theory]
    [InlineData("ayse.yilmaz", "ayse.yilmaz")]
    [InlineData("*", @"\2a")]
    [InlineData("admin)(|(uid=*", @"admin\29\28|\28uid=\2a")]
    [InlineData(@"CN=Kaya\, Zeynep,OU=IK", @"CN=Kaya\5c, Zeynep,OU=IK")]
    [InlineData("a\0b", @"a\00b")]
    public void EscapeFilterValue_WithSpecialCharacters_EscapesThem(string input, string expected)
    {
        LdapService.EscapeFilterValue(input).ShouldBe(expected);
    }
}
