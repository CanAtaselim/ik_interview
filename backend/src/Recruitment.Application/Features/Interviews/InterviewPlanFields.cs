using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Domain.Dictionaries;
using Recruitment.Domain.Interviews;
using Recruitment.Domain.Questions;
using Recruitment.Domain.Users;

namespace Recruitment.Application.Features.Interviews;

/// <summary>Mülakat planlama ve yeniden planlama command'larının ortak alanları.</summary>
public interface IInterviewPlanFields
{
    Guid TypeId { get; }
    InterviewMode Mode { get; }
    DateTimeOffset StartsAt { get; }
    int DurationMinutes { get; }
    Guid? RoomId { get; }
    string? MeetingUrl { get; }
    string? Note { get; }
    IReadOnlyCollection<Guid> InterviewerUserIds { get; }

    /// <summary>Soru şablonu; planlamada boşsa pozisyonun şablonlarından mülakat tipine uyan seçilir.</summary>
    Guid? TemplateId { get; }

    /// <summary>Planlama, değişiklik ve iptal adaya .ics ekli e-postayla bildirilsin mi?</summary>
    bool NotifyCandidate { get; }

    /// <summary>Mülakatçının mülakattaki rolü (İK / Teknik); verilmeyenin rolü sistem rolünden belirlenir.</summary>
    IReadOnlyDictionary<Guid, InterviewerRole>? InterviewerRoles { get; }

    /// <summary>İK, mülakatçı veya oda çakışması uyarısını görüp yine de planlamak istediğini bildirdi.</summary>
    bool ConfirmConflicts { get; }
}

internal abstract class InterviewPlanValidator<T> : AbstractValidator<T>
    where T : IInterviewPlanFields
{
    protected InterviewPlanValidator()
    {
        RuleFor(c => c.TypeId).NotEmpty().WithMessage("Mülakat tipini seçmelisiniz.");
        RuleFor(c => c.Mode).IsInEnum().WithMessage("Mülakatın yüz yüze mi online mı olduğunu seçmelisiniz.");
        RuleFor(c => c.StartsAt).NotEmpty().WithMessage("Mülakatın tarih ve saatini seçmelisiniz.");
        RuleFor(c => c.DurationMinutes)
            .InclusiveBetween(Interview.MinDurationMinutes, Interview.MaxDurationMinutes)
            .WithMessage($"Mülakat süresi {Interview.MinDurationMinutes} ile {Interview.MaxDurationMinutes} dakika arasında olmalı.");
        RuleFor(c => c.InterviewerUserIds)
            .NotEmpty().WithMessage("En az bir mülakatçı seçmelisiniz.")
            .Must(ids => ids is null || ids.Distinct().Count() <= Interview.MaxInterviewers)
            .WithMessage($"Bir mülakata en fazla {Interview.MaxInterviewers} mülakatçı atanabilir.");
        RuleFor(c => c.MeetingUrl)
            .MaximumLength(Interview.MeetingUrlMaxLength)
            .WithMessage($"Bağlantı en fazla {Interview.MeetingUrlMaxLength} karakter olabilir.")
            .Must(BeWebAddress)
            .When(c => c.Mode == InterviewMode.Online && !string.IsNullOrWhiteSpace(c.MeetingUrl))
            .WithMessage("Toplantı bağlantısı https:// ile başlayan bir adres olmalı.");
        RuleFor(c => c.Note)
            .MaximumLength(Interview.TextMaxLength)
            .WithMessage($"Not en fazla {Interview.TextMaxLength} karakter olabilir.");
    }

    private static bool BeWebAddress(string? url) =>
        Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}

internal static class InterviewPlanChecks
{
    /// <summary>Mülakatçı olarak atanabilen roller (uygulamaya en az bir kez girmiş aktif kullanıcılar).</summary>
    public static readonly Role[] InterviewerRoles =
        [Role.TeamLead, Role.DepartmentManager, Role.HrSpecialist, Role.HrManager, Role.Executive];

    public static InterviewPlan ToPlan(this IInterviewPlanFields fields) => new(
        fields.TypeId,
        fields.Mode,
        fields.StartsAt,
        fields.DurationMinutes,
        fields.RoomId,
        fields.MeetingUrl,
        fields.Note,
        fields.InterviewerUserIds,
        fields.TemplateId,
        fields.NotifyCandidate,
        fields.InterviewerRoles);

