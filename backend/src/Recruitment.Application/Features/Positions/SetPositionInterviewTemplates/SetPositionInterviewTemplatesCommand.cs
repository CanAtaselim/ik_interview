using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Positions.SetPositionInterviewTemplates;

/// <summary>
/// Pozisyonun mülakat şablonlarını belirler (ör. teknik ve final için ayrı şablon). Mülakat
/// planlanınca tipine uyan şablon otomatik bağlanır.
/// </summary>
public sealed record SetPositionInterviewTemplatesCommand(Guid PositionId, IReadOnlyList<Guid> TemplateIds) : ICommand<Unit>;

internal sealed class SetPositionInterviewTemplatesCommandHandler(IAppDbContext db)
    : ICommandHandler<SetPositionInterviewTemplatesCommand, Unit>
{
    public const int MaxTemplates = 10;

    public async Task<Result<Unit>> Handle(SetPositionInterviewTemplatesCommand command, CancellationToken ct)
    {
        var position = await db.Positions.FirstOrDefaultAsync(p => p.Id == command.PositionId, ct);
        if (position is null)
            return PositionErrors.NotFound;

        var ids = (command.TemplateIds ?? []).Distinct().ToList();
        if (ids.Count > MaxTemplates)
            return Field($"Bir pozisyona en fazla {MaxTemplates} şablon bağlanabilir.");
        var activeCount = await db.InterviewTemplates.CountAsync(t => ids.Contains(t.Id) && t.IsActive, ct);
        if (activeCount != ids.Count)
            return Field("Yalnızca aktif şablonlar seçilebilir.");

        position.SetInterviewTemplates(ids);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }

    private static Error Field(string message) =>
        Error.Validation(new Dictionary<string, string[]> { ["templateIds"] = [message] });
}
