using Recruitment.Domain.Common;

namespace Recruitment.Domain.Blacklist;

public enum BlacklistStatus
{
    PendingApproval,
    Active,
    Expired,
    Removed,
}

/// <summary>
/// Adayın kara liste kaydı. İK Uzmanı talep eder, İK Yöneticisi onaylar (veya doğrudan ekler);
/// çıkarma yalnızca İK Yöneticisindedir ve gerekçe ister.
/// </summary>
public sealed class BlacklistEntry : AggregateRoot
{
    public const int DescriptionMaxLength = 2000;

    private BlacklistEntry() { }

    public Guid CandidateId { get; private set; }
    public Guid ReasonId { get; private set; }
    public string Description { get; private set; } = default!;

    /// <summary><c>null</c> ise kayıt kalıcıdır.</summary>
    public DateTimeOffset? ExpiresAt { get; private set; }

    public BlacklistStatus Status { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public Guid? ApprovedByUserId { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }
    public Guid? RemovedByUserId { get; private set; }
    public DateTimeOffset? RemovedAt { get; private set; }
    public string? RemovalReason { get; private set; }

    /// <param name="approveImmediately">İK Yöneticisi eklediğinde kayıt onay beklemeden yürürlüğe girer.</param>
    public static BlacklistEntry Request(
        Guid candidateId,
        Guid reasonId,
        string description,
        DateTimeOffset? expiresAt,
        Guid requestedByUserId,
        bool approveImmediately,
        TimeProvider clock)
    {
        if (reasonId == Guid.Empty)
            throw new DomainException("Kara liste nedeni seçmelisiniz.");
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("Kara listeye alma gerekçesini yazmalısınız.");

        var now = clock.GetUtcNow();
        if (expiresAt <= now)
            throw new DomainException("Bitiş tarihi gelecekte olmalı.");

        var entry = new BlacklistEntry
        {
            CandidateId = candidateId,
            ReasonId = reasonId,
            Description = description.Trim(),
            ExpiresAt = expiresAt,
            Status = BlacklistStatus.PendingApproval,
            RequestedByUserId = requestedByUserId,
        };
        if (approveImmediately)
            entry.Approve(requestedByUserId, clock);
        else
            entry.Raise(new BlacklistEntryRequested(entry.Id, candidateId, requestedByUserId));

        return entry;
    }

    /// <summary>Kayıt şu an yürürlükte mi (onaylı ve süresi dolmamış)?</summary>
    public bool IsEffective(TimeProvider clock) =>
        Status == BlacklistStatus.Active && (ExpiresAt is null || ExpiresAt > clock.GetUtcNow());

    public void Approve(Guid approvedByUserId, TimeProvider clock)
    {
        if (Status != BlacklistStatus.PendingApproval)
            throw new DomainException("Yalnızca onay bekleyen kara liste kaydı onaylanabilir.");

        Status = BlacklistStatus.Active;
        ApprovedByUserId = approvedByUserId;
        ApprovedAt = clock.GetUtcNow();
    }

    /// <summary>Onay bekleyen talebi reddeder veya yürürlükteki kaydı kaldırır.</summary>
    public void Remove(Guid removedByUserId, string reason, TimeProvider clock)
    {
        if (Status is not (BlacklistStatus.PendingApproval or BlacklistStatus.Active))
            throw new DomainException("Yalnızca onay bekleyen veya yürürlükteki kayıt kara listeden çıkarılabilir.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Kara listeden çıkarma gerekçesini yazmalısınız.");

        Status = BlacklistStatus.Removed;
        RemovedByUserId = removedByUserId;
        RemovedAt = clock.GetUtcNow();
        RemovalReason = reason.Trim();
    }

    /// <summary>Süresi dolan yürürlükteki kaydı sonlandırır; dolmadıysa bir şey yapmaz.</summary>
    public bool ExpireIfDue(TimeProvider clock)
    {
        if (Status != BlacklistStatus.Active || ExpiresAt is null || ExpiresAt > clock.GetUtcNow())
            return false;

        Status = BlacklistStatus.Expired;
        return true;
    }
}

/// <summary>İK Uzmanı kara liste talebi açtı; İK Yöneticisinin onayı bekleniyor.</summary>
public sealed record BlacklistEntryRequested(Guid EntryId, Guid CandidateId, Guid RequestedByUserId) : IDomainEvent;
