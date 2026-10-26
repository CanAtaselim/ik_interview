using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Recruitment.Infrastructure.Persistence.Seed;

namespace Recruitment.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>Bekleyen migration'ları uygular ve başlangıç verisini ekler.</summary>
    public static async Task InitializeDatabase(this IServiceProvider services, CancellationToken ct = default)
    {
        await using var scope = services.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync(ct);
        await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().Seed(ct);
    }
}
