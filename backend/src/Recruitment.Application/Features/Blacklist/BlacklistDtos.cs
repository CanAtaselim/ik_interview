using Recruitment.Domain.Blacklist;

namespace Recruitment.Application.Features.Blacklist;

public sealed record BlacklistEntryListItemDto(
    Guid Id,
    Guid CandidateId,
    string CandidateName,
    string ReasonName,
    BlacklistStatus Status,
    DateTimeOffset? ExpiresAt,
    string? RequestedByName,
    DateTimeOffset CreatedAt);

/// <param name="CanApprove">İsteği yapan kullanıcı bu talebi onaylayabilir mi?</param>
/// <param name="CanRemove">İsteği yapan kullanıcı kaydı kara listeden çıkarabilir (veya talebi reddedebilir) mi?</param>
public sealed record BlacklistEntryDetailDto(
    Guid Id,
    Guid CandidateId,
    string CandidateName,
    string ReasonName,
    string Description,
    BlacklistStatus Status,
    DateTimeOffset? ExpiresAt,
    string? RequestedByName,
    DateTimeOffset CreatedAt,
    string? ApprovedByName,
    DateTimeOffset? ApprovedAt,
    string? RemovedByName,
    DateTimeOffset? RemovedAt,
    string? RemovalReason,
    bool CanApprove,
    bool CanRemove);
