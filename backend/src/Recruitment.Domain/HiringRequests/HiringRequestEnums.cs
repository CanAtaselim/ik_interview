namespace Recruitment.Domain.HiringRequests;

public enum HiringRequestStatus
{
    Draft,
    PendingApproval,
    Approved,
    Rejected,
    Cancelled,
}

public enum HiringReason
{
    NewHeadcount,
    Replacement,
}

public enum Urgency
{
    Low,
    Normal,
    High,
}

public enum ApprovalDecision
{
    Pending,
    Approved,
    Rejected,
}
