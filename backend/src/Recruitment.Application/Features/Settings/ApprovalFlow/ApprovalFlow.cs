using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Approvals;
using Recruitment.Domain.Users;

namespace Recruitment.Application.Features.Settings.ApprovalFlow;

/// <param name="AllowedRoles">Akışa konabilecek roller.</param>
public sealed record ApprovalFlowDto(IReadOnlyList<Role> Roles, IReadOnlyList<Role> AllowedRoles);

/// <summary>Kadro talebinin onay akışı: onaycı rollerin sırası.</summary>
public sealed record GetApprovalFlowQuery : IQuery<ApprovalFlowDto>;

internal sealed class GetApprovalFlowQueryHandler(IAppDbContext db) : IQueryHandler<GetApprovalFlowQuery, ApprovalFlowDto>
{
    public async Task<Result<ApprovalFlowDto>> Handle(GetApprovalFlowQuery query, CancellationToken ct)
    {
        var flow = await db.ApprovalFlowDefinitions.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Kind == ApprovalFlowKind.HiringRequest, ct);
        return flow is null
            ? SettingsErrors.ApprovalFlowMissing
            : new ApprovalFlowDto(flow.ApproverRoles, ApprovalFlowDefinition.AllowedApproverRoles);
    }
}

/// <summary>Akışı değiştirir. Yalnızca bundan sonra onaya gönderilen talepleri etkiler.</summary>
public sealed record UpdateApprovalFlowCommand(IReadOnlyList<Role> Roles) : ICommand<Unit>;

internal sealed class UpdateApprovalFlowCommandValidator : AbstractValidator<UpdateApprovalFlowCommand>
{
    public UpdateApprovalFlowCommandValidator() =>
        RuleFor(c => c.Roles).NotEmpty().WithMessage("Onay akışında en az bir adım olmalı.");
}

internal sealed class UpdateApprovalFlowCommandHandler(IAppDbContext db) : ICommandHandler<UpdateApprovalFlowCommand, Unit>
{
    public async Task<Result<Unit>> Handle(UpdateApprovalFlowCommand command, CancellationToken ct)
    {
        var flow = await db.ApprovalFlowDefinitions.Include(f => f.Steps)
            .FirstOrDefaultAsync(f => f.Kind == ApprovalFlowKind.HiringRequest, ct);
        if (flow is null)
            return SettingsErrors.ApprovalFlowMissing;

        flow.ReplaceSteps(command.Roles);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
