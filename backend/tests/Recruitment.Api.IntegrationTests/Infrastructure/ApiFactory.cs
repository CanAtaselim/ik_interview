using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.Users;
using Recruitment.Infrastructure.Outbox;
using Testcontainers.PostgreSql;

namespace Recruitment.Api.IntegrationTests.Infrastructure;

/// <summary>
/// API'yi gerçek bir PostgreSQL konteynerine karşı ayağa kaldırır. Ortam "Testing"dir; böylece
/// geliştiricinin user-secrets'ı yüklenmez ve cookie'ler üretimdeki gibi Secure verilir.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17").Build();

    // Yüklenen dosyalar her test koşusuna özel geçici bir klasöre yazılır.
    private readonly string _fileRoot = Path.Combine(Path.GetTempPath(), $"recruitment-tests-{Guid.NewGuid():N}");

    public TestLdapService Ldap { get; } = new();

    public TestEmailSender Emails { get; } = new();

    public RecordingNotificationPusher Pushes { get; } = new();

    public TestCalendarService Calendar { get; } = new();

    private readonly ConcurrentDictionary<string, Lazy<Task>> _positionTitles = new();

    /// <summary>
    /// Kadro talebinde pozisyon adı "Pozisyon adları" listesinden seçilir; testin kullanacağı adı o
    /// listeye (bir kez) ekler ve aynı adı döner.
    /// </summary>
    public async Task<string> PositionTitle(string name)
    {
        await _positionTitles.GetOrAdd(name, title => new Lazy<Task>(async () =>
        {
            var manager = await LoginAs("İnsan Kaynakları", Role.HrManager);
            var response = await manager.Post("/api/v1/dictionaries/PositionTitle/items", new { name = title });
            if (response.StatusCode != HttpStatusCode.Conflict)
                response.EnsureSuccessStatusCode();
        })).Value;
        return name;
    }

    public Task InitializeAsync() => _postgres.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();

        if (Directory.Exists(_fileRoot))
            Directory.Delete(_fileRoot, recursive: true);
    }

    /// <summary>Cookie'leri kendisi yönetmeyen istemci; testler refresh cookie'sini elle taşır.</summary>
    public HttpClient CreateApiClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = false,
    });

    /// <summary>Verilen birimde ve rollerde yeni bir kullanıcıyla giriş yapmış istemci döner.</summary>
    public Task<ApiUser> LoginAs(string department, params Role[] roles) =>
        LoginExisting(Ldap.AddUserIn(department, roles));

    /// <summary>Dizine önceden eklenmiş kullanıcıyla giriş yapar.</summary>
    public async Task<ApiUser> LoginExisting(string username)
    {
        var client = CreateApiClient();

        var response = await client.PostAsJsonAsync(
            "/api/v1/auth/login", new { username, password = TestLdapService.Password });
        response.EnsureSuccessStatusCode();

        var session = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", session.GetProperty("accessToken").GetString());

        return new ApiUser(client, session.GetProperty("user").GetProperty("id").GetGuid(), username);
    }

    public Task<ApiUser> LoginAs(params Role[] roles) => LoginAs(TestLdapService.DefaultDepartment, roles);

    /// <summary>Bekleyen domain event'leri işler (üretimde bunu 15 saniyede bir Hangfire yapar).</summary>
    public Task<int> ProcessOutbox() => Services.GetRequiredService<OutboxProcessor>().ProcessPending();

    /// <summary>Bekleyen olayları işler ve kuyruktaki e-postaları gönderir (üretimde Hangfire yapar).</summary>
    public async Task ProcessOutboxAndEmails()
    {
        await ProcessOutbox();
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IDispatcher>()
            .Send(new Recruitment.Application.Features.Notifications.DispatchOutgoingEmails.DispatchOutgoingEmailsCommand());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _postgres.GetConnectionString(),
                ["Database:ApplyMigrationsOnStartup"] = "true",
                ["Jobs:Enabled"] = "false",
                ["FileStorage:RootPath"] = _fileRoot,
                ["Jwt:SigningKey"] = "yalnizca-testlerde-kullanilan-imza-anahtari-0123456789",
                ["Ldap:Host"] = "ldap.test.local",
                ["Ldap:BaseDn"] = "DC=test,DC=local",
            }));

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<ILdapService>();
            services.AddSingleton<ILdapService>(Ldap);
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
            services.RemoveAll<INotificationPusher>();
            services.AddSingleton<INotificationPusher>(Pushes);
            services.RemoveAll<ICalendarService>();
            services.AddSingleton<ICalendarService>(Calendar);

            // Zamanlanmış işler testlerde çalışmaz; outbox ProcessOutbox ile elle işlenir (hızlı ve belirli).
            // Hangfire sunucusu bir fabrika fonksiyonuyla kayıtlı olduğu için geldiği derlemeden tanınır.
            var hangfireServer = services.Single(s =>
                s.ServiceType == typeof(IHostedService) &&
                s.ImplementationFactory?.Method.DeclaringType?.Assembly.GetName().Name?.StartsWith("Hangfire") == true);
            services.Remove(hangfireServer);
        });
    }
}

public sealed record ApiUser(HttpClient Client, Guid Id, string Username);
