using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Candidates;

namespace Recruitment.Application.Features.Interviews.GetInterviewDetail;

/// <summary>Mülakat ve adayın kısıtlı görünümü (CV, ön görüşme özeti; ücret ve İK yorumu yok).</summary>
public sealed record GetInterviewDetailQuery(Guid Id) : IQuery<InterviewDetailDto>;

internal sealed class GetInterviewDetailQueryHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : IQueryHandler<GetInterviewDetailQuery, InterviewDetailDto>
{
    public async Task<Result<InterviewDetailDto>> Handle(GetInterviewDetailQuery query, CancellationToken ct)
    {
        var interview = await db.VisibleInterviews(currentUser).AsNoTracking().FirstOrDefaultAsync(i => i.Id == query.Id, ct);
        if (interview is null)
            return InterviewErrors.NotFound;

        var candidate = await db.LoadRestrictedCandidate(interview.CandidateId, ct);
        if (candidate is null)
            return CandidateErrors.NotFound;

        var names = await db.InterviewNames([interview], ct, [interview.ScheduledByUserId]);
        return new InterviewDetailDto(
            interview.ToListItem(names, currentUser.UserId, clock.GetUtcNow()),
            interview.Note,
            interview.CancellationReason,
            names.Users.GetValueOrDefault(interview.ScheduledByUserId, string.Empty),
            candidate,
            await LoadTemplate(interview.TemplateId, ct));
    }

    // Şablon pasif yapılmış olsa da mülakata bağlı kalır ve gösterilir.
    private async Task<InterviewQuestionSetDto?> LoadTemplate(Guid? templateId, CancellationToken ct)
    {
        if (templateId is null)
            return null;

        var template = await db.InterviewTemplates.AsNoTracking().FirstOrDefaultAsync(t => t.Id == templateId, ct);
        if (template is null)
            return null;

        var ids = template.OrderedQuestionIds;
        var dictionary = db.DictionaryItems;
        var questions = await db.Questions.AsNoTracking()
            .Where(q => ids.Contains(q.Id))
            .Select(q => new InterviewQuestionDto(
                q.Id, q.Text, q.Guidance,
                dictionary.Where(d => d.Id == q.CompetencyId).Select(d => d.Name).FirstOrDefault() ?? string.Empty,
                q.Type, q.Difficulty, q.Weight))
            .ToListAsync(ct);

        return new InterviewQuestionSetDto(
            template.Id, template.Name, [.. ids.Select(id => questions.FirstOrDefault(q => q.QuestionId == id)).OfType<InterviewQuestionDto>()]);
    }
}
