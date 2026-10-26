using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.Common;

namespace Recruitment.Application.Common;

internal sealed class Dispatcher(IServiceProvider services) : IDispatcher
{
    private static readonly ConcurrentDictionary<Type, object> Invokers = new();

    public Task<Result<T>> Send<T>(ICommand<T> command, CancellationToken ct = default) =>
        Run<T>(command, isCommand: true, typeof(CommandInvoker<,>), ct);

    public Task<Result<T>> Query<T>(IQuery<T> query, CancellationToken ct = default) =>
        Run<T>(query, isCommand: false, typeof(QueryInvoker<,>), ct);

    private async Task<Result<T>> Run<T>(object request, bool isCommand, Type invokerDefinition, CancellationToken ct)
    {
        var requestType = request.GetType();
        var invoker = (Invoker<T>)Invokers.GetOrAdd(
            requestType,
            type => Activator.CreateInstance(invokerDefinition.MakeGenericType(type, typeof(T)))!);

        var context = new RequestContext(request, isCommand);
        Func<Task<Result<T>>> pipeline = () => invoker.Invoke(request, services, ct);

        // Kayıt sırası dıştan içe: ilk kaydedilen davranış en dışta çalışır.
        foreach (var behavior in services.GetServices<IPipelineBehavior>().Reverse())
        {
            var next = pipeline;
            pipeline = () => behavior.Handle(context, next, ct);
        }

        try
        {
            return await pipeline();
        }
        catch (DomainException ex)
        {
            return Error.BusinessRule("Domain.RuleViolation", ex.Message);
        }
    }

    private abstract class Invoker<TResponse>
    {
        public abstract Task<Result<TResponse>> Invoke(object request, IServiceProvider services, CancellationToken ct);
    }

    private sealed class CommandInvoker<TCommand, TResponse> : Invoker<TResponse>
        where TCommand : ICommand<TResponse>
    {
        public override Task<Result<TResponse>> Invoke(object request, IServiceProvider services, CancellationToken ct) =>
            services.GetRequiredService<ICommandHandler<TCommand, TResponse>>().Handle((TCommand)request, ct);
    }

    private sealed class QueryInvoker<TQuery, TResponse> : Invoker<TResponse>
        where TQuery : IQuery<TResponse>
    {
        public override Task<Result<TResponse>> Invoke(object request, IServiceProvider services, CancellationToken ct) =>
            services.GetRequiredService<IQueryHandler<TQuery, TResponse>>().Handle((TQuery)request, ct);
    }
}
