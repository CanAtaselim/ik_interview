using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Candidates;
using Recruitment.Application.Features.Positions;
using Recruitment.Domain.Pipeline;

namespace Recruitment.Application.Features.Pipeline.StartApplication;

/// <summary>Adayı bir pozisyona bağlar; süreç ilk aşamadan başlar.</summary>
public sealed record StartApplicationCommand(Guid CandidateId, Guid PositionId) : ICommand<Guid>;

internal sealed class StartApplicationCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<StartApplicationCommand, Guid>
{
    public async Task<Result<Guid>> Handle(StartApplicationCommand command, CancellationToken ct)
    {
        if (!await db.Candidates.AnyAsync(c => c.Id == command.CandidateId, ct))
            return CandidateErrors.NotFound;

        var position = await db.Positions.AsNoTracking().FirstOrDefaultAsync(p => p.Id == command.PositionId, ct);
        if (position is null)
            return PositionErrors.NotFound;
        if (!position.AcceptsCandidates)
            return PipelineErrors.PositionNotOpen;

        var alreadyInProcess = await db.JobApplications.AnyAsync(
            a => a.CandidateId == command.CandidateId &&
                 a.PositionId == command.PositionId &&
                 a.Status == ApplicationStatus.Active,
            ct);
        if (alreadyInProcess)
            return PipelineErrors.AlreadyInProcess;

        var template = await db.DefaultPipelineTemplate(ct);
        if (template is null)
            return PipelineErrors.TemplateMissing;

        var application = JobApplication.Start(
            command.CandidateId, command.PositionId, template.FirstStage, currentUser.UserId, clock);

        db.JobApplications.Add(application);
        await db.SaveChangesAsync(ct);
        return application.Id;
    }
}
