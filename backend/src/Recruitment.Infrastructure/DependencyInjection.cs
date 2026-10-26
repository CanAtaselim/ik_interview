using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Recruitment.Application.Abstractions;
using Recruitment.Infrastructure.Blacklist;
using Recruitment.Infrastructure.Calendar;
using Recruitment.Infrastructure.Email;
using Recruitment.Infrastructure.Files;
using Recruitment.Infrastructure.Identity;
using Recruitment.Infrastructure.Jobs;
using Recruitment.Infrastructure.Outbox;
using Recruitment.Infrastructure.Persistence;
using Recruitment.Infrastructure.Persistence.Interceptors;
using Recruitment.Infrastructure.Persistence.Seed;
using Recruitment.Infrastructure.Search;

namespace Recruitment.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddMemoryCache();

        services.AddPersistence();
        services.AddIdentity();
        services.AddBackgroundJobs();
        services.AddFiles();
        services.AddEmail();
        services.AddOutlookCalendar();

        return services;
    }

    private static void AddPersistence(this IServiceCollection services)
    {
        services.AddScoped<AuditableEntityInterceptor>();
        services.AddScoped<AuditLogInterceptor>();
        services.AddScoped<DomainEventsToOutboxInterceptor>();

        services.AddDbContext<AppDbContext>((provider, options) => options
            .UseNpgsql(provider.GetRequiredService<IConfiguration>().GetConnectionString("Default"))
            .UseSnakeCaseNamingConvention()
            // Sıra önemli: önce audit alanları ve soft delete, sonra audit kaydı, en son outbox.
            .AddInterceptors(
                provider.GetRequiredService<AuditableEntityInterceptor>(),
                provider.GetRequiredService<AuditLogInterceptor>(),
                provider.GetRequiredService<DomainEventsToOutboxInterceptor>()));

        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddScoped<DatabaseSeeder>();
        services.AddSingleton<ICandidateTextSearch, CandidateTextSearch>();
        services.AddScoped<IBlacklistMatcher, BlacklistMatcher>();

        services.AddOptions<RecruitmentOptions>().BindConfiguration(RecruitmentOptions.SectionName);
        services.AddScoped<IBusinessCalendar, BusinessCalendar>();
    }

    private static void AddFiles(this IServiceCollection services)
    {
        services.AddOptions<FileStorageOptions>().BindConfiguration(FileStorageOptions.SectionName);

        services.AddDataProtection();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddSingleton<IDownloadTokenService, DownloadTokenService>();
        services.AddSingleton<ITextExtractor, TextExtractor>();
    }

    // SMTP boş bırakılabilir: o durumda e-postalar gönderilmez, kayda geçer ve uygulama açılır.
    private static void AddEmail(this IServiceCollection services)
    {
        services.AddOptions<SmtpOptions>().BindConfiguration(SmtpOptions.SectionName);
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IIcsGenerator, IcsGenerator>();
    }

    // Graph ayarları boş bırakılabilir: o durumda Outlook okunmaz, yalnızca sistem içi müsaitlik kullanılır.
    private static void AddOutlookCalendar(this IServiceCollection services)
    {
        services.AddOptions<GraphOptions>()
            .BindConfiguration(GraphOptions.SectionName)
            .Validate<IHostEnvironment>(
                (o, environment) => !o.UseFake || environment.IsDevelopment(),
                "Graph:UseFake yalnızca Development ortamında kullanılabilir.")
            .ValidateOnStart();

        services.AddHttpClient(GraphCalendarService.ApiClientName, client =>
            client.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/"));
        services.AddHttpClient(GraphCalendarService.TokenClientName, client =>
            client.BaseAddress = new Uri("https://login.microsoftonline.com/"));

        services.AddSingleton<ICalendarService>(provider =>
            provider.GetRequiredService<IOptions<GraphOptions>>().Value.UseFake
                ? new FakeCalendarService()
                : ActivatorUtilities.CreateInstance<GraphCalendarService>(provider));
    }

    private static void AddIdentity(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>()
            .BindConfiguration(JwtOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<LdapOptions>()
            .BindConfiguration(LdapOptions.SectionName)
            .ValidateDataAnnotations()
            .Validate(
                o => o.UseFake || (!string.IsNullOrWhiteSpace(o.Host) && !string.IsNullOrWhiteSpace(o.BaseDn)),
                "Ldap:Host ve Ldap:BaseDn doldurulmalı.")
            .Validate<IHostEnvironment>(
                (o, environment) => !o.UseFake || environment.IsDevelopment(),
                "Ldap:UseFake yalnızca Development ortamında kullanılabilir.")
            .ValidateOnStart();

        services.AddSingleton<ILdapService>(provider =>
            provider.GetRequiredService<IOptions<LdapOptions>>().Value.UseFake
                ? new FakeLdapService()
                : ActivatorUtilities.CreateInstance<LdapService>(provider));

        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<ILoginThrottle, LoginThrottle>();
    }

    private static void AddBackgroundJobs(this IServiceCollection services)
    {
        services.AddSingleton<OutboxProcessor>();
        services.AddTransient<OutboxProcessorJob>();
        services.AddTransient<BlacklistExpiryJob>();
        services.AddTransient<CvReviewSlaJob>();
        services.AddTransient<InterviewReminderJob>();
        services.AddTransient<EmailDispatchJob>();

        services.AddHangfire((provider, hangfire) => hangfire
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(storage => storage.UseNpgsqlConnection(
                provider.GetRequiredService<IConfiguration>().GetConnectionString("Default"))));

        services.AddHangfireServer();
    }
}
