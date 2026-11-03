using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dashboard;
using Recruitment.Domain.Audit;

namespace Recruitment.Application.Features.Reports;

/// <summary>Dashboard'daki tüm kartların verisini, ekrandaki filtrelerle Excel çalışma kitabı olarak verir.</summary>
public sealed record ExportDashboardQuery(DashboardFilter Filter) : IQuery<ReportFile>;

internal sealed class ExportDashboardQueryHandler(
    IDispatcher dispatcher, IAppDbContext db, ICurrentUser currentUser, IReportRenderer renderer, TimeProvider clock)
    : IQueryHandler<ExportDashboardQuery, ReportFile>
{
    private static readonly string[] Days = ["Pazartesi", "Salı", "Çarşamba", "Perşembe", "Cuma", "Cumartesi", "Pazar"];

    public async Task<Result<ReportFile>> Handle(ExportDashboardQuery query, CancellationToken ct)
    {
        var filter = query.Filter;
        var period = DashboardPeriod.Resolve(filter, clock);
        if (period.IsFailure)
            return period.Error;

        var summary = await dispatcher.Query(new GetDashboardSummaryQuery(filter), ct);
        var pipeline = await dispatcher.Query(new GetDashboardPipelineQuery(filter), ct);
        var requests = await dispatcher.Query(new GetDashboardRequestsQuery(filter), ct);
        var candidates = await dispatcher.Query(new GetDashboardCandidatesQuery(filter), ct);
        var cvReviews = await dispatcher.Query(new GetDashboardCvReviewsQuery(filter), ct);
        var interviews = await dispatcher.Query(new GetDashboardInterviewsQuery(filter), ct);
        var scorecards = await dispatcher.Query(new GetDashboardScorecardsQuery(filter), ct);
        var blacklist = await dispatcher.Query(new GetDashboardBlacklistQuery(filter), ct);

        foreach (var failed in new[] { summary.IsFailure ? summary.Error : null, pipeline.IsFailure ? pipeline.Error : null,
                     requests.IsFailure ? requests.Error : null, candidates.IsFailure ? candidates.Error : null,
                     cvReviews.IsFailure ? cvReviews.Error : null, interviews.IsFailure ? interviews.Error : null,
                     scorecards.IsFailure ? scorecards.Error : null, blacklist.IsFailure ? blacklist.Error : null })
        {
            if (failed is not null)
                return failed;
        }

        var start = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(period.Value.Start, TurkishTime.Zone).DateTime);
        var end = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(period.Value.End.AddTicks(-1), TurkishTime.Zone).DateTime);
        var subtitle = $"Dönem: {ReportFormatter.Format(start)} – {ReportFormatter.Format(end)}"
                       + (filter.DepartmentId is not null ? " · Birim filtresi" : "")
                       + (filter.PositionId is not null ? " · Pozisyon filtresi" : "");

        var s = summary.Value;
        var p = pipeline.Value;
        var r = requests.Value;
        var c = candidates.Value;
        var v = cvReviews.Value;
        var i = interviews.Value;
        var sc = scorecards.Value;
        var b = blacklist.Value;

        var sections = new List<ReportSection>
        {
            new("Özet", Fields:
            [
                new("Açık pozisyon", s.OpenPositions),
                new("Açık kadro", s.OpenHeadcount),
                new("Aktif süreç", s.ActiveApplications),
                new("Bekleyen CV değerlendirmesi", s.PendingCvReviews),
                new("Süresi geçen CV değerlendirmesi", s.OverdueCvReviews),
                new("Planlı mülakat", s.UpcomingInterviews),
                new("Sonucu girilmemiş mülakat", s.AwaitingOutcome),
                new("Yeni aday (dönem)", s.NewCandidates.Value),
                new("Yeni aday (önceki dönem)", s.NewCandidates.Previous),
                new("İşe alınan (dönem)", s.Hires.Value),
                new("İşe alınan (önceki dönem)", s.Hires.Previous),
                new("Tamamlanan mülakat (dönem)", s.InterviewsHeld.Value),
                new("Tamamlanan mülakat (önceki dönem)", s.InterviewsHeld.Previous),
            ]),
            Table("Süreç - Huni", ["Aşama", "Süreç sayısı"], p.Funnel.Select(x => new object?[] { x.Stage, x.Count })),
            Table("Süreç - Aşamada kalma", ["Aşama", "Ortalama süre (gün)", "Şu an bekleyen", "Bekleyenin ort. süresi (gün)"],
                p.StageDurations.Select(x => new object?[] { x.Stage, x.AverageDays, x.Waiting, x.AverageWaitingDays })),
            Table("Süreç - Aşamada ret", ["Aşama", "Ulaşan", "Sonlanan"], p.RejectionByStage.Select(x => new object?[] { x.Stage, x.Reached, x.Closed })),
            Table("Süreç - Ret nedenleri", ["Neden", "Süreç sayısı"], p.RejectionReasons.Select(x => new object?[] { x.Label, x.Count })),
            new("Süreler", Fields:
            [
                new("İşe alım süresi - ortalama (gün)", p.TimeToHire.AverageDays),
                new("İşe alım süresi - ortanca (gün)", p.TimeToHire.MedianDays),
                new("İşe alım süresi - işe alım sayısı", p.TimeToHire.Count),
                new("İşe alım süresi - önceki dönem ort. (gün)", p.TimeToHire.PreviousAverageDays),
                new("Kadro doldurma - ortalama (gün)", p.TimeToFill.AverageDays),
                new("Kadro doldurma - ortanca (gün)", p.TimeToFill.MedianDays),
                new("Kadro doldurma - dolan pozisyon", p.TimeToFill.Count),
                new("Kadro doldurma - önceki dönem ort. (gün)", p.TimeToFill.PreviousAverageDays),
            ]),
            Table("Birim bazında işe alım", ["Birim", "Ortalama süre (gün)", "İşe alım"],
                p.TimeToHireByDepartment.Select(x => new object?[] { x.Label, x.AverageDays, x.Count })),
            Table("Talep durumu", ["Durum", "Talep sayısı"], r.ByStatus.Select(x => new object?[] { x.Label, x.Count })),
            Table("Talep nedeni", ["Neden", "Talep sayısı"], r.ByReason.Select(x => new object?[] { x.Label, x.Count })),
            Table("Birim bazında açık kadro", ["Birim", "Açık kadro"], r.OpenByDepartment.Select(x => new object?[] { x.Label, x.Count })),
            Table("Açık pozisyonlar", ["Pozisyon", "Birim", "Kadro", "Dolan"],
                r.OpenPositions.Select(x => new object?[] { x.Title, x.Department, x.Headcount, x.Filled })),
            new("Talep özeti", Fields:
            [
                new("Onay bekleyen talep", r.PendingApproval),
                new("Ortalama onay süresi (gün)", r.AverageApprovalDays),
            ]),
            Table("Aday - Zaman içinde", [$"Tarih ({c.Granularity})", "Eklenen aday"], c.Timeline.Select(x => new object?[] { x.Date, x.Count })),
            Table("Aday - Kaynak", ["Kaynak", "Aday sayısı"], c.BySource.Select(x => new object?[] { x.Label, x.Count })),
            Table("Aday - Kaynak verimi", ["Kaynak", "Aday", "İşe alınan"], c.SourceConversion.Select(x => new object?[] { x.Source, x.Candidates, x.Hired })),
            Table("Aday - Etiketler", ["Etiket", "Aday sayısı"], c.TopTags.Select(x => new object?[] { x.Label, x.Count })),
            new("Aday özeti", Fields:
            [
                new("Toplam aday", c.Total),
                new("Yetenek havuzunda", c.InTalentPool),
                new("Dönemde eklenen", c.Added.Value),
                new("Önceki dönemde eklenen", c.Added.Previous),
            ]),
            Table("CV - Karar dağılımı", ["Karar", "Değerlendirme sayısı"], v.Decisions.Select(x => new object?[] { x.Label, x.Count })),
            Table("CV - Bekleyenler", ["Değerlendiren", "Bekleyen"], v.PendingByReviewer.Select(x => new object?[] { x.Label, x.Count })),
            new("CV özeti", Fields:
            [
                new("Önerilen CV", v.Suggested),
                new("Ortalama karar süresi (gün)", v.AverageDecisionDays),
                new("Önceki dönem ort. karar süresi (gün)", v.PreviousAverageDecisionDays),
                new("SLA uyumu (%)", v.SlaCompliancePercent),
                new("Süresi geçen", v.Overdue),
            ]),
            Table("Mülakat - Durum", ["Durum", "Mülakat sayısı"], i.ByStatus.Select(x => new object?[] { x.Label, x.Count })),
            Table("Mülakat - Tür", ["Tür", "Mülakat sayısı"], i.ByType.Select(x => new object?[] { x.Label, x.Count })),
            Table("Mülakat - Mülakatçı yükü", ["Mülakatçı", "Mülakat sayısı"], i.InterviewerLoad.Select(x => new object?[] { x.Label, x.Count })),
            Table("Mülakat - Yoğunluk", ["Gün", "Saat", "Mülakat sayısı"],
                i.Heatmap.OrderBy(x => x.Day).ThenBy(x => x.Hour).Select(x => new object?[] { Days[Math.Clamp(x.Day, 0, 6)], $"{x.Hour:00}:00", x.Count })),
            new("Mülakat özeti", Fields:
            [
                new("Dönemde mülakat", i.InPeriod),
                new("Planlı mülakat", i.Upcoming),
                new("Sonucu girilmemiş", i.AwaitingOutcome),
            ]),
            Table("Puan - Öneriler", ["Öneri", "Form sayısı"], sc.Recommendations.Select(x => new object?[] { x.Label, x.Count })),
            Table("Puan - Yetkinlik ortalamaları", ["Yetkinlik", "Ortalama puan", "Puanlanan madde"],
                sc.CompetencyAverages.Select(x => new object?[] { x.Label, x.Average, x.Count })),
            Table("Puan - Mülakatçı eğilimi", ["Mülakatçı", "Ortalama puan", "Form sayısı"],
                sc.InterviewerScoring.Select(x => new object?[] { x.Label, x.Average, x.Count })),
            Table("Puan - Bekleyen formlar", ["Mülakatçı", "Bekleyen form"], sc.PendingByInterviewer.Select(x => new object?[] { x.Label, x.Count })),
            new("Puan özeti", Fields:
            [
                new("Bekleyen form", sc.Pending),
                new("Gönderilen form", sc.Submitted),
            ]),
            Table("Kara liste - Nedenler", ["Neden", "Kayıt sayısı"], b.ByReason.Select(x => new object?[] { x.Label, x.Count })),
            new("Kara liste özeti", Fields:
            [
                new("Yürürlükte", b.Active),
                new("Onay bekleyen", b.PendingApproval),
                new("Dönemde eklenen", b.Added.Value),
                new("Önceki dönemde eklenen", b.Added.Previous),
            ]),
        };

        var now = clock.GetUtcNow();
        var generatedBy = (await ReportLookups.Load(db, [currentUser.UserId], [], [], ct)).User(currentUser.UserId);
        var document = new ReportDocument("İşe alım dashboard raporu", subtitle, sections, now, generatedBy);

        db.AccessLogs.Add(AccessLog.ReportExported(currentUser.UserId, null, "Dashboard raporu (Excel)", now));
        await db.SaveChangesAsync(ct);

        var fileName = $"Dashboard-{TimeZoneInfo.ConvertTime(now, TurkishTime.Zone):yyyyMMdd-HHmm}";
        return renderer.Render(document, ReportFormat.Xlsx, fileName);
    }

    private static ReportSection Table(string title, string[] columns, IEnumerable<object?[]> rows) =>
        new(title, Table: new ReportTable(columns, [.. rows.Select(row => (IReadOnlyList<object?>)row)]));
}
