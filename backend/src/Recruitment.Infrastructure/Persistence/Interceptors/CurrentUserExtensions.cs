using Recruitment.Application.Abstractions;

namespace Recruitment.Infrastructure.Persistence.Interceptors;

internal static class CurrentUserExtensions
{
    private const string SystemActor = "system";

    /// <summary>Kayda yazılacak kullanıcı adı; oturum yoksa (giriş, seed, job) "system".</summary>
    public static string ActorName(this ICurrentUser currentUser) =>
        currentUser.IsAuthenticated ? currentUser.Username : SystemActor;
}
