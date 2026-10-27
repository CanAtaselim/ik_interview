using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Users;

namespace Recruitment.Application.Features.Users.Preferences;

/// <summary>Kullanıcının saklayabildiği tercih anahtarları. Yeni bir kişisel ayar eklemek için buraya yazılır.</summary>
public static class UserPreferenceKeys
{
    public const string DashboardLayout = "dashboard.layout";

    private static readonly HashSet<string> Known = [DashboardLayout];

    public static bool IsKnown(string key) => Known.Contains(key);
}

/// <summary>Kayıtlı tercih; hiç kaydedilmemişse <see cref="Value"/> null'dır.</summary>
public sealed record UserPreferenceDto(string Key, JsonElement? Value);

public sealed record GetUserPreferenceQuery(string Key) : IQuery<UserPreferenceDto>;

internal sealed class GetUserPreferenceQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IQueryHandler<GetUserPreferenceQuery, UserPreferenceDto>
{
    public async Task<Result<UserPreferenceDto>> Handle(GetUserPreferenceQuery query, CancellationToken ct)
    {
        if (!UserPreferenceKeys.IsKnown(query.Key))
            return Error.NotFound("Preference.UnknownKey", "Bilinmeyen tercih.");

        var stored = await db.UserPreferences.AsNoTracking()
            .Where(p => p.UserId == currentUser.UserId && p.Key == query.Key)
            .Select(p => p.Value)
            .FirstOrDefaultAsync(ct);

        return new UserPreferenceDto(query.Key, stored is null ? null : JsonDocument.Parse(stored).RootElement.Clone());
    }
}

public sealed record SaveUserPreferenceCommand(string Key, JsonElement Value) : ICommand<Unit>;

internal sealed class SaveUserPreferenceCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<SaveUserPreferenceCommand, Unit>
{
    public async Task<Result<Unit>> Handle(SaveUserPreferenceCommand command, CancellationToken ct)
    {
        if (!UserPreferenceKeys.IsKnown(command.Key))
            return Error.NotFound("Preference.UnknownKey", "Bilinmeyen tercih.");

        if (command.Value.ValueKind != JsonValueKind.Object)
            return Error.Validation(new Dictionary<string, string[]> { ["value"] = ["Tercih bir nesne olmalıdır."] });

        var json = command.Value.GetRawText();
        if (json.Length > UserPreference.ValueMaxLength)
            return Error.Validation(new Dictionary<string, string[]> { ["value"] = ["Tercih çok büyük."] });

        var now = clock.GetUtcNow();
        var existing = await db.UserPreferences
            .FirstOrDefaultAsync(p => p.UserId == currentUser.UserId && p.Key == command.Key, ct);
        if (existing is null)
            db.UserPreferences.Add(UserPreference.Create(currentUser.UserId, command.Key, json, now));
        else
            existing.Update(json, now);

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}

/// <summary>Kaydı siler; kullanıcı varsayılana döner.</summary>
public sealed record ResetUserPreferenceCommand(string Key) : ICommand<Unit>;

internal sealed class ResetUserPreferenceCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : ICommandHandler<ResetUserPreferenceCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ResetUserPreferenceCommand command, CancellationToken ct)
    {
        if (!UserPreferenceKeys.IsKnown(command.Key))
            return Error.NotFound("Preference.UnknownKey", "Bilinmeyen tercih.");

        await db.UserPreferences
            .Where(p => p.UserId == currentUser.UserId && p.Key == command.Key)
            .ExecuteDeleteAsync(ct);
        return Unit.Value;
    }
}
