using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Pipeline;

namespace Recruitment.Application.Features.Settings.PipelineStages;

/// <param name="CanBeOptional">Aşama atlanabilir yapılabilir mi? (Çıkış kuralı olan aşamalar yapılamaz.)</param>
/// <param name="ActiveCount">Aşamada şu an bekleyen aday sayısı.</param>
public sealed record StageSettingDto(
    Guid Id, string Name, StageKind Kind, int Order, bool IsOptional, bool CanBeOptional, int ActiveCount);

public sealed record PipelineSettingsDto(string Name, IReadOnlyList<StageSettingDto> Stages);

/// <summary>Süreç aşamaları ve her aşamadaki aday sayısı.</summary>
public sealed record GetPipelineSettingsQuery : IQuery<PipelineSettingsDto>;

internal sealed class GetPipelineSettingsQueryHandler(IAppDbContext db)
    : IQueryHandler<GetPipelineSettingsQuery, PipelineSettingsDto>
{
    public async Task<Result<PipelineSettingsDto>> Handle(GetPipelineSettingsQuery query, CancellationToken ct)
    {
        var template = await db.PipelineTemplates.AsNoTracking().Include(t => t.Stages).FirstOrDefaultAsync(t => t.IsDefault, ct);
        if (template is null)
            return SettingsErrors.PipelineMissing;

        var counts = await db.JobApplications.AsNoTracking()
            .Where(a => a.Status == ApplicationStatus.Active)
            .GroupBy(a => a.StageId)
            .Select(g => new { StageId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.StageId, x => x.Count, ct);

        return new PipelineSettingsDto(
            template.Name,
            [.. template.OrderedStages.Select(s => new StageSettingDto(
                s.Id, s.Name, s.Kind, s.Order, s.IsOptional, PipelineStageDefinition.CanBeOptional(s.Kind), counts.GetValueOrDefault(s.Id)))]);
    }
}

public sealed record StageEditInput(Guid Id, string Name, bool IsOptional);

/// <summary>
/// Aşamaların adını ve atlanabilirliğini değiştirir. Sıra ve türler sabittir; geçiş kuralları
/// aşamanın türüne bağlıdır.
/// </summary>
public sealed record UpdatePipelineStagesCommand(IReadOnlyList<StageEditInput> Stages) : ICommand<Unit>;

internal sealed class UpdatePipelineStagesCommandValidator : AbstractValidator<UpdatePipelineStagesCommand>
{
    public UpdatePipelineStagesCommandValidator()
    {
        RuleFor(c => c.Stages).NotEmpty().WithMessage("Aşamalar gönderilmeli.");
        RuleForEach(c => c.Stages).ChildRules(stage =>
            stage.RuleFor(s => s.Name)
                .NotEmpty().WithMessage("Aşama adını yazmalısınız.")
                .MaximumLength(100).WithMessage("Aşama adı en fazla 100 karakter olabilir."));
    }
}

internal sealed class UpdatePipelineStagesCommandHandler(IAppDbContext db) : ICommandHandler<UpdatePipelineStagesCommand, Unit>
{
    public async Task<Result<Unit>> Handle(UpdatePipelineStagesCommand command, CancellationToken ct)
    {
        var template = await db.PipelineTemplates.Include(t => t.Stages).FirstOrDefaultAsync(t => t.IsDefault, ct);
        if (template is null)
            return SettingsErrors.PipelineMissing;

        template.EditStages([.. command.Stages.Select(s => new StageEdit(s.Id, s.Name, s.IsOptional))]);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
