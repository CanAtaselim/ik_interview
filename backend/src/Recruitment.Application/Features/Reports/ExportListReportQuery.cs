using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Candidates.GetCandidates;
using Recruitment.Application.Features.CvReviews.GetCvReviews;
using Recruitment.Application.Features.Dashboard;
using Recruitment.Domain.Audit;
using Recruitment.Domain.Interviews;
using Recruitment.Domain.Scorecards;

namespace Recruitment.Application.Features.Reports;

public enum ReportKind
{
    Candidates,
    Applications,
    HiringRequests,
    Positions,
    Interviews,
    CvReviews,
    Blacklist,
}

/// <summary>
/// Liste raporlarının ortak filtresi. Tarih aralığı ilgili kaydın ana tarihine uygulanır (aday için kayıt, süreç için başlangıç,
/// mülakat için başlama tarihi…). Birim ve pozisyon filtresi, birimle ilişkisi olmayan raporlarda (kara liste) yok sayılır.
/// Aşağıdaki ek filtreler, ilgili listenin ekranındaki filtrelerle aynı anlamdadır; o rapora ait olmayan filtre yok sayılır.
/// </summary>
public sealed record ReportFilter(DateOnly? From, DateOnly? To, Guid? DepartmentId, Guid? PositionId)
{
    /// <summary>Adaylarda ad, iletişim, etiket, CV içeriği; süreç ve CV değerlendirmesinde aday adı; kadro talebi ve pozisyonda başlık.</summary>
    public string? Search { get; init; }

    /// <summary>Raporun kendi durum listesinden bir değer (enum adı): süreç, kadro talebi, pozisyon, mülakat, kara liste; CV değerlendirmesinde Waiting/Decided/Overdue.</summary>
    public string? Status { get; init; }

    public Guid? SourceId { get; init; }
    public bool? InTalentPool { get; init; }
    public string? Tag { get; init; }

    /// <summary>Aday listesindeki süreç süzgeci (Active, None, Hired, Closed).</summary>
    public string? Process { get; init; }

    public bool? Blacklisted { get; init; }
    public string? City { get; init; }
    public Guid? InterviewerUserId { get; init; }
    public Guid? RoomId { get; init; }
}

/// <summary>Seçilen liste raporunu Excel dosyası olarak üretir.</summary>
public sealed record ExportListReportQuery(ReportKind Kind, ReportFilter Filter) : IQuery<ReportFile>;

