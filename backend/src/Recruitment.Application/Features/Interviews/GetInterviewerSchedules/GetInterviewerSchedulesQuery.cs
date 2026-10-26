using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Interviews;

namespace Recruitment.Application.Features.Interviews.GetInterviewerSchedules;

/// <summary>
/// Planlama için seçilen mülakatçıların müsaitlik aralıkları ve dolu saatleri; oda verilirse odanın
/// dolu saatleri. Yalnızca İK kullanır.
/// </summary>
public sealed record GetInterviewerSchedulesQuery(
    DateTimeOffset From, DateTimeOffset To, IReadOnlyCollection<Guid> UserIds, Guid? RoomId, Guid? ExcludeInterviewId)
    : IQuery<PlanningScheduleDto>
{
    public const int MaxUsers = 10;
    public static readonly TimeSpan MaxRange = TimeSpan.FromDays(14);
}

internal sealed class GetInterviewerSchedulesQueryValidator : AbstractValidator<GetInterviewerSchedulesQuery>
{
    public GetInterviewerSchedulesQueryValidator()
    {
        RuleFor(q => q.To)
            .Must((q, to) => to > q.From && to - q.From <= GetInterviewerSchedulesQuery.MaxRange)
            .WithMessage("Tarih aralığı en fazla iki hafta olabilir ve bitiş başlangıçtan sonra olmalı.");
        RuleFor(q => q.UserIds)
            .Must(ids => ids is null || ids.Count <= GetInterviewerSchedulesQuery.MaxUsers)
            .WithMessage($"En fazla {GetInterviewerSchedulesQuery.MaxUsers} kişinin takvimine birlikte bakılabilir.");
    }
}

internal sealed class GetInterviewerSchedulesQueryHandler(IAppDbContext db, ICalendarService calendar)
    : IQueryHandler<GetInterviewerSchedulesQuery, PlanningScheduleDto>
{
    public async Task<Result<PlanningScheduleDto>> Handle(GetInterviewerSchedulesQuery query, CancellationToken ct)
    {
        var userIds = (query.UserIds ?? []).Distinct().ToList();
        var users = await db.Users.AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.Email })
            .ToListAsync(ct);

        var outlook = await ReadOutlook(users.Select(u => (u.Id, u.Email)).ToList(), query, ct);

        var availability = await db.InterviewerAvailabilities.AsNoTracking()
            .Where(a => userIds.Contains(a.UserId) && a.StartsAt < query.To && a.EndsAt > query.From)
            .OrderBy(a => a.StartsAt)
            .ToListAsync(ct);

        var busy = await db.Interviews.AsNoTracking()
            .Where(i => i.Id != query.ExcludeInterviewId &&
                        i.Status == InterviewStatus.Scheduled &&
                        i.StartsAt < query.To && i.EndsAt > query.From &&
                        ((query.RoomId != null && i.RoomId == query.RoomId) ||
                         i.Interviewers.Any(x => userIds.Contains(x.UserId))))
            .OrderBy(i => i.StartsAt)
            .ToListAsync(ct);
        var names = await db.InterviewNames(busy, ct);

        BusyRangeDto ToBusy(Interview interview) => new(
            interview.Id,
            $"{names.Dictionary.GetValueOrDefault(interview.TypeId)} · {names.Candidates.GetValueOrDefault(interview.CandidateId)}",
            interview.StartsAt,
            interview.EndsAt);

        return new PlanningScheduleDto(
            [.. users
                .OrderBy(u => userIds.IndexOf(u.Id))
                .Select(u => new InterviewerScheduleDto(
                    u.Id,
                    u.DisplayName,
                    [.. availability.Where(a => a.UserId == u.Id).Select(a => new TimeRangeDto(a.StartsAt, a.EndsAt))],
                    [.. busy.Where(i => i.IsAssignedTo(u.Id)).Select(ToBusy)],
                    outlook[u.Id].Status,
                    [.. outlook[u.Id].Busy.Select(b => new TimeRangeDto(b.StartsAt, b.EndsAt))]))],
            [.. busy.Where(i => query.RoomId is not null && i.RoomId == query.RoomId).Select(ToBusy)]);
    }

    // Outlook'a ulaşılamaması planlamayı engellemez; kişi başına durum döner ve arayüz uyarır.
    private async Task<Dictionary<Guid, MailboxBusy>> ReadOutlook(
        List<(Guid Id, string? Email)> users, GetInterviewerSchedulesQuery query, CancellationToken ct)
    {
        if (!calendar.IsEnabled)
            return users.ToDictionary(u => u.Id, _ => MailboxBusy.Of(OutlookStatus.NotConfigured));

        var mailboxes = users.Select(u => u.Email).OfType<string>().Where(e => e.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var read = mailboxes.Count == 0
            ? new Dictionary<string, MailboxBusy>()
            : new Dictionary<string, MailboxBusy>(await calendar.GetBusy(mailboxes, query.From, query.To, ct), StringComparer.OrdinalIgnoreCase);

        return users.ToDictionary(
            u => u.Id,
            u => !string.IsNullOrWhiteSpace(u.Email) && read.TryGetValue(u.Email, out var busy)
                ? busy
                : MailboxBusy.Of(string.IsNullOrWhiteSpace(u.Email) ? OutlookStatus.NoMailbox : OutlookStatus.Unavailable));
    }
}
