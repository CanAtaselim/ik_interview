using System.Globalization;
using System.Reflection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Common.Behaviors;
using Recruitment.Application.Features.Auth;

namespace Recruitment.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddScoped<IDispatcher, Dispatcher>();
        services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

        // Sıra önemli: Validation → Logging → Transaction → Handler.
        services.AddScoped<IPipelineBehavior, ValidationBehavior>();
        services.AddScoped<IPipelineBehavior, LoggingBehavior>();
        services.AddScoped<IPipelineBehavior, TransactionBehavior>();

        services.AddHandlers(assembly);

        // Değerlendirme puanı hesabı (Strategy): başka bir yöntem gerekirse yalnızca bu kayıt değişir.
        services.AddSingleton<Domain.Scorecards.IScoreCalculator, Domain.Scorecards.WeightedAverageCalculator>();

        services.AddScoped<Features.Notifications.EmailComposer>();
        services.AddScoped<Features.Notifications.Notifier>();
        services.AddScoped<IAfterCommitQueue, AfterCommitQueue>();

        // WithMessage unutulan bir kural olursa varsayılan mesaj da Türkçe çıksın.
        ValidatorOptions.Global.LanguageManager.Culture = new CultureInfo("tr");
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);

        services.AddScoped<SessionIssuer>();

        return services;
    }

    private static void AddHandlers(this IServiceCollection services, Assembly assembly)
    {
        var handlerTypes = assembly.GetTypes().Where(t => t is { IsClass: true, IsAbstract: false });

        foreach (var type in handlerTypes)
        {
            var contracts = type.GetInterfaces().Where(i =>
                i.IsGenericType &&
                (i.GetGenericTypeDefinition() == typeof(ICommandHandler<,>) ||
                 i.GetGenericTypeDefinition() == typeof(IQueryHandler<,>) ||
                 i.GetGenericTypeDefinition() == typeof(IDomainEventHandler<>)));

            foreach (var contract in contracts)
                services.AddScoped(contract, type);
        }
    }
}