internal sealed class ExportListReportQueryHandler(
    IAppDbContext db, ICurrentUser currentUser, IReportRenderer renderer, ICandidateTextSearch textSearch, TimeProvider clock)
    : IQueryHandler<ExportListReportQuery, ReportFile>
{
    public async Task<Result<ReportFile>> Handle(ExportListReportQuery query, CancellationToken ct)
    {
        var filter = query.Filter;
        if (filter.From is { } from && filter.To is { } to && to < from)
            return Error.Validation(new Dictionary<string, string[]> { ["to"] = ["Bitiş tarihi başlangıçtan önce olamaz."] });

        if (InvalidFilters(query.Kind, filter) is { } invalid)
            return Error.Validation(invalid);

        var range = new DateRange(filter.From, filter.To);
        var (title, table) = query.Kind switch
        {
            ReportKind.Candidates => ("Adaylar", await Candidates(filter, range, ct)),
            ReportKind.Applications => ("Süreçler (başvurular)", await Applications(filter, range, ct)),
            ReportKind.HiringRequests => ("Kadro talepleri", await HiringRequests(filter, range, ct)),
            ReportKind.Positions => ("Pozisyonlar", await Positions(filter, range, ct)),
            ReportKind.Interviews => ("Mülakatlar", await Interviews(filter, range, ct)),
            ReportKind.CvReviews => ("CV değerlendirmeleri", await CvReviews(filter, range, ct)),
            _ => ("Kara liste", await Blacklist(filter, range, ct)),
        };

        var now = clock.GetUtcNow();
        var me = await ReportLookups.Load(db, [currentUser.UserId], [], [], ct);
        var document = new ReportDocument(
            title,
            Describe(filter),
            [new ReportSection(title, Table: table, Note: table.Rows.Count == 0 ? "Seçilen filtrelere uyan kayıt yok." : $"{table.Rows.Count} kayıt")],
            now,
            me.User(currentUser.UserId));

        db.AccessLogs.Add(AccessLog.ReportExported(currentUser.UserId, null, $"Liste raporu: {title} (Excel)", now));
        await db.SaveChangesAsync(ct);

        var fileName = $"{ReportFormatter.FileSafe(title)}-{TimeZoneInfo.ConvertTime(now, TurkishTime.Zone):yyyyMMdd-HHmm}";
        return renderer.Render(document, ReportFormat.Xlsx, fileName);
    }

    /// <summary>Bilinmeyen durum / süreç değerlerini yok saymak yerine reddeder; yoksa rapor sessizce yanlış kapsamla üretilirdi.</summary>
    private static Dictionary<string, string[]>? InvalidFilters(ReportKind kind, ReportFilter filter)
    {
        var errors = new Dictionary<string, string[]>();
        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            var statusType = kind switch
            {
                ReportKind.Applications => typeof(Domain.Pipeline.ApplicationStatus),
                ReportKind.HiringRequests => typeof(Domain.HiringRequests.HiringRequestStatus),
                ReportKind.Positions => typeof(Domain.Positions.PositionStatus),
                ReportKind.Interviews => typeof(InterviewStatus),
                ReportKind.CvReviews => typeof(CvReviewStatusFilter),
                ReportKind.Blacklist => typeof(Domain.Blacklist.BlacklistStatus),
                _ => null,
            };
            if (statusType is not null && !IsDefined(statusType, filter.Status))
                errors["status"] = ["Geçersiz durum."];
        }

        if (kind == ReportKind.Candidates && !string.IsNullOrWhiteSpace(filter.Process) &&
            !IsDefined(typeof(CandidateProcessFilter), filter.Process))
            errors["process"] = ["Geçersiz süreç filtresi."];

        return errors.Count == 0 ? null : errors;
    }

    private static bool IsDefined(Type enumType, string value) =>
        Enum.TryParse(enumType, value, ignoreCase: true, out var parsed) && Enum.IsDefined(enumType, parsed!);

    private static T? Parse<T>(string? value) where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: true, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;

    private static string? Describe(ReportFilter filter)
    {
        var parts = new List<string>();
        if (filter.From is { } from || filter.To is not null)
            parts.Add($"Tarih: {(filter.From is { } f ? ReportFormatter.Format(f) : "…")} – {(filter.To is { } t ? ReportFormatter.Format(t) : "…")}");
        if (filter.DepartmentId is not null)
            parts.Add("Birim filtresi uygulandı");
        if (filter.PositionId is not null)
            parts.Add("Pozisyon filtresi uygulandı");
        if (!string.IsNullOrWhiteSpace(filter.Status))
            parts.Add($"Durum: {filter.Status.Trim()}");
        if (!string.IsNullOrWhiteSpace(filter.Search))
            parts.Add($"Arama: “{filter.Search.Trim()}”");
        if (filter.SourceId is not null || filter.InTalentPool is not null || !string.IsNullOrWhiteSpace(filter.Tag) ||
            !string.IsNullOrWhiteSpace(filter.Process) || filter.Blacklisted is not null || !string.IsNullOrWhiteSpace(filter.City) ||
            filter.InterviewerUserId is not null || filter.RoomId is not null)
            parts.Add("Ek filtreler uygulandı");
        return parts.Count == 0 ? "Tüm kayıtlar" : string.Join(" · ", parts);
    }

    /// <summary>Türkiye gününe göre iki ucu dahil tarih aralığı; uç boşsa o yöne sınırsızdır.</summary>
    private sealed class DateRange(DateOnly? from, DateOnly? to)
    {
        private readonly DateTimeOffset? _start = from is { } f ? AtStartOfDay(f) : null;
        private readonly DateTimeOffset? _end = to is { } t ? AtStartOfDay(t.AddDays(1)) : null;

        public bool Contains(DateTimeOffset value) => (_start is null || value >= _start) && (_end is null || value < _end);

        private static DateTimeOffset AtStartOfDay(DateOnly day)
        {
            var local = day.ToDateTime(TimeOnly.MinValue);
            return new DateTimeOffset(local, TurkishTime.Zone.GetUtcOffset(local));
        }
    }

    private IQueryable<Domain.Pipeline.JobApplication> ScopedApplications(ReportFilter filter)
    {
        var applications = db.JobApplications.AsNoTracking();
        if (filter.PositionId is { } positionId)
            return applications.Where(a => a.PositionId == positionId);

        return filter.DepartmentId is { } departmentId
            ? applications.Where(a => db.Positions.Any(p => p.Id == a.PositionId && p.DepartmentId == departmentId))
            : applications;
    }

    private async Task<ReportTable> Candidates(ReportFilter filter, DateRange range, CancellationToken ct)
    {
        var candidates = textSearch.Apply(
            db.Candidates.AsNoTracking().Include(c => c.Documents).Include(c => c.Screenings), filter.Search);
        if (filter.DepartmentId is not null || filter.PositionId is not null)
        {
            var scoped = ScopedApplications(filter).Select(a => a.CandidateId);
            candidates = candidates.Where(c => scoped.Contains(c.Id));
        }

        if (filter.SourceId is { } sourceId)
            candidates = candidates.Where(c => c.SourceId == sourceId);
        if (filter.InTalentPool is { } inTalentPool)
            candidates = candidates.Where(c => c.InTalentPool == inTalentPool);
        if (!string.IsNullOrWhiteSpace(filter.Tag))
        {
            var tag = filter.Tag.Trim();
            candidates = candidates.Where(c => EF.Property<List<string>>(c, "_tags").Contains(tag));
        }

        if (Parse<CandidateProcessFilter>(filter.Process) is { } process)
        {
            var processes = db.JobApplications;
            candidates = process switch
            {
                CandidateProcessFilter.Active =>
                    candidates.Where(c => processes.Any(a => a.CandidateId == c.Id && a.Status == Domain.Pipeline.ApplicationStatus.Active)),
                CandidateProcessFilter.None => candidates.Where(c => !processes.Any(a => a.CandidateId == c.Id)),
                CandidateProcessFilter.Hired =>
                    candidates.Where(c => processes.Any(a => a.CandidateId == c.Id && a.Status == Domain.Pipeline.ApplicationStatus.Hired)),
                _ => candidates.Where(c => processes.Any(a => a.CandidateId == c.Id &&
                    (a.Status == Domain.Pipeline.ApplicationStatus.Rejected || a.Status == Domain.Pipeline.ApplicationStatus.Withdrawn))),
            };
        }

        if (filter.Blacklisted is { } blacklisted)
        {
            var entries = db.BlacklistEntries;
            candidates = candidates.Where(c =>
                entries.Any(e => e.CandidateId == c.Id && e.Status == Domain.Blacklist.BlacklistStatus.Active) == blacklisted);
        }

        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            var city = filter.City.Trim().ToLower();
            candidates = candidates.Where(c => c.City != null && c.City.ToLower().Contains(city));
        }

        var list = (await candidates.OrderBy(c => c.FirstName).ThenBy(c => c.LastName).ToListAsync(ct))
            .Where(c => range.Contains(c.CreatedAt))
            .ToList();
        var ids = list.Select(c => c.Id).ToList();
        var applications = await db.JobApplications.AsNoTracking().Where(a => ids.Contains(a.CandidateId)).ToListAsync(ct);
        var blacklist = await db.BlacklistEntries.AsNoTracking()
            .Where(e => ids.Contains(e.CandidateId) &&
                        (e.Status == Domain.Blacklist.BlacklistStatus.Active || e.Status == Domain.Blacklist.BlacklistStatus.PendingApproval))
            .ToListAsync(ct);
        var lookups = await ReportLookups.Load(
            db, [], list.Select(c => c.SourceId), applications.Select(a => a.PositionId), ct);

        return new ReportTable(
            ["Ad", "Soyad", "E-posta", "Telefon", "Şehir", "Kaynak", "Etiketler / yetenekler", "Yetenek havuzunda", "Kayıt tarihi", "Belge sayısı",
                "Toplam süreç", "Açık süreçler", "Ücret beklentisi", "Toplam deneyim (ay)", "Çalışma modeli", "İK genel puanı", "Kara liste"],
            [.. list.Select(c =>
            {
                var mine = applications.Where(a => a.CandidateId == c.Id).ToList();
                var open = mine.Where(a => a.Status == Domain.Pipeline.ApplicationStatus.Active)
                    .Select(a => $"{lookups.PositionTitle(a.PositionId)} ({lookups.Stage(a.StageId)})");
                var screening = c.Screenings.OrderByDescending(s => s.CreatedAt).FirstOrDefault();
                var entry = blacklist.FirstOrDefault(e => e.CandidateId == c.Id);
                return (IReadOnlyList<object?>)
                [
                    c.FirstName, c.LastName, c.Email, c.Phone, c.City,
                    c.SourceId is null ? null : lookups.Item(c.SourceId),
                    string.Join(", ", c.Tags), c.InTalentPool, c.CreatedAt, c.Documents.Count,
                    mine.Count, string.Join("; ", open),
                    screening is null ? null : ReportLabels.Money(screening.ExpectedSalary),
                    screening?.TotalExperienceMonths,
                    screening?.WorkModel is { } model ? ReportLabels.Of(model) : null,
                    screening?.OverallRating,
                    entry is null ? null : ReportLabels.Of(entry.Status),
                ];
            })]);
    }

    private async Task<ReportTable> Applications(ReportFilter filter, DateRange range, CancellationToken ct)
    {
        var applications = ScopedApplications(filter);
        if (Parse<Domain.Pipeline.ApplicationStatus>(filter.Status) is { } status)
            applications = applications.Where(a => a.Status == status);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var matching = db.Candidates.WhereAnyContains(filter.Search, c => c.FirstName + " " + c.LastName);
            applications = applications.Where(a => matching.Any(c => c.Id == a.CandidateId));
        }

        var list = (await applications.OrderBy(a => a.StartedAt).ToListAsync(ct))
            .Where(a => range.Contains(a.StartedAt)).ToList();
        var now = clock.GetUtcNow();
        var positionIds = list.Select(a => a.PositionId).Distinct().ToList();
        var responsible = await db.Positions.AsNoTracking().Where(p => positionIds.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id, p => p.ResponsibleHrUserId, ct);
        var lookups = await ReportLookups.Load(
            db, responsible.Values, list.Select(a => a.RejectionReasonId), positionIds, ct, list.Select(a => a.CandidateId));

        return new ReportTable(
            ["Aday", "Pozisyon", "Birim", "Seviye", "Aşama", "Durum", "Başlangıç", "Bitiş", "Süre (gün)", "Ret nedeni", "Kapanış notu", "Sorumlu İK"],
            [.. list.Select(a => (IReadOnlyList<object?>)
            [
                lookups.Candidate(a.CandidateId), lookups.PositionTitle(a.PositionId), lookups.PositionDepartment(a.PositionId),
                lookups.PositionLevel(a.PositionId), lookups.Stage(a.StageId), ReportLabels.Of(a.Status), a.StartedAt, a.ClosedAt,
                Math.Round(((a.ClosedAt ?? now) - a.StartedAt).TotalDays, 1),
                a.RejectionReasonId is null ? null : lookups.Item(a.RejectionReasonId), a.ClosingNote,
                lookups.User(responsible.GetValueOrDefault(a.PositionId)),
            ])]);
    }

    private async Task<ReportTable> HiringRequests(ReportFilter filter, DateRange range, CancellationToken ct)
    {
        var requests = db.HiringRequests.AsNoTracking().Include(r => r.ApprovalSteps).AsQueryable();
        if (filter.PositionId is { } positionId)
            requests = requests.Where(r => db.Positions.Any(p => p.Id == positionId && p.HiringRequestId == r.Id));
        else if (filter.DepartmentId is { } departmentId)
            requests = requests.Where(r => r.DepartmentId == departmentId);
        if (Parse<Domain.HiringRequests.HiringRequestStatus>(filter.Status) is { } status)
            requests = requests.Where(r => r.Status == status);
        requests = requests.WhereAnyContains(filter.Search, r => r.Title);

        var list = (await requests.OrderBy(r => r.CreatedAt).ToListAsync(ct)).Where(r => range.Contains(r.CreatedAt)).ToList();
        var lookups = await ReportLookups.Load(
            db,
            list.Select(r => (Guid?)r.RequestedByUserId),
            list.SelectMany(r => new Guid?[] { r.DepartmentId, r.LevelId }),
            [],
            ct);

        return new ReportTable(
            ["Başlık", "Birim", "Seviye", "Kadro sayısı", "Neden", "İkame edilen", "Aciliyet", "Beklenen başlangıç", "Talep eden", "Durum",
                "Bekleyen onay", "Oluşturma", "Gönderim", "Tamamlanma", "Onay adımları", "Aranan yetkinlikler", "Açıklama"],
            [.. list.Select(r => (IReadOnlyList<object?>)
            [
                r.Title, lookups.Item(r.DepartmentId), lookups.Item(r.LevelId), r.Headcount, DashboardLabels.Of(r.Reason),
                r.ReplacedEmployeeName, ReportLabels.Of(r.Urgency), r.ExpectedStartDate, lookups.User(r.RequestedByUserId),
                DashboardLabels.Of(r.Status), r.AwaitingRole is { } role ? ReportLabels.Of(role) : null, r.CreatedAt, r.SubmittedAt,
                r.CompletedAt,
                string.Join("; ", r.ApprovalSteps.OrderBy(s => s.Order).Select(s => $"{ReportLabels.Of(s.Role)}: {ReportLabels.Of(s.Decision)}")),
                string.Join(", ", r.Competencies), r.Description,
            ])]);
    }

    private async Task<ReportTable> Positions(ReportFilter filter, DateRange range, CancellationToken ct)
    {
        var positions = db.Positions.AsNoTracking();
        if (filter.PositionId is { } positionId)
            positions = positions.Where(p => p.Id == positionId);
        else if (filter.DepartmentId is { } departmentId)
            positions = positions.Where(p => p.DepartmentId == departmentId);
        if (Parse<Domain.Positions.PositionStatus>(filter.Status) is { } status)
            positions = positions.Where(p => p.Status == status);
        positions = positions.WhereAnyContains(filter.Search, p => p.Title);

        var list = (await positions.OrderBy(p => p.Title).ToListAsync(ct)).Where(p => range.Contains(p.CreatedAt)).ToList();
        var ids = list.Select(p => p.Id).ToList();
        var active = await db.JobApplications.AsNoTracking()
            .Where(a => ids.Contains(a.PositionId) && a.Status == Domain.Pipeline.ApplicationStatus.Active)
            .GroupBy(a => a.PositionId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var lookups = await ReportLookups.Load(
            db,
            list.Select(p => p.ResponsibleHrUserId),
            list.SelectMany(p => new Guid?[] { p.DepartmentId, p.LevelId }),
            [],
            ct);

        return new ReportTable(
            ["Pozisyon", "Birim", "Seviye", "Kadro", "Dolan", "Boş", "Durum", "Sorumlu İK", "Aktif süreç", "Oluşturma"],
            [.. list.Select(p => (IReadOnlyList<object?>)
            [
                p.Title, lookups.Item(p.DepartmentId), lookups.Item(p.LevelId), p.Headcount, p.FilledCount,
                Math.Max(0, p.Headcount - p.FilledCount), ReportLabels.Of(p.Status), lookups.User(p.ResponsibleHrUserId),
                active.GetValueOrDefault(p.Id), p.CreatedAt,
            ])]);
    }

    private async Task<ReportTable> Interviews(ReportFilter filter, DateRange range, CancellationToken ct)
    {
        var interviews = db.Interviews.AsNoTracking().Include(i => i.Interviewers).AsQueryable();
        if (filter.PositionId is { } positionId)
            interviews = interviews.Where(i => i.PositionId == positionId);
        else if (filter.DepartmentId is { } departmentId)
            interviews = interviews.Where(i => db.Positions.Any(p => p.Id == i.PositionId && p.DepartmentId == departmentId));
        if (Parse<InterviewStatus>(filter.Status) is { } status)
            interviews = interviews.Where(i => i.Status == status);
        if (filter.InterviewerUserId is { } interviewerId)
            interviews = interviews.Where(i => i.Interviewers.Any(x => x.UserId == interviewerId));
        if (filter.RoomId is { } roomId)
            interviews = interviews.Where(i => i.RoomId == roomId);

        var list = (await interviews.OrderBy(i => i.StartsAt).ToListAsync(ct)).Where(i => range.Contains(i.StartsAt)).ToList();
        var ids = list.Select(i => i.Id).ToList();
        var scorecards = await db.Scorecards.AsNoTracking().Include(s => s.Items)
            .Where(s => ids.Contains(s.InterviewId) && s.Status == ScorecardStatus.Submitted).ToListAsync(ct);
        var calculator = new WeightedAverageCalculator();
        var lookups = await ReportLookups.Load(
            db,
            list.SelectMany(i => i.InterviewerUserIds.Select(id => (Guid?)id)),
            list.SelectMany(i => new Guid?[] { i.TypeId, i.RoomId }),
            list.Select(i => i.PositionId),
            ct,
            list.Select(i => i.CandidateId));

        return new ReportTable(
            ["Aday", "Pozisyon", "Tür", "Başlangıç", "Süre (dk)", "Şekil", "Yer / bağlantı", "Mülakatçılar", "Durum", "İptal nedeni",
                "Değerlendirme kararı", "Gönderilen form", "Ortalama puan"],
            [.. list.Select(i =>
            {
                var forms = scorecards.Where(s => s.InterviewId == i.Id).ToList();
                var scores = forms
                    .Select(s => calculator.Calculate(s.Items.Where(x => x.Score is not null).Select(x => new WeightedScore(x.Score!.Value, x.Weight))))
                    .OfType<decimal>()
                    .ToList();
                return (IReadOnlyList<object?>)
                [
                    lookups.Candidate(i.CandidateId), lookups.PositionTitle(i.PositionId), lookups.Item(i.TypeId), i.StartsAt,
                    i.DurationMinutes, ReportLabels.Of(i.Mode),
                    i.Mode == InterviewMode.InPerson ? lookups.Item(i.RoomId) : i.MeetingUrl,
                    string.Join(", ", i.InterviewerUserIds.Select(id => lookups.User(id))), DashboardLabels.Of(i.Status),
                    i.CancellationReason, i.DebriefDecision is { } decision ? ReportLabels.Of(decision) : null, forms.Count,
                    scores.Count == 0 ? null : Math.Round(scores.Average(), 2),
                ];
            })]);
    }

    private async Task<ReportTable> CvReviews(ReportFilter filter, DateRange range, CancellationToken ct)
    {
        var reviews = db.CvReviews.AsNoTracking();
        if (filter.PositionId is { } positionId)
            reviews = reviews.Where(r => db.JobApplications.Any(a => a.Id == r.ApplicationId && a.PositionId == positionId));
        else if (filter.DepartmentId is { } departmentId)
            reviews = reviews.Where(r => r.DepartmentId == departmentId ||
                db.JobApplications.Any(a => a.Id == r.ApplicationId &&
                                            db.Positions.Any(p => p.Id == a.PositionId && p.DepartmentId == departmentId)));

        var now = clock.GetUtcNow();
        reviews = Parse<CvReviewStatusFilter>(filter.Status) switch
        {
            CvReviewStatusFilter.Waiting => reviews.Where(
                r => r.Decision == Domain.CvReviews.CvReviewDecision.Pending || r.Decision == Domain.CvReviews.CvReviewDecision.OnHold),
            CvReviewStatusFilter.Decided => reviews.Where(
                r => r.Decision == Domain.CvReviews.CvReviewDecision.Positive || r.Decision == Domain.CvReviews.CvReviewDecision.Negative),
            CvReviewStatusFilter.Overdue => reviews.Where(r => r.Decision == Domain.CvReviews.CvReviewDecision.Pending && r.DueAt < now),
            _ => reviews,
        };
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var matching = db.Candidates.WhereAnyContains(filter.Search, c => c.FirstName + " " + c.LastName);
            reviews = reviews.Where(r => matching.Any(c => c.Id == r.CandidateId));
        }

        var list = (await reviews.OrderBy(r => r.SuggestedAt).ToListAsync(ct)).Where(r => range.Contains(r.SuggestedAt)).ToList();
        var applicationIds = list.Select(r => r.ApplicationId).OfType<Guid>().ToList();
        var applicationPositions = await db.JobApplications.AsNoTracking()
            .Where(a => applicationIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.PositionId, ct);
        var lookups = await ReportLookups.Load(
            db,
            list.SelectMany(r => new Guid?[] { r.ReviewerUserId, r.SuggestedByUserId }),
            list.SelectMany(r => new Guid?[] { r.DepartmentId, r.RejectionReasonId }),
            applicationPositions.Values,
            ct,
            list.Select(r => r.CandidateId));

        return new ReportTable(
            ["Aday", "Pozisyon", "Birim", "Değerlendiren", "Öneren", "Öneri notu", "Öneri tarihi", "Son tarih", "Karar", "Karar tarihi", "Gerekçe", "Ret nedeni"],
            [.. list.Select(r => (IReadOnlyList<object?>)
            [
                lookups.Candidate(r.CandidateId),
                r.ApplicationId is { } applicationId && applicationPositions.TryGetValue(applicationId, out var positionId)
                    ? lookups.PositionTitle(positionId)
                    : null,
                r.DepartmentId is null ? null : lookups.Item(r.DepartmentId), lookups.User(r.ReviewerUserId), lookups.User(r.SuggestedByUserId),
                r.SuggestionNote, r.SuggestedAt, r.DueAt, DashboardLabels.Of(r.Decision), r.DecidedAt, r.DecisionReason,
                r.RejectionReasonId is null ? null : lookups.Item(r.RejectionReasonId),
            ])]);
    }

    private async Task<ReportTable> Blacklist(ReportFilter filter, DateRange range, CancellationToken ct)
    {
        var entries = db.BlacklistEntries.AsNoTracking();
        if (Parse<Domain.Blacklist.BlacklistStatus>(filter.Status) is { } status)
            entries = entries.Where(e => e.Status == status);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var matching = db.Candidates.WhereAnyContains(filter.Search, c => c.FirstName + " " + c.LastName);
            entries = entries.Where(e => matching.Any(c => c.Id == e.CandidateId));
        }

        var list = (await entries.OrderBy(e => e.CreatedAt).ToListAsync(ct))
            .Where(e => range.Contains(e.CreatedAt)).ToList();
        var lookups = await ReportLookups.Load(
            db,
            list.SelectMany(e => new Guid?[] { e.RequestedByUserId, e.ApprovedByUserId, e.RemovedByUserId }),
            list.Select(e => (Guid?)e.ReasonId),
            [],
            ct,
            list.Select(e => e.CandidateId));

        return new ReportTable(
            ["Aday", "Neden", "Açıklama", "Durum", "Talep eden", "Talep tarihi", "Onaylayan", "Onay tarihi", "Bitiş", "Kaldıran", "Kaldırma tarihi", "Kaldırma gerekçesi"],
            [.. list.Select(e => (IReadOnlyList<object?>)
            [
                lookups.Candidate(e.CandidateId), lookups.Item(e.ReasonId), e.Description, ReportLabels.Of(e.Status),
                lookups.User(e.RequestedByUserId), e.CreatedAt, e.ApprovedByUserId is null ? null : lookups.User(e.ApprovedByUserId),
                e.ApprovedAt, e.ExpiresAt, e.RemovedByUserId is null ? null : lookups.User(e.RemovedByUserId), e.RemovedAt, e.RemovalReason,
            ])]);
    }
}
