using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.Pipeline;

namespace Recruitment.Application.Features.Pipeline;

internal static class PipelineQueries
{
    public static Task<PipelineTemplate?> DefaultPipelineTemplate(this IAppDbContext db, CancellationToken ct) =>
        db.PipelineTemplates.AsNoTracking().Include(t => t.Stages).FirstOrDefaultAsync(t => t.IsDefault, ct);

    /// <summary>Verilen aşamanın ait olduğu şablon (sürecin izlediği aşama sırası).</summary>
    public static Task<PipelineTemplate?> PipelineTemplateOfStage(this IAppDbContext db, Guid stageId, CancellationToken ct) =>
        db.PipelineTemplates.AsNoTracking()
            .Include(t => t.Stages)
            .FirstOrDefaultAsync(t => t.Stages.Any(s => s.Id == stageId), ct);

    public static Task<JobApplication?> FindApplication(this IAppDbContext db, Guid id, CancellationToken ct) =>
        db.JobApplications.Include(a => a.History).FirstOrDefaultAsync(a => a.Id == id, ct);
}