    /// <summary>
    /// Rolü verilmeyen mülakatçıların rolünü sistem rolünden belirler: yalnızca İK rolü olanlar İK,
    /// takım lideri/yönetici rolü de olanlar teknik sayılır.
    /// </summary>
    public static async Task<InterviewPlan> WithInterviewerRoles(this IAppDbContext db, InterviewPlan plan, CancellationToken ct)
    {
        var ids = plan.InterviewerUserIds.Distinct().ToList();
        var userRoles = await db.Users.AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, Roles = u.Roles.Select(r => r.Role).ToList() })
            .ToListAsync(ct);

        var roles = new Dictionary<Guid, InterviewerRole>();
        foreach (var user in userRoles)
        {
            var isHr = user.Roles.Any(r => r is Role.HrSpecialist or Role.HrManager);
            var isTechnical = user.Roles.Any(r => r is Role.TeamLead or Role.DepartmentManager);
            roles[user.Id] = isHr && !isTechnical ? InterviewerRole.Hr : InterviewerRole.Technical;
        }

        if (plan.InterviewerRoles is not null)
        {
            foreach (var (id, role) in plan.InterviewerRoles.Where(entry => ids.Contains(entry.Key)))
                roles[id] = role;
        }

        return plan with { InterviewerRoles = roles };
    }

    /// <summary>Sözlük seçimleri ve mülakatçılar geçerli mi? Geçerliyse <c>null</c>.</summary>
    public static async Task<Error?> FindInvalidPlan(this IAppDbContext db, IInterviewPlanFields fields, CancellationToken ct)
    {
        if (!await db.IsSelectable(fields.TypeId, DictionaryType.InterviewType, ct))
            return InterviewErrors.Field("typeId", "Geçerli bir mülakat tipi seçmelisiniz.");

        if (fields.Mode == InterviewMode.InPerson && fields.RoomId is { } roomId &&
            !await db.IsSelectable(roomId, DictionaryType.Room, ct))
        {
            return InterviewErrors.Field("roomId", "Geçerli bir oda seçmelisiniz.");
        }

        if (fields.TemplateId is { } templateId && !await db.InterviewTemplates.AnyAsync(t => t.Id == templateId && t.IsActive, ct))
            return InterviewErrors.Field("templateId", "Geçerli (aktif) bir soru şablonu seçmelisiniz.");

        var ids = fields.InterviewerUserIds.Distinct().ToList();
        var validCount = await db.Users.CountAsync(
            u => ids.Contains(u.Id) && u.IsActive && u.Roles.Any(r => InterviewerRoles.Contains(r.Role)), ct);
        return validCount == ids.Count
            ? null
            : InterviewErrors.Field("interviewerUserIds", "Seçilen mülakatçıların tümü uygulamayı kullanan aktif çalışanlar olmalı.");
    }

    /// <summary>
    /// Pozisyonun şablonlarından mülakat tipine uyanı; tipi belirtilmemiş tek şablon varsa onu seçer.
    /// </summary>
    public static async Task<Guid?> SuggestTemplate(
        this IAppDbContext db, Guid positionId, Guid typeId, CancellationToken ct, InterviewerRole role = InterviewerRole.Technical)
    {
        var position = await db.Positions.AsNoTracking().FirstOrDefaultAsync(p => p.Id == positionId, ct);
        var attached = position?.InterviewTemplateIds.ToList() ?? [];

        var fromPosition = await Pick(
            db.InterviewTemplates.AsNoTracking().Where(t => attached.Contains(t.Id) && t.IsActive && t.TargetRole == role), typeId, ct);
        if (fromPosition is not null || role == InterviewerRole.Technical)
            return fromPosition;

        // İK değerlendirme soruları pozisyona özel olmak zorunda değildir; pozisyonda yoksa genel İK şablonu seçilir.
        return await Pick(db.InterviewTemplates.AsNoTracking().Where(t => t.IsActive && t.TargetRole == role), typeId, ct);
    }

    private static async Task<Guid?> Pick(IQueryable<InterviewTemplate> templates, Guid typeId, CancellationToken ct)
    {
        var found = await templates.Select(t => new { t.Id, t.InterviewTypeId }).ToListAsync(ct);

        var sameType = found.FirstOrDefault(t => t.InterviewTypeId == typeId);
        if (sameType is not null)
            return sameType.Id;

        var general = found.Where(t => t.InterviewTypeId is null).ToList();
        return general.Count == 1 ? general[0].Id : null;
    }

    /// <summary>Aynı saatte mülakatçılardan birinin veya odanın başka bir planlı mülakatı.</summary>
    public static async Task<List<ScheduleConflictDto>> FindConflicts(
        this IAppDbContext db, InterviewPlan plan, Guid? excludeInterviewId, CancellationToken ct)
    {
        var endsAt = plan.StartsAt.AddMinutes(plan.DurationMinutes);
        var interviewerIds = plan.InterviewerUserIds.Distinct().ToList();
        var roomId = plan.Mode == InterviewMode.InPerson ? plan.RoomId : null;

        var overlapping = await db.Interviews.AsNoTracking()
            .Where(i => i.Id != excludeInterviewId &&
                        i.Status == InterviewStatus.Scheduled &&
                        i.StartsAt < endsAt && plan.StartsAt < i.EndsAt &&
                        ((roomId != null && i.RoomId == roomId) ||
                         i.Interviewers.Any(x => interviewerIds.Contains(x.UserId))))
            .OrderBy(i => i.StartsAt)
            .ToListAsync(ct);
        if (overlapping.Count == 0)
            return [];

        var names = await db.InterviewNames(overlapping, ct);
        var conflicts = new List<ScheduleConflictDto>();
        foreach (var other in overlapping)
        {
            var candidateName = names.Candidates.GetValueOrDefault(other.CandidateId, string.Empty);
            conflicts.AddRange(other.InterviewerUserIds
                .Where(interviewerIds.Contains)
                .Select(userId => new ScheduleConflictDto(
                    ConflictKind.Interviewer, names.Users.GetValueOrDefault(userId, string.Empty), other.Id,
                    candidateName, other.StartsAt, other.EndsAt)));
            if (roomId is not null && other.RoomId == roomId)
            {
                conflicts.Add(new ScheduleConflictDto(
                    ConflictKind.Room, names.Dictionary.GetValueOrDefault(roomId.Value, string.Empty), other.Id,
                    candidateName, other.StartsAt, other.EndsAt));
            }
        }

        return conflicts;
    }
}
