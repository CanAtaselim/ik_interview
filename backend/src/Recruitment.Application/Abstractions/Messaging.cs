using Recruitment.Application.Common;

namespace Recruitment.Application.Abstractions;

/// <summary>Veri değiştiren istek.</summary>
public interface ICommand<TResponse>;

/// <summary>Veri değiştirmeyen istek.</summary>
public interface IQuery<TResponse>;

public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken ct);
}

public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> Handle(TQuery query, CancellationToken ct);
}

public interface IDispatcher
{
    Task<Result<T>> Send<T>(ICommand<T> command, CancellationToken ct = default);

    Task<Result<T>> Query<T>(IQuery<T> query, CancellationToken ct = default);
}

public sealed record RequestContext(object Request, bool IsCommand)
{
    public string Name => Request.GetType().Name;
}

/// <summary>Handler'ın çevresinde çalışan kesişen iş (validasyon, loglama, transaction).</summary>
public interface IPipelineBehavior
{
    Task<Result<TResponse>> Handle<TResponse>(
        RequestContext context,
        Func<Task<Result<TResponse>>> next,
        CancellationToken ct);
}
