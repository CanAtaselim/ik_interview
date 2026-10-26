using Recruitment.Domain.Common;

namespace Recruitment.Domain.Pipeline;

public sealed class PipelineStageDefinition : Entity
{
    private PipelineStageDefinition() { }

    internal PipelineStageDefinition(string name, StageKind kind, int order, bool isOptional)
    {
        Name = name;
        Kind = kind;
        Order = order;
        IsOptional = isOptional;
    }

    public string Name { get; private set; } = default!;
    public StageKind Kind { get; private set; }
    public int Order { get; private set; }

    /// <summary>Opsiyonel aşama atlanabilir.</summary>
    public bool IsOptional { get; private set; }

    /// <summary>
    /// Yalnızca kural taşımayan aşamalar atlanabilir yapılabilir. CV değerlendirme ve mülakat
    /// aşamaları gibi çıkış koşulu olanlar atlanırsa o koşul da atlanırdı.
    /// </summary>
    public static bool CanBeOptional(StageKind kind) => kind is StageKind.CaseStudy or StageKind.ReferenceCheck;

    internal void Edit(string name, bool isOptional)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Aşama adı boş olamaz.");
        if (isOptional && !CanBeOptional(Kind))
            throw new DomainException($"“{Name}” aşaması atlanabilir yapılamaz; çıkışında uygulanan bir kural var.");

        Name = name.Trim();
        IsOptional = isOptional;
    }
}

/// <summary>Bir aşamanın yönetim ekranından değiştirilebilen alanları.</summary>
public sealed record StageEdit(Guid StageId, string Name, bool IsOptional);

/// <summary>Bir pozisyondaki sürecin sıralı aşamaları.</summary>
public sealed class PipelineTemplate : AggregateRoot
{
    private readonly List<PipelineStageDefinition> _stages = [];

    private PipelineTemplate() { }

    public string Name { get; private set; } = default!;
    public bool IsDefault { get; private set; }
    public IReadOnlyCollection<PipelineStageDefinition> Stages => _stages;

    public IReadOnlyList<PipelineStageDefinition> OrderedStages => [.. _stages.OrderBy(s => s.Order)];

    public PipelineStageDefinition FirstStage => OrderedStages[0];

    /// <summary>Varsayılan süreç: Başvuru → … → İşe alındı; case ve referans kontrolü atlanabilir.</summary>
    public static PipelineTemplate CreateDefault()
    {
        (string Name, StageKind Kind, bool IsOptional)[] stages =
        [
            ("Başvuru", StageKind.Applied, false),
            ("İK ön görüşme", StageKind.HrScreening, false),
            ("CV değerlendirme", StageKind.CvReview, false),
            ("Teknik mülakat", StageKind.TechnicalInterview, false),
            ("Case / ödev", StageKind.CaseStudy, true),
            ("Final mülakat", StageKind.FinalInterview, false),
            ("Referans kontrolü", StageKind.ReferenceCheck, true),
            ("Teklif", StageKind.Offer, false),
            ("İşe alındı", StageKind.Hired, false),
        ];

        var template = new PipelineTemplate { Name = "Standart süreç", IsDefault = true };
        template._stages.AddRange(stages.Select((s, index) =>
            new PipelineStageDefinition(s.Name, s.Kind, index + 1, s.IsOptional)));
        return template;
    }

    /// <summary>
    /// Aşamaların adını ve atlanabilirliğini günceller. Sıra ve türler sabittir: geçiş kuralları
    /// (CV kararı, mülakat değerlendirmeleri, işe alım) aşamanın türüne bağlıdır.
    /// </summary>
    public void EditStages(IReadOnlyList<StageEdit> edits)
    {
        if (edits.Count != _stages.Count || edits.Select(e => e.StageId).Distinct().Count() != _stages.Count ||
            edits.Any(e => FindStage(e.StageId) is null))
        {
            throw new DomainException("Aşamaların tümü gönderilmeli.");
        }

        var names = edits.Select(e => e.Name.Trim().ToLowerInvariant()).ToList();
        if (names.Distinct().Count() != names.Count)
            throw new DomainException("İki aşama aynı adı taşıyamaz.");

        foreach (var edit in edits)
            FindStage(edit.StageId)!.Edit(edit.Name, edit.IsOptional);
    }

    public PipelineStageDefinition? FindStage(Guid stageId) => _stages.FirstOrDefault(s => s.Id == stageId);

    /// <summary>İleri geçiş yalnızca sıradaki aşamaya veya aradaki opsiyonel aşamalar atlanarak yapılır.</summary>
    public TransitionCheck CheckAdvance(Guid fromStageId, Guid toStageId)
    {
        var from = FindStage(fromStageId);
        var to = FindStage(toStageId);
        if (from is null || to is null)
            return TransitionCheck.Denied("Seçilen aşama bu sürecin aşamalarından biri değil.");

        if (to.Order == from.Order)
            return TransitionCheck.Denied("Aday zaten bu aşamada.");
        if (to.Order < from.Order)
            return TransitionCheck.Denied(
                "Aday önceki bir aşamaya taşınamaz. Yanlışlıkla taşıdıysanız 10 dakika içinde “Geri al”ı kullanın.");

        var skipped = _stages
            .Where(s => s.Order > from.Order && s.Order < to.Order && !s.IsOptional)
            .OrderBy(s => s.Order)
            .FirstOrDefault();

        return skipped is null
            ? TransitionCheck.Allowed
            : TransitionCheck.Denied($"“{skipped.Name}” aşaması atlanamaz; aday önce o aşamadan geçmeli.");
    }
}
