namespace Recruitment.Domain.Interviews;

public enum InterviewMode
{
    InPerson,
    Online,
}

public enum InterviewStatus
{
    Scheduled,
    Completed,
    Cancelled,

    /// <summary>Aday mülakata gelmedi.</summary>
    NoShow,
}

/// <summary>
/// Mülakatçının mülakattaki rolü; hangi değerlendirme şablonunu ve sorularını göreceğini belirler.
/// </summary>
public enum InterviewerRole
{
    /// <summary>Teknik mülakatçı / takım lideri: birime göre teknik sorular.</summary>
    Technical,

    /// <summary>İK: İK değerlendirme soruları ve yorum alanı.</summary>
    Hr,
}
