using Recruitment.Domain.Common;

namespace Recruitment.Domain.Interviews;

public sealed record InterviewScheduled(
    Guid InterviewId,
    Guid CandidateId,
    DateTimeOffset StartsAt,
    IReadOnlyList<Guid> InterviewerUserIds,
    Guid ActorUserId,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>Zaman, yer veya mülakatçılar değişti; eski ve yeni mülakatçılar bilgilendirilir.</summary>
public sealed record InterviewRescheduled(
    Guid InterviewId,
    Guid CandidateId,
    DateTimeOffset PreviousStartsAt,
    DateTimeOffset StartsAt,
    IReadOnlyList<Guid> PreviousInterviewerUserIds,
    IReadOnlyList<Guid> InterviewerUserIds,
    Guid ActorUserId,
    DateTimeOffset OccurredAt) : IDomainEvent;

public sealed record InterviewCancelled(
    Guid InterviewId,
    Guid CandidateId,
    IReadOnlyList<Guid> InterviewerUserIds,
    Guid ActorUserId,
    DateTimeOffset OccurredAt) : IDomainEvent;

/// <summary>Mülakat yapıldı (Completed) veya aday gelmedi (NoShow).</summary>
public sealed record InterviewOutcomeRecorded(
    Guid InterviewId, Guid CandidateId, InterviewStatus Status, Guid ActorUserId, DateTimeOffset OccurredAt)
    : IDomainEvent;

/// <summary>Mülakata <see cref="HoursBefore"/> saat kaldı; mülakatçılara hatırlatma gider.</summary>
public sealed record InterviewReminderDue(
    Guid InterviewId, IReadOnlyList<Guid> InterviewerUserIds, int HoursBefore, DateTimeOffset StartsAt) : IDomainEvent;

public sealed record InterviewDebriefRecorded(
    Guid InterviewId, Guid CandidateId, Scorecards.DebriefDecision Decision, Guid ActorUserId, DateTimeOffset OccurredAt)
    : IDomainEvent;
