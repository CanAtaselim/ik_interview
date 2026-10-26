using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Recruitment.Application.Abstractions;

namespace Recruitment.Infrastructure.Files;

/// <summary>Dosyaları sunucu diskinde, <c>FileStorage:RootPath</c> altında yıl/ay klasörlerinde saklar.</summary>
internal sealed class LocalFileStorage(IOptions<FileStorageOptions> options, IHostEnvironment environment, TimeProvider clock)
    : IFileStorage
{
    private readonly string _root = Path.GetFullPath(options.Value.RootPath, environment.ContentRootPath);

    public async Task<string> Save(Stream content, string extension, CancellationToken ct)
    {
        var now = clock.GetUtcNow();
        // Dosya adı rastgeledir; kullanıcının verdiği ad diske hiç yazılmaz.
        var key = $"{now:yyyy}/{now:MM}/{Guid.CreateVersion7():N}{extension}";
        var path = Resolve(key);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true);
        await content.CopyToAsync(file, ct);

        return key;
    }

    public Task<Stream> Open(string storageKey, CancellationToken ct) =>
        Task.FromResult<Stream>(new FileStream(
            Resolve(storageKey), FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true));

    public Task Delete(string storageKey, CancellationToken ct)
    {
        File.Delete(Resolve(storageKey));
        return Task.CompletedTask;
    }

    // Anahtar kök klasörün dışına çıkamaz ("../" ile başka dosyalara erişilemez).
    private string Resolve(string storageKey)
    {
        var path = Path.GetFullPath(Path.Combine(_root, storageKey));
        if (!path.StartsWith(_root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Geçersiz depolama anahtarı.");

        return path;
    }
}
