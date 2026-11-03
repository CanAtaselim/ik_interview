using Recruitment.Domain.Blacklist;
using Recruitment.Domain.Candidates;
using Recruitment.Domain.CvReviews;
using Recruitment.Domain.HiringRequests;
using Recruitment.Domain.Interviews;
using Recruitment.Domain.Notifications;
using Recruitment.Domain.Pipeline;
using Recruitment.Domain.Positions;
using Recruitment.Domain.Scorecards;
using Recruitment.Domain.Users;

namespace Recruitment.Application.Features.Reports;

/// <summary>Rapor çıktılarında enum karşılıkları (arayüz dili Türkçe).</summary>
internal static class ReportLabels
{
    public static string Of(ApplicationStatus status) => status switch
    {
        ApplicationStatus.Active => "Süreçte",
        ApplicationStatus.Hired => "İşe alındı",
        ApplicationStatus.Rejected => "Elendi",
        _ => "Adaydan çekildi",
    };

    public static string Of(PositionStatus status) => status switch
    {
        PositionStatus.Open => "Açık",
        PositionStatus.OnHold => "Beklemede",
        PositionStatus.Closed => "Kapandı",
        _ => "İptal edildi",
    };

    public static string Of(Urgency urgency) => urgency switch
    {
        Urgency.Low => "Düşük",
        Urgency.Normal => "Normal",
        _ => "Yüksek",
    };

    public static string Of(ApprovalDecision decision) => decision switch
    {
        ApprovalDecision.Pending => "Bekliyor",
        ApprovalDecision.Approved => "Onayladı",
        _ => "Reddetti",
    };

    public static string Of(Role role) => role switch
    {
        Role.SystemAdmin => "Sistem yöneticisi",
        Role.HrManager => "İK yöneticisi",
        Role.HrSpecialist => "İK uzmanı",
        Role.TeamLead => "Takım lideri",
        Role.DepartmentManager => "Birim yöneticisi",
        _ => "Üst yönetim",
    };

    public static string Of(InterviewMode mode) => mode == InterviewMode.InPerson ? "Yüz yüze" : "Online";

    public static string Of(DebriefDecision decision) => decision switch
    {
        DebriefDecision.Proceed => "Devam",
        DebriefDecision.Hold => "Beklet",
        _ => "Ele",
    };

    public static string Of(ScorecardStatus status) => status switch
    {
        ScorecardStatus.Draft => "Taslak",
        ScorecardStatus.Submitted => "Gönderildi",
        _ => "Yeniden açıldı",
    };

    public static string Of(BlacklistStatus status) => status switch
    {
        BlacklistStatus.PendingApproval => "Onay bekliyor",
        BlacklistStatus.Active => "Yürürlükte",
        BlacklistStatus.Expired => "Süresi doldu",
        _ => "Kaldırıldı",
    };

    public static string Of(OutgoingEmailStatus status) => status switch
    {
        OutgoingEmailStatus.Pending => "Bekliyor",
        OutgoingEmailStatus.Sent => "Gönderildi",
        _ => "Gönderilemedi",
    };

    public static string Of(ScreeningChannel channel) => channel switch
    {
        ScreeningChannel.Phone => "Telefon",
        ScreeningChannel.InPerson => "Yüz yüze",
        ScreeningChannel.Teams => "Teams",
        _ => "Diğer",
    };

    public static string Of(WorkModel model) => model switch
    {
        WorkModel.Office => "Ofis",
        WorkModel.Hybrid => "Hibrit",
        WorkModel.Remote => "Uzaktan",
        _ => "Fark etmez",
    };

    public static string Of(MilitaryStatus status) => status switch
    {
        MilitaryStatus.Done => "Yaptı",
        MilitaryStatus.Postponed => "Tecilli",
        MilitaryStatus.Exempt => "Muaf",
        _ => "Uygulanamaz",
    };

    public static string Of(LanguageLevel level) => level switch
    {
        LanguageLevel.Beginner => "Başlangıç",
        LanguageLevel.Intermediate => "Orta",
        LanguageLevel.Advanced => "İleri",
        _ => "Ana dil",
    };

    public static string Money(Domain.Common.Money? money) =>
        money is null
            ? ReportFormatter.Empty
            : $"{money.Amount:#,0.##} {money.Currency} ({(money.IsNet ? "net" : "brüt")})";
}
