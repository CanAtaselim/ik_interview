using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Blacklist;
using Recruitment.Application.Features.Candidates;
using Recruitment.Application.Features.Dictionaries;
using Recruitment.Application.Features.Pipeline;
using Recruitment.Domain.Audit;
using Recruitment.Domain.CvReviews;
using Recruitment.Domain.Dictionaries;
using Recruitment.Domain.Pipeline;

namespace Recruitment.Application.Features.CvReviews.SuggestCandidate;

/// <summary>
/// Adayın CV'sini bir veya birden çok takım liderine değerlendirmesi için önerir.
/// </summary>
/// <param name="ApplicationId">Öneri bir pozisyon içinse adayın o pozisyondaki süreci.</param>
/// <param name="DepartmentId">Pozisyonsuz öneride birim; boşsa takım liderinin kendi birimi.</param>
public sealed record SuggestCandidateCommand(
    Guid CandidateId,
    Guid? ApplicationId,
    Guid? DepartmentId,
    IReadOnlyCollection<Guid> ReviewerUserIds,
    string? Note,
    bool ConfirmBlacklist = false) : ICommand<SuggestionResultDto>;

internal sealed class SuggestCandidateCommandValidator : AbstractValidator<SuggestCandidateCommand>
{
    public const int MaxReviewers = 10;

    public SuggestCandidateCommandValidator()
    {
        RuleFor(c => c.ReviewerUserIds)
            .NotEmpty().WithMessage("En az bir takım lideri seçmelisiniz.")
            .Must(ids => ids is null || ids.Count <= MaxReviewers)
            .WithMessage($"Aday tek seferde en fazla {MaxReviewers} takım liderine önerilebilir.");
        RuleFor(c => c.Note)
            .MaximumLength(CvReview.TextMaxLength)
            .WithMessage($"Not en fazla {CvReview.TextMaxLength} karakter olabilir.");
    }
}

internal sealed class SuggestCandidateCommandHandler(
    IAppDbContext db,
    IBlacklistMatcher blacklist,
    IBusinessCalendar calendar,
    IOptions<RecruitmentOptions> options,
    ICurrentUser currentUser,
    TimeProvider clock) : ICommandHandler<SuggestCandidateCommand, SuggestionResultDto>
{
    public async Task<Result<SuggestionResultDto>> Handle(SuggestCandidateCommand command, CancellationToken ct)
    {
        var candidate = await db.Candidates.AsNoTracking().FirstOrDefaultAsync(c => c.Id == command.CandidateId, ct);
        if (candidate is null)
            return CandidateErrors.NotFound;

        // Pozisyon için öneride birim pozisyondan gelir.
        var departmentId = command.DepartmentId;
        if (command.ApplicationId is { } applicationId)
        {
            var application = await db.JobApplications.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == applicationId && a.CandidateId == candidate.Id, ct);
            if (application is null)
                return PipelineErrors.NotFound;
            if (application.Status != ApplicationStatus.Active)
                return CvReviewErrors.ApplicationNotActive;

            departmentId = await db.Positions
                .Where(p => p.Id == application.PositionId)
                .Select(p => (Guid?)p.DepartmentId)
                .FirstOrDefaultAsync(ct);
        }
        else if (departmentId is { } selected && !await db.IsSelectable(selected, DictionaryType.Department, ct))
        {
            return CvReviewErrors.Field("departmentId", "Geçerli bir birim seçmelisiniz.");
        }

        var reviewerIds = command.ReviewerUserIds.Distinct().ToList();
        var reviewers = await db.Users.AsNoTracking()
            .Include(u => u.Roles)
            .Where(u => reviewerIds.Contains(u.Id) && u.IsActive)
            .ToListAsync(ct);
        if (reviewers.Count != reviewerIds.Count || reviewers.Any(u => !u.IsTeamLead()))
            return CvReviewErrors.Field("reviewerUserIds", "Seçilen kişilerin tümü aktif takım lideri olmalı.");

        // Aynı lidere aynı pozisyon (veya pozisyonsuz) için kararı beklenen ikinci bir öneri açılmaz.
        var waitingReviewerId = await db.CvReviews
            .Where(r => r.CandidateId == candidate.Id &&
                        r.ApplicationId == command.ApplicationId &&
                        reviewerIds.Contains(r.ReviewerUserId) &&
                        (r.Decision == CvReviewDecision.Pending || r.Decision == CvReviewDecision.OnHold))
            .Select(r => (Guid?)r.ReviewerUserId)
            .FirstOrDefaultAsync(ct);
        if (waitingReviewerId is { } waitingId)
            return CvReviewErrors.AlreadySuggested(reviewers.First(u => u.Id == waitingId).DisplayName);

        // Kara liste engellemez; İK'nın onayı gerekir ve onay erişim kaydına yazılır.
        var matches = await blacklist.FindForCandidate(candidate, ct);
        if (matches.Count > 0 && !command.ConfirmBlacklist)
            return CvReviewErrors.BlacklistMatch(matches);

        var now = clock.GetUtcNow();
        var dueAt = await calendar.AddBusinessDays(now, options.Value.CvReviewSlaBusinessDays, ct);
        var escalateAt = await calendar.AddBusinessDays(dueAt, options.Value.CvReviewEscalationBusinessDays, ct);

        var reviews = reviewers
            .Select(reviewer => CvReview.Suggest(
                candidate.Id,
                command.ApplicationId,
                departmentId ?? reviewer.DepartmentId,
                reviewer.Id,
                currentUser.UserId,
                command.Note,
                dueAt,
                escalateAt,
                clock))
            .ToList();

        db.CvReviews.AddRange(reviews);
        if (matches.Count > 0)
        {
            db.AccessLogs.Add(AccessLog.Blacklist(
                AccessAction.BlacklistWarningAcknowledged, currentUser.UserId, candidate.Id, now));
        }
        await db.SaveChangesAsync(ct);
        return new SuggestionResultDto([.. reviews.Select(r => r.Id)]);
    }
}
