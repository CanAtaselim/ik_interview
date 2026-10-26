namespace Recruitment.Domain.Candidates;

public enum ScreeningChannel
{
    Phone,
    InPerson,
    Teams,
}

public enum LanguageLevel
{
    Beginner,
    Intermediate,
    Advanced,
    Native,
}

public enum WorkModel
{
    Office,
    Hybrid,
    Remote,

    /// <summary>Aday çalışma şeklinde tercih belirtmiyor ("Fark etmez").</summary>
    Any,
}

public enum MilitaryStatus
{
    Done,
    Postponed,
    Exempt,
    NotApplicable,
}

// Öneri, karar, mülakat, puan, aşama değişikliği ve e-posta türleri ilgili modüllerle eklenecek.
public enum TimelineEntryType
{
    CandidateAdded,
    DocumentAdded,
    ScreeningStarted,
    ScreeningDeleted,
    AddedToTalentPool,
    RemovedFromTalentPool,
    ApplicationStarted,
    StageChanged,
    ApplicationClosed,
    ChangeUndone,
    CvSuggested,
    CvReviewed,
    InterviewScheduled,
    InterviewRescheduled,
    InterviewCancelled,
    InterviewCompleted,
    InterviewNoShow,
    ScorecardSubmitted,
    ScorecardReopened,
    DebriefRecorded,
    EmailSent,
}
