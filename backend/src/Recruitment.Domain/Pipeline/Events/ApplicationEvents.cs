using Recruitment.Domain.Common;

namespace Recruitment.Domain.Pipeline.Events;

public sealed record ApplicationStarted(
    Guid ApplicationId, Guid CandidateId, Guid PositionId, Guid ActorUserId, DateTimeOffset OccurredAt) : IDomainEvent;

/// <param name="BypassReason">İK Yöneticisi bekleyen değerlendirmelere rağmen geçirdiyse gerekçesi.</param>
public sealed record ApplicationStageChanged(
    Guid ApplicationId,
    Guid CandidateId,
    Guid FromStageId,
    Guid ToStageId,
    Guid ActorUserId,
    DateTimeOffset OccurredAt,
    string? BypassReason = null) : IDomainEvent;

/// <summary>Süreç red veya aday çekilmesiyle sonlandı.</summary>
public sealed record ApplicationClosed(
    Guid ApplicationId, Guid CandidateId, ApplicationStatus Status, Guid ActorUserId, DateTimeOffset OccurredAt)
    : IDomainEvent;

public sealed record ApplicationChangeUndone(
    Guid ApplicationId, Guid CandidateId, Guid ActorUserId, DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>Aday işe alındı; pozisyonun doluluğu artar.</summary>
public sealed record ApplicationHired(Guid ApplicationId, Guid PositionId) : IDomainEvent;

/// <summary>İşe alım geri alındı; pozisyonun doluluğu azalır.</summary>
public sealed record ApplicationHireReverted(Guid ApplicationId, Guid PositionId) : IDomainEvent;
