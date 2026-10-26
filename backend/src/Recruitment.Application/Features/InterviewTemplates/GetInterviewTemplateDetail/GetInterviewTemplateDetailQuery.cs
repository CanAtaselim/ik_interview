using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Questions;

namespace Recruitment.Application.Features.InterviewTemplates.GetInterviewTemplateDetail;

public sealed record GetInterviewTemplateDetailQuery(Guid Id) : IQuery<InterviewTemplateDetailDto>;

internal sealed class GetInterviewTemplateDetailQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IQueryHandler<GetInterviewTemplateDetailQuery, InterviewTemplateDetailDto>
{
    public async Task<Result<InterviewTemplateDetailDto>> Handle(GetInterviewTemplateDetailQuery query, CancellationToken ct)
    {
        var template = await db.VisibleTemplates(currentUser).AsNoTracking().FirstOrDefaultAsync(t => t.Id == query.Id, ct);
        if (template is null)
            return QuestionErrors.TemplateNotFound;

        var item = await db.InterviewTemplates.Where(t => t.Id == template.Id).ToListItems(db, currentUser).FirstAsync(ct);
        var ids = template.OrderedQuestionIds;
        // Şablondaki sorular, kullanıcının birimi dışında kalsalar bile (İK'nın eklediği genel sorular) gösterilir.
        var questions = await db.Questions.Where(q => ids.Contains(q.Id)).ToListItems(db, currentUser).ToListAsync(ct);

        return new InterviewTemplateDetailDto(item, [.. ids.Select(id => questions.FirstOrDefault(q => q.Id == id)).OfType<QuestionListItemDto>()]);
    }
}
