using Recruitment.Domain.Common;
using Recruitment.Domain.Users;

namespace Recruitment.Domain.HiringRequests;

/// <summary>
/// Talebin onay sürecindeki bir adım. Adımlar talep onaya gönderilirken akış tanımından
/// kopyalanır; akış sonradan değişse de süren talepler etkilenmez.
/// </summary>
public sealed class ApprovalStep : Entity
{
    private ApprovalStep() { }

    internal ApprovalStep(int order, Role role)
    {
        Order = order;
        Role = role;
        Decision = ApprovalDecision.Pending;
    }

    public int Order { get; private set; }
    public Role Role { get; private set; }
    public ApprovalDecision Decision { get; private set; }
    public Guid? DecidedByUserId { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }

    internal void Decide(ApprovalDecision decision, Guid decidedByUserId, string? note, DateTimeOffset decidedAt)
    {
        Decision = decision;
        DecidedByUserId = decidedByUserId;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        DecidedAt = decidedAt;
    }
}
