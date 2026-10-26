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
