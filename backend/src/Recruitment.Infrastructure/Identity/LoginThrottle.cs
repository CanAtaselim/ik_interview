using Microsoft.Extensions.Caching.Memory;
using Recruitment.Application.Abstractions;

namespace Recruitment.Infrastructure.Identity;

/// <summary>
/// 5 dakikada 5 başarısız denemeden sonra kullanıcı adını 5 dakika kilitler. Sayaç bellekte
/// tutulur; uygulama birden çok sunucuda çalışacaksa paylaşılan bir depoya taşınmalıdır.
/// </summary>
internal sealed class LoginThrottle(IMemoryCache cache) : ILoginThrottle
{
    private const int MaxFailures = 5;

    private static readonly TimeSpan FailureWindow = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(5);

    public bool IsLockedOut(string username) => cache.TryGetValue(LockKey(username), out _);

    public void RecordFailure(string username)
    {
        var counter = cache.GetOrCreate(FailureKey(username), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = FailureWindow;
            return new FailureCounter();
        })!;

        if (Interlocked.Increment(ref counter.Count) < MaxFailures)
            return;

        cache.Set(LockKey(username), true, LockoutDuration);
        cache.Remove(FailureKey(username));
    }

    public void Reset(string username) => cache.Remove(FailureKey(username));

    private static string FailureKey(string username) => $"login-failures:{username}";

    private static string LockKey(string username) => $"login-lock:{username}";

    private sealed class FailureCounter
    {
        public int Count;
    }
}
