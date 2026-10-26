using Microsoft.Extensions.Logging;
using Recruitment.Application.Abstractions;

namespace Recruitment.Application.Common;

internal sealed class AfterCommitQueue(ILogger<AfterCommitQueue> logger) : IAfterCommitQueue
{
    private readonly List<Func<CancellationToken, Task>> _actions = [];

    public void Enqueue(Func<CancellationToken, Task> action) => _actions.Add(action);

    // Her iş en iyi çabayla çalışır; biri başarısız olursa diğerleri yine çalışır.
    public async Task RunAll(CancellationToken ct)
    {
        var actions = _actions.ToList();
        _actions.Clear();
        foreach (var action in actions)
        {
            try
            {
                await action(ct);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                logger.LogWarning(exception, "Commit sonrası iş çalıştırılamadı");
            }
        }
    }
}
