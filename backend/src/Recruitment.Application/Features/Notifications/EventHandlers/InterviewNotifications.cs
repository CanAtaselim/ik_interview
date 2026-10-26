using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Domain.Interviews;
using Recruitment.Domain.Notifications;

namespace Recruitment.Application.Features.Notifications.EventHandlers;

/// <summary>
/// Mülakat daveti, değişikliği, iptali ve hatırlatmaları. Mülakatçılara uygulama bildirimi ve
/// .ics ekli e-posta; planlamada istendiyse adaya da .ics ekli e-posta gider. Aynı mülakatın
/// davetleri aynı UID'yi taşır, böylece takvim uygulamaları kaydı günceller veya siler.
/// </summary>
internal sealed class InterviewNotifications(IAppDbContext db, Notifier notifier, IIcsGenerator ics) :
    IDomainEventHandler<InterviewScheduled>,
    IDomainEventHandler<InterviewRescheduled>,
    IDomainEventHandler<InterviewCancelled>,
    IDomainEventHandler<InterviewReminderDue>
{
    public async Task Handle(InterviewScheduled e, CancellationToken ct)
    {
        var info = await Load(e.InterviewId, ct);
        var invite = Invite(info, CalendarMethod.Request, forCandidate: false);

        await notifier.Notify(
            info.Interview.InterviewerUserIds,
            NotificationType.InterviewInvitation,
            $"Mülakat daveti: {info.Candidate} · {info.Type}",
            $"{TurkishTime.Range(info.Interview.StartsAt, info.Interview.EndsAt)} · {info.Location} · {info.Position}",
            $"/interviews/{info.Interview.Id}",
            $"iv-scheduled:{info.Interview.Id}",
            ct,
            _ => invite);

        if (info.Interview.NotifyCandidate)
        {
            await notifier.EmailCandidate(
                info.Interview.CandidateId, EmailTemplateCodes.InterviewInvitationCandidate, CandidateValues(info),
                $"iv-scheduled-candidate:{info.Interview.Id}", ct, Invite(info, CalendarMethod.Request, forCandidate: true));
        }
    }

    public async Task Handle(InterviewRescheduled e, CancellationToken ct)
    {
        var info = await Load(e.InterviewId, ct);
        var key = $"{info.Interview.Id}:{e.OccurredAt.UtcTicks}";

        await notifier.Notify(
            e.InterviewerUserIds,
            NotificationType.InterviewUpdated,
            $"Mülakat güncellendi: {info.Candidate} · {info.Type}",
            $"{TurkishTime.Range(info.Interview.StartsAt, info.Interview.EndsAt)} · {info.Location}",
            $"/interviews/{info.Interview.Id}",
            $"iv-updated:{key}",
            ct,
            _ => Invite(info, CalendarMethod.Request, forCandidate: false));

        // Mülakattan çıkarılan mülakatçının takvimindeki kayıt silinir.
        var removed = e.PreviousInterviewerUserIds.Except(e.InterviewerUserIds).ToList();
        await notifier.Notify(
            removed,
            NotificationType.InterviewCancelled,
            $"Mülakattan çıkarıldınız: {info.Candidate} · {info.Type}",
            $"{TurkishTime.DateTime(e.PreviousStartsAt)} tarihli mülakata artık katılmayacaksınız.",
            null,
            $"iv-removed:{key}",
            ct,
            _ => Invite(info, CalendarMethod.Cancel, forCandidate: false));

        if (info.Interview.NotifyCandidate)
        {
            await notifier.EmailCandidate(
                info.Interview.CandidateId, EmailTemplateCodes.InterviewUpdatedCandidate, CandidateValues(info),
                $"iv-updated-candidate:{key}", ct, Invite(info, CalendarMethod.Request, forCandidate: true));
        }
    }

    public async Task Handle(InterviewCancelled e, CancellationToken ct)
    {
        var info = await Load(e.InterviewId, ct);

        await notifier.Notify(
            e.InterviewerUserIds,
            NotificationType.InterviewCancelled,
            $"Mülakat iptal edildi: {info.Candidate} · {info.Type}",
            $"{TurkishTime.DateTime(info.Interview.StartsAt)} · Neden: {info.Interview.CancellationReason}",
            $"/interviews/{info.Interview.Id}",
            $"iv-cancelled:{info.Interview.Id}",
            ct,
            _ => Invite(info, CalendarMethod.Cancel, forCandidate: false));

        if (info.Interview.NotifyCandidate)
        {
            await notifier.EmailCandidate(
                info.Interview.CandidateId, EmailTemplateCodes.InterviewCancelledCandidate, CandidateValues(info),
                $"iv-cancelled-candidate:{info.Interview.Id}", ct, Invite(info, CalendarMethod.Cancel, forCandidate: true));
        }
    }

    public async Task Handle(InterviewReminderDue e, CancellationToken ct)
    {
        var info = await Load(e.InterviewId, ct);
        var when = e.HoursBefore == 1 ? "1 saat" : "24 saat";

        await notifier.Notify(
            e.InterviewerUserIds,
            NotificationType.InterviewReminder,
            $"Mülakata {when} kaldı: {info.Candidate} · {info.Type}",
            $"{TurkishTime.Range(info.Interview.StartsAt, info.Interview.EndsAt)} · {info.Location}",
            $"/interviews/{info.Interview.Id}",
            $"iv-reminder:{info.Interview.Id}:{e.HoursBefore}:{e.StartsAt.UtcTicks}",
            ct);
    }

    private sealed record InterviewInfo(
        Interview Interview, string Candidate, string Position, string Type, string Location,
        IReadOnlyList<CalendarAttendee> Interviewers, CalendarAttendee? CandidateAttendee);

    private async Task<InterviewInfo> Load(Guid interviewId, CancellationToken ct)
    {
        var interview = await db.Interviews.AsNoTracking().FirstAsync(i => i.Id == interviewId, ct);
        var candidate = await db.Candidates.AsNoTracking()
            .Where(c => c.Id == interview.CandidateId)
            .Select(c => new { Name = c.FirstName + " " + c.LastName, c.Email })
            .FirstAsync(ct);
        var position = await db.Positions.Where(p => p.Id == interview.PositionId).Select(p => p.Title).FirstAsync(ct);
        var names = await db.ItemNames([interview.TypeId, .. interview.RoomId is { } roomId ? [roomId] : Array.Empty<Guid>()], ct);
        var interviewerIds = interview.InterviewerUserIds.ToList();
        var interviewers = await db.Users.AsNoTracking()
            .Where(u => interviewerIds.Contains(u.Id) && u.Email != null)
            .Select(u => new CalendarAttendee(u.DisplayName, u.Email!))
            .ToListAsync(ct);

        var location = interview.Mode == InterviewMode.Online
            ? interview.MeetingUrl ?? "Online (bağlantı ayrıca iletilecek)"
            : interview.RoomId is { } room ? names.GetValueOrDefault(room, "Yüz yüze") : "Yüz yüze";

        return new InterviewInfo(
            interview,
            candidate.Name,
            position,
            names.GetValueOrDefault(interview.TypeId, "Mülakat"),
            location,
            interviewers,
            string.IsNullOrWhiteSpace(candidate.Email) ? null : new CalendarAttendee(candidate.Name, candidate.Email));
    }

    // Adaya giden davette iç not ve diğer kişilerin bilgileri yer almaz.
    private CalendarAttachment Invite(InterviewInfo info, CalendarMethod method, bool forCandidate)
    {
        var interview = info.Interview;
        var content = ics.Create(new CalendarInvite(
            $"interview-{interview.Id}@ik-ise-alim",
            interview.CalendarSequence,
            method,
            interview.StartsAt,
            interview.EndsAt,
            forCandidate ? $"Mülakat: {info.Position}" : $"Mülakat: {info.Candidate} · {info.Type}",
            forCandidate ? null : interview.Note,
            info.Location,
            forCandidate ? interview.MeetingUrl : notifier.AbsoluteLink($"/interviews/{interview.Id}"),
            forCandidate
                ? info.CandidateAttendee is { } attendee ? [attendee] : []
                : info.Interviewers));
        return new CalendarAttachment(content, method);
    }

    private static Dictionary<string, string?> CandidateValues(InterviewInfo info) => new()
    {
        ["AdayAdi"] = info.Candidate,
        ["Pozisyon"] = info.Position,
        ["MulakatTarihi"] = TurkishTime.Range(info.Interview.StartsAt, info.Interview.EndsAt),
        ["Sure"] = $"{info.Interview.DurationMinutes} dakika",
        ["Yer"] = info.Location,
        ["MulakatTipi"] = info.Type,
    };
}
