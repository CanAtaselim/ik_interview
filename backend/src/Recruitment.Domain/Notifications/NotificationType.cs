namespace Recruitment.Domain.Notifications;

/// <summary>Bildirim türü; kullanıcı her tür için e-posta alıp almayacağını seçer.</summary>
public enum NotificationType
{
    HiringRequestAwaitingApproval,
    HiringRequestDecided,
    CvReviewRequested,
    CvReviewReminder,
    CvReviewEscalated,
    CvReviewDecided,
    InterviewInvitation,
    InterviewUpdated,
    InterviewCancelled,
    InterviewReminder,
    DebriefReady,
    ScorecardReopened,
    BlacklistApprovalPending,
}
