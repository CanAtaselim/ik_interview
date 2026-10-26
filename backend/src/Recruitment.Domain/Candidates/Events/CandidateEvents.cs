using Recruitment.Domain.Common;

namespace Recruitment.Domain.Candidates.Events;

// Event'ler outbox'tan (kullanıcı oturumu olmadan) işlendiği için kim ve ne zaman bilgisini kendileri taşır.

public sealed record CandidateCreated(
    Guid CandidateId, Guid ActorUserId, DateTimeOffset OccurredAt, bool DespiteDuplicate) : IDomainEvent;

public sealed record CandidateDocumentAdded(
    Guid CandidateId, Guid DocumentId, string FileName, Guid ActorUserId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ScreeningStarted(
    Guid CandidateId, Guid ScreeningId, Guid ActorUserId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record ScreeningDeleted(
    Guid CandidateId, Guid ScreeningId, Guid ActorUserId, DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record CandidateTalentPoolChanged(
    Guid CandidateId, bool InTalentPool, Guid ActorUserId, DateTimeOffset OccurredAt) : IDomainEvent;
