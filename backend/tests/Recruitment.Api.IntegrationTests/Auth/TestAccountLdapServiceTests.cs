using Microsoft.Extensions.Logging.Abstractions;
using Recruitment.Application.Abstractions;
using Recruitment.Infrastructure.Identity;
using Shouldly;

namespace Recruitment.Api.IntegrationTests.Auth;

public sealed class TestAccountLdapServiceTests
{
    private sealed class RecordingDirectory : ILdapService
    {
        public int Calls { get; private set; }

        public Task<LdapUser?> Authenticate(string username, string password, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult<LdapUser?>(null);
        }
    }

    private static (TestAccountLdapService Service, RecordingDirectory Directory) Create()
    {
        var directory = new RecordingDirectory();
        return (new TestAccountLdapService(directory, new FakeLdapService(), NullLogger<TestAccountLdapService>.Instance), directory);
    }

    [Fact]
    public async Task TestAccountWithTheSharedPassword_SignsInWithoutAskingTheDirectory()
    {
        var (service, directory) = Create();

        var user = await service.Authenticate("ikyonetici", FakeLdapService.Password, CancellationToken.None);

        user.ShouldNotBeNull();
        user.Username.ShouldBe("ikyonetici");
        directory.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData("ikyonetici", "yanlis-sifre")]
    [InlineData("ad.soyad", "Test123!")]
    public async Task AnythingElse_GoesToTheRealDirectory(string username, string password)
    {
        var (service, directory) = Create();

        (await service.Authenticate(username, password, CancellationToken.None)).ShouldBeNull();

        directory.Calls.ShouldBe(1);
    }
}
