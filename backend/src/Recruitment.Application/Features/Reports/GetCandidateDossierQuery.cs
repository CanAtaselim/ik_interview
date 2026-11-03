using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Candidates;
using Recruitment.Application.Features.Dashboard;
using Recruitment.Domain.Audit;
using Recruitment.Domain.Blacklist;
using Recruitment.Domain.Candidates;
using Recruitment.Domain.CvReviews;
using Recruitment.Domain.Interviews;
using Recruitment.Domain.Notifications;
using Recruitment.Domain.Pipeline;
using Recruitment.Domain.Scorecards;

namespace Recruitment.Application.Features.Reports;

/// <summary>Bir adayın sistemdeki tüm bilgilerini (profil, süreçler, görüşmeler, puanlar…) tek dosyada verir.</summary>
public sealed record GetCandidateDossierQuery(Guid CandidateId, ReportFormat Format) : IQuery<ReportFile>;

internal sealed class GetCandidateDossierQueryHandler(
    IAppDbContext db, ICurrentUser currentUser, IReportRenderer renderer, TimeProvider clock)
    : IQueryHandler<GetCandidateDossierQuery, ReportFile>
{
    public async Task<Result<ReportFile>> Handle(GetCandidateDossierQuery query, CancellationToken ct)
    {
        var candidate = await db.Candidates.AsNoTracking()
            .Include(c => c.Documents)
            .Include(c => c.Screenings)
            .FirstOrDefaultAsync(c => c.Id == query.CandidateId, ct);
        if (candidate is null)
            return CandidateErrors.NotFound;

        var applications = await db.JobApplications.AsNoTracking().Include(a => a.History)
            .Where(a => a.CandidateId == candidate.Id).OrderBy(a => a.StartedAt).ToListAsync(ct);
        var reviews = await db.CvReviews.AsNoTracking()
            .Where(r => r.CandidateId == candidate.Id).OrderBy(r => r.SuggestedAt).ToListAsync(ct);
        var interviews = await db.Interviews.AsNoTracking().Include(i => i.Interviewers)
            .Where(i => i.CandidateId == candidate.Id).OrderBy(i => i.StartsAt).ToListAsync(ct);
        var interviewIds = interviews.Select(i => i.Id).ToList();
        var scorecards = await db.Scorecards.AsNoTracking().Include(s => s.Items)
            .Where(s => interviewIds.Contains(s.InterviewId)).ToListAsync(ct);
        var blacklist = await db.BlacklistEntries.AsNoTracking()
            .Where(e => e.CandidateId == candidate.Id).OrderBy(e => e.CreatedAt).ToListAsync(ct);
        var timeline = await db.TimelineEntries.AsNoTracking()
            .Where(t => t.CandidateId == candidate.Id).OrderBy(t => t.OccurredAt).ToListAsync(ct);
        var emails = await db.OutgoingEmails.AsNoTracking()
            .Where(e => e.CandidateId == candidate.Id).OrderBy(e => e.CreatedAt).ToListAsync(ct);

        var lookups = await ReportLookups.Load(
            db,
            userIds:
            [
                currentUser.UserId,
                ..candidate.Documents.Select(d => (Guid?)d.UploadedByUserId),
                ..candidate.Screenings.Select(s => (Guid?)s.InterviewerUserId),
                ..applications.SelectMany(a => a.History).Select(h => (Guid?)h.ChangedByUserId),
                ..reviews.SelectMany(r => new Guid?[] { r.ReviewerUserId, r.SuggestedByUserId }),
                ..interviews.SelectMany(i => i.InterviewerUserIds.Select(id => (Guid?)id).Append(i.ScheduledByUserId).Append(i.DebriefByUserId)),
                ..scorecards.Select(s => (Guid?)s.InterviewerUserId),
                ..blacklist.SelectMany(e => new Guid?[] { e.RequestedByUserId, e.ApprovedByUserId, e.RemovedByUserId }),
                ..timeline.Select(t => t.ActorUserId),
            ],
            itemIds:
            [
                candidate.SourceId,
                ..candidate.Documents.Select(d => d.TypeId),
                ..applications.Select(a => a.RejectionReasonId),
                ..reviews.Select(r => r.RejectionReasonId),
                ..reviews.Select(r => r.DepartmentId),
                ..interviews.SelectMany(i => new Guid?[] { i.TypeId, i.RoomId }),
                ..scorecards.SelectMany(s => s.Items).Select(i => i.CompetencyId),
                ..blacklist.Select(e => (Guid?)e.ReasonId),
            ],
            positionIds: [.. applications.Select(a => a.PositionId), .. interviews.Select(i => i.PositionId)],
            ct);

        var now = clock.GetUtcNow();
        var fullName = $"{candidate.FirstName} {candidate.LastName}";
        var orderedScreenings = candidate.Screenings.OrderBy(s => s.CreatedAt).ToList();
        var document = new ReportDocument(
            $"Aday dosyası: {fullName}",
            "Adayın sistemdeki tüm kayıtları",
            [
                Profile(candidate, lookups, blacklist),
                Documents(candidate, lookups),
                .. orderedScreenings.Select((s, index) => Screening(s, index + 1, orderedScreenings.Count, lookups)),
                Applications(applications, lookups, now),
                StageHistory(applications, lookups),
                CvReviews(reviews, applications, lookups),
                Interviews(interviews, lookups),
                ScorecardSummary(scorecards, interviews, lookups),
                ScorecardDetails(scorecards, interviews, lookups),
                Blacklist(blacklist, lookups),
                Emails(emails),
                Timeline(timeline, lookups),
            ],
            now,
            lookups.User(currentUser.UserId));

        var detail = $"Aday dosyası ({query.Format.ToString().ToUpperInvariant()})";
        db.AccessLogs.Add(AccessLog.ReportExported(currentUser.UserId, candidate.Id, detail, now));
        await db.SaveChangesAsync(ct);

        var fileName = $"Aday-{ReportFormatter.FileSafe(fullName)}-{TimeZoneInfo.ConvertTime(now, TurkishTime.Zone):yyyyMMdd}";
        return renderer.Render(document, query.Format, fileName);
    }

    private static ReportSection Profile(Candidate c, ReportLookups lookups, IReadOnlyList<BlacklistEntry> blacklist)
    {
        var current = blacklist.FirstOrDefault(e => e.Status is BlacklistStatus.Active or BlacklistStatus.PendingApproval);
        return new ReportSection(
            "Aday bilgileri",
            [
                new("Ad soyad", $"{c.FirstName} {c.LastName}"),
                new("E-posta", c.Email),
                new("Telefon", c.Phone),
                new("Şehir", c.City),
                new("LinkedIn", c.LinkedInUrl),
                new("GitHub", c.GitHubUrl),
                new("Kaynak", c.SourceId is null ? null : lookups.Item(c.SourceId)),
                new("Etiketler / yetenekler", string.Join(", ", c.Tags)),
                new("Yetenek havuzunda", c.InTalentPool),
                new("Kayıt tarihi", c.CreatedAt),
                new("Kara liste", current is null ? "Yok" : ReportLabels.Of(current.Status)),
                new("İK notu", c.Notes),
            ]);
    }

    private static ReportSection Documents(Candidate c, ReportLookups lookups) =>
        new(
            "Belgeler",
            Table: new ReportTable(
                ["Dosya adı", "Tür", "Boyut (KB)", "Yükleyen", "Yüklenme"],
                [.. c.Documents.OrderBy(d => d.UploadedAt).Select(d => (IReadOnlyList<object?>)
                [
                    d.FileName,
                    d.TypeId is null ? CandidateMappings.CvTypeName : lookups.Item(d.TypeId),
                    Math.Round(d.SizeBytes / 1024.0, 1),
                    lookups.User(d.UploadedByUserId),
                    d.UploadedAt,
                ])]),
            Note: c.Documents.Count == 0
                ? "Yüklenmiş belge yok."
                : "Belge dosyalarının kendisi bu rapora eklenmez; yalnızca kayıtları listelenir.");

    private static ReportSection Screening(Screening s, int number, int total, ReportLookups lookups)
    {
        var educations = string.Join("; ", s.Educations.Select(e => string.Join(
            ", ", new[] { e.School, e.Department, e.Degree, e.GraduationYear?.ToString() }.Where(x => !string.IsNullOrWhiteSpace(x)))));
        var languages = string.Join(
            ", ", s.Languages.Select(l => $"{l.Language}{(l.Level is { } level ? $" ({ReportLabels.Of(level)})" : "")}"));
        var title = total > 1 ? $"İK ön görüşme formu ({number}/{total})" : "İK ön görüşme formu";

        return new ReportSection(
            title,
            [
                new("Görüşmeyi yapan", lookups.User(s.InterviewerUserId)),
                new("Kanal", s.Channel is { } channel ? ReportLabels.Of(channel) : null),
                new("Görüşme tarihi", s.InterviewedAt),
                new("Eğitim", educations),
                new("Eğitim notu", s.EducationNote),
                new("Yabancı diller", languages),
                new("Dil notu", s.LanguagesNote),
                new("Teknik yetenek etiketleri", string.Join(", ", s.TechnicalSkillTags)),
                new("Teknik yetkinlikler", s.TechnicalSkills),
                new("Sosyal yetkinlikler", s.SoftSkills),
                new("Deneyim özeti", s.ExperienceSummary),
                new("Motivasyon", s.Motivation),
                new("Toplam deneyim (ay)", s.TotalExperienceMonths),
                new("Alan deneyimi (ay)", s.FieldExperienceMonths),
                new("Ücret beklentisi", ReportLabels.Money(s.ExpectedSalary)),
                new("Ücret beklentisi üst sınır", s.ExpectedSalaryMax),
                new("Mevcut ücret", ReportLabels.Money(s.CurrentSalary)),
                new("İhbar süresi (hafta)", s.NoticePeriodWeeks),
                new("Çalışma modeli", s.WorkModel is { } model ? ReportLabels.Of(model) : null),
                new("Konum", string.Join(" / ", new[] { s.LocationProvince, s.LocationDistrict }.Where(x => !string.IsNullOrWhiteSpace(x)))),
                new("Konum notu", s.LocationNote),
                new("Taşınmaya açık", s.OpenToRelocation),
                new("Seyahat kısıtı var", s.HasTravelRestriction),
                new("Askerlik", s.MilitaryStatus is { } military ? ReportLabels.Of(military) : null),
                new("İK genel puanı (1-5)", s.OverallRating),
                new("İK yorumu", s.HrComment),
            ]);
    }

    private static ReportSection Applications(IReadOnlyList<JobApplication> applications, ReportLookups lookups, DateTimeOffset now) =>
        new(
            "Süreçler (başvurular)",
            Table: new ReportTable(
                ["Pozisyon", "Birim", "Seviye", "Şu anki aşama", "Durum", "Başlangıç", "Bitiş", "Süre (gün)", "Ret nedeni", "Kapanış notu"],
                [.. applications.Select(a => (IReadOnlyList<object?>)
                [
                    lookups.PositionTitle(a.PositionId),
                    lookups.PositionDepartment(a.PositionId),
                    lookups.PositionLevel(a.PositionId),
                    lookups.Stage(a.StageId),
                    ReportLabels.Of(a.Status),
                    a.StartedAt,
                    a.ClosedAt,
                    Math.Round(((a.ClosedAt ?? now) - a.StartedAt).TotalDays, 1),
                    a.RejectionReasonId is null ? null : lookups.Item(a.RejectionReasonId),
                    a.ClosingNote,
                ])]),
            Note: applications.Count == 0 ? "Adayın açık ya da kapanmış bir süreci yok." : null);

    private static ReportSection StageHistory(IReadOnlyList<JobApplication> applications, ReportLookups lookups) =>
        new(
            "Aşama geçmişi",
            Table: new ReportTable(
                ["Tarih", "Pozisyon", "Geçiş", "Önceki aşamada (gün)", "Değiştiren", "Not"],
                [.. applications.SelectMany(a => a.History.OrderBy(h => h.ChangedAt).Select(h => (IReadOnlyList<object?>)
                [
                    h.ChangedAt,
                    lookups.PositionTitle(a.PositionId),
                    $"{lookups.Stage(h.FromStageId)} → {lookups.Stage(h.ToStageId)}",
                    Math.Round((h.ChangedAt - h.PreviousStageEnteredAt).TotalDays, 1),
                    lookups.User(h.ChangedByUserId),
                    string.Join(" · ", new[]
                    {
                        h.ToStatus == ApplicationStatus.Active ? null : ReportLabels.Of(h.ToStatus),
                        h.BypassReason is { Length: > 0 } reason ? $"Atlama gerekçesi: {reason}" : null,
                        h.UndoneAt is { } undone ? $"Geri alındı: {ReportFormatter.Format(undone)}" : null,
                    }.Where(x => x is not null)),
                ]))]));

    private static ReportSection CvReviews(
        IReadOnlyList<CvReview> reviews, IReadOnlyList<JobApplication> applications, ReportLookups lookups) =>
        new(
            "CV değerlendirmeleri",
            Table: new ReportTable(
                ["Pozisyon", "Birim", "Değerlendiren", "Öneren", "Öneri notu", "Öneri tarihi", "Son tarih", "Karar", "Karar tarihi", "Gerekçe", "Ret nedeni"],
                [.. reviews.Select(r =>
                {
                    var positionId = applications.FirstOrDefault(a => a.Id == r.ApplicationId)?.PositionId;
                    return (IReadOnlyList<object?>)
                    [
                        positionId is { } id ? lookups.PositionTitle(id) : null,
                        r.DepartmentId is null ? null : lookups.Item(r.DepartmentId),
                        lookups.User(r.ReviewerUserId),
                        lookups.User(r.SuggestedByUserId),
                        r.SuggestionNote,
                        r.SuggestedAt,
                        r.DueAt,
                        DashboardLabels.Of(r.Decision),
                        r.DecidedAt,
                        r.DecisionReason,
                        r.RejectionReasonId is null ? null : lookups.Item(r.RejectionReasonId),
                    ];
                })]),
            Note: reviews.Count == 0 ? "CV değerlendirmesi yok." : null);

    private static ReportSection Interviews(IReadOnlyList<Interview> interviews, ReportLookups lookups) =>
        new(
            "Mülakatlar",
            Table: new ReportTable(
                ["Pozisyon", "Tür", "Başlangıç", "Süre (dk)", "Şekil", "Yer / bağlantı", "Mülakatçılar", "Planlayan", "Durum", "İptal nedeni", "Not", "Değerlendirme kararı", "Karar gerekçesi", "Karar veren", "Karar tarihi"],
                [.. interviews.Select(i => (IReadOnlyList<object?>)
                [
                    lookups.PositionTitle(i.PositionId),
                    lookups.Item(i.TypeId),
                    i.StartsAt,
                    i.DurationMinutes,
                    ReportLabels.Of(i.Mode),
                    i.Mode == InterviewMode.InPerson ? lookups.Item(i.RoomId) : i.MeetingUrl,
                    string.Join(", ", i.InterviewerUserIds.Select(id => lookups.User(id))),
                    lookups.User(i.ScheduledByUserId),
                    DashboardLabels.Of(i.Status),
                    i.CancellationReason,
                    i.Note,
                    i.DebriefDecision is { } decision ? ReportLabels.Of(decision) : null,
                    i.DebriefReason,
                    i.DebriefByUserId is null ? null : lookups.User(i.DebriefByUserId),
                    i.DebriefAt,
                ])]),
            Note: interviews.Count == 0 ? "Mülakat yok." : null);

    private static decimal? Average(Scorecard s) =>
        new WeightedAverageCalculator().Calculate(
            s.Items.Where(i => i.Score is not null).Select(i => new WeightedScore(i.Score!.Value, i.Weight)));

    private static string InterviewLabel(Guid interviewId, IReadOnlyList<Interview> interviews, ReportLookups lookups)
    {
        var interview = interviews.FirstOrDefault(i => i.Id == interviewId);
        return interview is null
            ? ReportFormatter.Empty
            : $"{lookups.PositionTitle(interview.PositionId)} · {ReportFormatter.Format(interview.StartsAt)}";
    }

    private static ReportSection ScorecardSummary(
        IReadOnlyList<Scorecard> scorecards, IReadOnlyList<Interview> interviews, ReportLookups lookups) =>
        new(
            "Değerlendirme formları (özet)",
            Table: new ReportTable(
                ["Mülakat", "Mülakatçı", "Durum", "Ağırlıklı puan", "Öneri", "Gönderim", "Genel not"],
                [.. scorecards.OrderBy(s => s.SubmittedAt).Select(s => (IReadOnlyList<object?>)
                [
                    InterviewLabel(s.InterviewId, interviews, lookups),
                    lookups.User(s.InterviewerUserId),
                    ReportLabels.Of(s.Status),
                    Average(s),
                    s.Recommendation is { } recommendation ? DashboardLabels.Of(recommendation) : null,
                    s.SubmittedAt,
                    s.GeneralNote,
                ])]),
            Note: scorecards.Count == 0 ? "Değerlendirme formu yok." : null);

    private static ReportSection ScorecardDetails(
        IReadOnlyList<Scorecard> scorecards, IReadOnlyList<Interview> interviews, ReportLookups lookups) =>
        new(
            "Değerlendirme formları (soru bazında)",
            Table: new ReportTable(
                ["Mülakat (mülakatçı)", "Soru / madde", "Yetkinlik", "Ağırlık", "Puan (1-5)", "Not"],
                [.. scorecards.OrderBy(s => s.SubmittedAt).SelectMany(s => s.OrderedItems.Select(i => (IReadOnlyList<object?>)
                [
                    $"{InterviewLabel(s.InterviewId, interviews, lookups)} ({lookups.User(s.InterviewerUserId)})",
                    i.Text,
                    i.CompetencyId is null ? null : lookups.Item(i.CompetencyId),
                    i.Weight,
                    i.Score,
                    i.Note,
                ]))]));

    private static ReportSection Blacklist(IReadOnlyList<BlacklistEntry> entries, ReportLookups lookups) =>
        new(
            "Kara liste kayıtları",
            Table: new ReportTable(
                ["Neden", "Açıklama", "Durum", "Talep eden", "Talep tarihi", "Onaylayan", "Onay tarihi", "Bitiş", "Kaldıran", "Kaldırma tarihi", "Kaldırma gerekçesi"],
                [.. entries.Select(e => (IReadOnlyList<object?>)
                [
                    lookups.Item(e.ReasonId),
                    e.Description,
                    ReportLabels.Of(e.Status),
                    lookups.User(e.RequestedByUserId),
                    e.CreatedAt,
                    e.ApprovedByUserId is null ? null : lookups.User(e.ApprovedByUserId),
                    e.ApprovedAt,
                    e.ExpiresAt,
                    e.RemovedByUserId is null ? null : lookups.User(e.RemovedByUserId),
                    e.RemovedAt,
                    e.RemovalReason,
                ])]),
            Note: entries.Count == 0 ? "Kara liste kaydı yok." : null);

    private static ReportSection Emails(IReadOnlyList<OutgoingEmail> emails) =>
        new(
            "Gönderilen e-postalar",
            Table: new ReportTable(
                ["Konu", "Alıcı", "Durum", "Oluşturma", "Gönderim"],
                [.. emails.Select(e => (IReadOnlyList<object?>)
                    [e.Subject, e.ToAddress, ReportLabels.Of(e.Status), e.CreatedAt, e.SentAt])]),
            Note: emails.Count == 0 ? "Adaya gönderilmiş e-posta yok." : null);

    private static ReportSection Timeline(IReadOnlyList<TimelineEntry> entries, ReportLookups lookups) =>
        new(
            "Zaman çizelgesi",
            Table: new ReportTable(
                ["Tarih", "Olay", "İşlemi yapan"],
                [.. entries.Select(t => (IReadOnlyList<object?>)
                    [t.OccurredAt, t.Summary, t.ActorUserId is null ? null : lookups.User(t.ActorUserId)])]));
}
