using Recruitment.Domain.CvReviews;
using Recruitment.Domain.HiringRequests;
using Recruitment.Domain.Interviews;
using Recruitment.Domain.Scorecards;

namespace Recruitment.Application.Features.Dashboard;

/// <summary>Dashboard grafiklerinde gösterilen enum karşılıkları (arayüz dili Türkçe).</summary>
internal static class DashboardLabels
{
    public const string Unspecified = "Belirtilmemiş";

    public static string Of(HiringRequestStatus status) => status switch
    {
        HiringRequestStatus.Draft => "Taslak",
        HiringRequestStatus.PendingApproval => "Onay bekliyor",
        HiringRequestStatus.Approved => "Onaylandı",
        HiringRequestStatus.Rejected => "Reddedildi",
        _ => "İptal edildi",
    };

    public static string Of(HiringReason reason) => reason == HiringReason.NewHeadcount ? "Yeni kadro" : "İkame";

    public static string Of(CvReviewDecision decision) => decision switch
    {
        CvReviewDecision.Pending => "Bekliyor",
        CvReviewDecision.Positive => "Olumlu",
        CvReviewDecision.Negative => "Olumsuz",
        _ => "Beklemeye alındı",
    };

    public static string Of(InterviewStatus status) => status switch
    {
        InterviewStatus.Scheduled => "Planlandı",
        InterviewStatus.Completed => "Tamamlandı",
        InterviewStatus.Cancelled => "İptal edildi",
        _ => "Gelmedi",
    };

    public static string Of(Recommendation recommendation) => recommendation switch
    {
        Recommendation.StrongHire => "Kesinlikle işe al",
        Recommendation.Hire => "İşe al",
        Recommendation.Undecided => "Kararsız",
        Recommendation.NoHire => "İşe alma",
        _ => "Kesinlikle alma",
    };
}
