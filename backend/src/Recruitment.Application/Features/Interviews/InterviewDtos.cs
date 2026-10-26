using Recruitment.Application.Abstractions;
using Recruitment.Application.Features.Candidates;
using Recruitment.Domain.Interviews;

namespace Recruitment.Application.Features.Interviews;

public sealed record InterviewerDto(Guid UserId, string DisplayName);

/// <param name="IsMine">İsteği yapan kullanıcı bu mülakatın mülakatçısı mı?</param>
/// <param name="AwaitingOutcome">Başlangıç saati geçti, sonucu (yapıldı / gelmedi) girilmedi.</param>
public sealed record InterviewListItemDto(
    Guid Id,
    Guid ApplicationId,
    Guid CandidateId,
    string CandidateName,
    Guid PositionId,
    string PositionTitle,
    Guid TypeId,
    string TypeName,
    InterviewMode Mode,
    DateTimeOffset StartsAt,
    DateTimeOffset EndsAt,
    int DurationMinutes,
    Guid? RoomId,
    string? RoomName,
    string? MeetingUrl,
    InterviewStatus Status,
    IReadOnlyList<InterviewerDto> Interviewers,
    bool IsMine,
    bool AwaitingOutcome,
    Guid? TemplateId,
    bool NotifyCandidate);

public sealed record InterviewDetailDto(
    InterviewListItemDto Interview,
    string? Note,
    string? CancellationReason,
    string ScheduledByName,
    RestrictedCandidateDto Candidate,
    InterviewQuestionSetDto? Template);

/// <summary>Mülakatçının hazırlanması için şablondaki sorular ve beklenen cevaplar.</summary>
public sealed record InterviewQuestionSetDto(Guid Id, string Name, IReadOnlyList<InterviewQuestionDto> Questions);

public sealed record InterviewQuestionDto(
    Guid QuestionId, string Text, string? Guidance, string CompetencyName, Domain.Questions.QuestionType Type, int Difficulty, decimal Weight);

public enum ConflictKind
{
    /// <summary>Mülakatçının aynı saatte başka bir mülakatı var.</summary>
    Interviewer,

    /// <summary>Oda aynı saatte başka bir mülakat için ayrılmış.</summary>
    Room,
}

public sealed record ScheduleConflictDto(
    ConflictKind Kind, string Name, Guid InterviewId, string CandidateName, DateTimeOffset StartsAt, DateTimeOffset EndsAt);

public sealed record TimeRangeDto(DateTimeOffset StartsAt, DateTimeOffset EndsAt);

/// <summary>Planlama için bir kişinin (veya odanın) belirli aralıktaki müsaitliği ve dolu saatleri.</summary>
public sealed record BusyRangeDto(Guid InterviewId, string Title, DateTimeOffset StartsAt, DateTimeOffset EndsAt);

/// <param name="Outlook">Outlook takvimi okunabildi mi? <see cref="OutlookStatus.NotConfigured"/> ise arayüz bu bilgiyi hiç göstermez.</param>
/// <param name="OutlookBusy">Outlook'taki meşgul saatler (konu ve katılımcı bilgisi yok).</param>
public sealed record InterviewerScheduleDto(
    Guid UserId,
    string DisplayName,
    IReadOnlyList<TimeRangeDto> Available,
    IReadOnlyList<BusyRangeDto> Busy,
    OutlookStatus Outlook,
    IReadOnlyList<TimeRangeDto> OutlookBusy);

public sealed record PlanningScheduleDto(
    IReadOnlyList<InterviewerScheduleDto> Interviewers, IReadOnlyList<BusyRangeDto> RoomBusy);
