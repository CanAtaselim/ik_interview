using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Notifications;

namespace Recruitment.Application.Features.Notifications.Preferences;

public sealed record NotificationPreferenceDto(NotificationType Type, bool EmailEnabled);

/// <summary>Her bildirim türü için e-posta tercihi; kayıt yoksa e-posta açıktır.</summary>
public sealed record GetNotificationPreferencesQuery : IQuery<IReadOnlyList<NotificationPreferenceDto>>;

internal sealed class GetNotificationPreferencesQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IQueryHandler<GetNotificationPreferencesQuery, IReadOnlyList<NotificationPreferenceDto>>
{
    public async Task<Result<IReadOnlyList<NotificationPreferenceDto>>> Handle(
        GetNotificationPreferencesQuery query, CancellationToken ct)
    {
        var stored = await db.NotificationPreferences.AsNoTracking()
            .Where(p => p.UserId == currentUser.UserId)
            .ToDictionaryAsync(p => p.Type, p => p.EmailEnabled, ct);

        return Enum.GetValues<NotificationType>()
            .Select(type => new NotificationPreferenceDto(type, stored.GetValueOrDefault(type, true)))
            .ToList();
    }
}

public sealed record UpdateNotificationPreferencesCommand(IReadOnlyList<NotificationPreferenceDto> Preferences) : ICommand<Unit>;

internal sealed class UpdateNotificationPreferencesCommandValidator : AbstractValidator<UpdateNotificationPreferencesCommand>
{
    public UpdateNotificationPreferencesCommandValidator() =>
        RuleForEach(c => c.Preferences)
            .Must(p => Enum.IsDefined(p.Type))
            .WithMessage("Bilinmeyen bildirim türü.");
}

internal sealed class UpdateNotificationPreferencesCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : ICommandHandler<UpdateNotificationPreferencesCommand, Unit>
{
    public async Task<Result<Unit>> Handle(UpdateNotificationPreferencesCommand command, CancellationToken ct)
    {
        var stored = await db.NotificationPreferences.Where(p => p.UserId == currentUser.UserId).ToListAsync(ct);
        foreach (var preference in command.Preferences.DistinctBy(p => p.Type))
        {
            var existing = stored.FirstOrDefault(p => p.Type == preference.Type);
            if (existing is null)
                db.NotificationPreferences.Add(NotificationPreference.Create(currentUser.UserId, preference.Type, preference.EmailEnabled));
            else
                existing.SetEmail(preference.EmailEnabled);
        }

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
