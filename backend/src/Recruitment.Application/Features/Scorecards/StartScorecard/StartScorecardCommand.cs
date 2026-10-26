using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Interviews;
using Recruitment.Domain.Interviews;
using Recruitment.Domain.Scorecards;

namespace Recruitment.Application.Features.Scorecards.StartScorecard;

/// <summary>
/// Mülakatçının değerlendirmesini açar; yoksa oluşturur (tekrar çağrılabilir). Mülakatın soru
/// şablonundaki sorular o anki halleriyle kopyalanır.
/// </summary>
public sealed record StartScorecardCommand(Guid InterviewId) : ICommand<Guid>;

internal sealed class StartScorecardCommandHandler(IAppDbContext db, ICurrentUser currentUser)
    : ICommandHandler<StartScorecardCommand, Guid>
{
    public async Task<Result<Guid>> Handle(StartScorecardCommand command, CancellationToken ct)
    {
        var interview = await db.VisibleInterviews(currentUser).AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == command.InterviewId, ct);
        if (interview is null)
            return InterviewErrors.NotFound;
        if (!interview.IsAssignedTo(currentUser.UserId))
            return ScorecardErrors.NotInterviewer;

        var existing = await db.Scorecards
            .Where(s => s.InterviewId == interview.Id && s.InterviewerUserId == currentUser.UserId)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(ct);
        if (existing is { } id)
            return id;

        if (interview.Status is InterviewStatus.Cancelled or InterviewStatus.NoShow)
            return ScorecardErrors.InterviewClosed;

        var scorecard = Scorecard.Start(interview.Id, currentUser.UserId, await Snapshot(interview.TemplateId, ct));
        db.Scorecards.Add(scorecard);
        await db.SaveChangesAsync(ct);
        return scorecard.Id;
    }

    // Şablon pasif yapılmış olsa da bağlı olduğu mülakatta kullanılır.
    private async Task<IReadOnlyList<QuestionSnapshot>> Snapshot(Guid? templateId, CancellationToken ct)
    {
        if (templateId is null)
            return [];

        var template = await db.InterviewTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == templateId, ct);
        if (template is null)
            return [];

        var ids = template.OrderedQuestionIds;
        var questions = await db.Questions.AsNoTracking()
            .Where(q => ids.Contains(q.Id))
            .Select(q => new QuestionSnapshot(q.Id, q.Version, q.Text, q.Guidance, q.CompetencyId, q.Weight))
            .ToListAsync(ct);
        return [.. ids.Select(id => questions.FirstOrDefault(q => q.QuestionId == id)).OfType<QuestionSnapshot>()];
    }
}
