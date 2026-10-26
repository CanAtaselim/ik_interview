using Microsoft.Extensions.DependencyInjection;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.Common;

namespace Recruitment.Application.Common;

internal sealed class DomainEventDispatcher(IServiceProvider services) : IDomainEventDispatcher
{
    public async Task Dispatch(IDomainEvent domainEvent, CancellationToken ct)
    {
        var handlerType = typeof(IDomainEventHandler<>).MakeGenericType(domainEvent.GetType());
        var handle = handlerType.GetMethod(nameof(IDomainEventHandler<IDomainEvent>.Handle))!;

        foreach (var handler in services.GetServices(handlerType))
            await (Task)handle.Invoke(handler, [domainEvent, ct])!;
    }
}
