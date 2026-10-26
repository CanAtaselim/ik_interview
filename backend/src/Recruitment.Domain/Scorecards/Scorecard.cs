using Recruitment.Domain.Common;

namespace Recruitment.Domain.Scorecards;

/// <summary>
/// Değerlendirmedeki bir soru. Şablondan gelen sorunun metni o anki haliyle kopyalanır; soru
/// sonradan değişse de değerlendirme etkilenmez. Mülakatçının eklediği sorular "özel" sorudur.
/// </summary>
public sealed class ScorecardItem
{
    private ScorecardItem() { }

    internal ScorecardItem(
        Guid id, Guid? questionId, int? questionVersion, string text, string? guidance, Guid? competencyId, decimal weight, int order)
    {
        Id = id;
        QuestionId = questionId;
        QuestionVersion = questionVersion;
        Text = text;
        Guidance = guidance;
        CompetencyId = competencyId;
        Weight = weight;
        Order = order;
    }

    public Guid Id { get; private set; }

    /// <summary>Soru bankasındaki soru; mülakatçının eklediği soruda boş.</summary>
    public Guid? QuestionId { get; private set; }

    public int? QuestionVersion { get; private set; }
    public string Text { get; private set; } = default!;
    public string? Guidance { get; private set; }
    public Guid? CompetencyId { get; private set; }
    public decimal Weight { get; private set; }
    public int Order { get; private set; }
    public int? Score { get; private set; }
    public string? Note { get; private set; }

    public bool IsCustom => QuestionId is null;

    internal void Rate(int? score, string? note, int order)
    {
        if (score is { } value && (value < ScoreRubric.Min || value > ScoreRubric.Max))
            throw new DomainException($"Puan {ScoreRubric.Min} ile {ScoreRubric.Max} arasında olmalı.");

        Score = score;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        Order = order;
    }

    internal void Rewrite(string text, Guid? competencyId, decimal weight)
    {
        Text = text;
        CompetencyId = competencyId;
        Weight = weight;
    }
}

/// <summary>Bankadaki bir sorunun değerlendirmeye kopyalanacak hali.</summary>
public sealed record QuestionSnapshot(
    Guid QuestionId, int Version, string Text, string? Guidance, Guid CompetencyId, decimal Weight);

/// <summary>
/// Mülakatçının arayüzden gönderdiği madde. Değerlendirmede olmayan kimlik yeni özel soru demektir;
/// kimliği arayüz üretir ki otomatik kayıt aynı soruyu tekrar tekrar eklemesin.
/// </summary>
public sealed record ScorecardItemInput(
    Guid? Id, string? Text, Guid? CompetencyId, decimal? Weight, int? Score, string? Note);

/// <summary>
/// Bir mülakatçının bir mülakat için değerlendirmesi. Taslakken serbestçe düzenlenir; gönderilince
/// kilitlenir ve yalnızca İK Yöneticisi gerekçeyle yeniden açabilir.
/// </summary>
public sealed class Scorecard : AggregateRoot
{
    public const int TextMaxLength = 2000;
    public const int NoteMaxLength = 4000;
    public const int MaxItems = 60;
    public const decimal DefaultCustomWeight = 1m;

    private readonly List<ScorecardItem> _items = [];

    private Scorecard() { }

    public Guid InterviewId { get; private set; }
    public Guid InterviewerUserId { get; private set; }
    public ScorecardStatus Status { get; private set; }
    public Recommendation? Recommendation { get; private set; }
    public string? GeneralNote { get; private set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public string? ReopenReason { get; private set; }

    public IReadOnlyCollection<ScorecardItem> Items => _items;

    public IReadOnlyList<ScorecardItem> OrderedItems => [.. _items.OrderBy(i => i.Order)];

    public bool IsSubmitted => Status == ScorecardStatus.Submitted;

    /// <summary>Mülakatçı değerlendirmeye başlar; şablon soruları o anki halleriyle kopyalanır.</summary>
    public static Scorecard Start(Guid interviewId, Guid interviewerUserId, IReadOnlyList<QuestionSnapshot> questions)
    {
        var scorecard = new Scorecard
        {
            InterviewId = interviewId,
            InterviewerUserId = interviewerUserId,
            Status = ScorecardStatus.Draft,
        };
        scorecard._items.AddRange(questions.Select((q, index) =>
            new ScorecardItem(Guid.CreateVersion7(), q.QuestionId, q.Version, q.Text, q.Guidance, q.CompetencyId, q.Weight, index + 1)));
        return scorecard;
    }

    /// <summary>
    /// Taslağı kaydeder. Listede olmayan maddeler çıkarılır, değerlendirmede olmayan maddeler özel soru
    /// olarak eklenir; sıra listedeki sıradır. Şablondan gelen sorunun metni değiştirilemez.
    /// </summary>
    public void UpdateDraft(IReadOnlyList<ScorecardItemInput> items, string? generalNote, Recommendation? recommendation)
    {
        EnsureDraft();
        if (items.Count > MaxItems)
            throw new DomainException($"Bir değerlendirmede en fazla {MaxItems} soru olabilir.");

        var kept = new List<ScorecardItem>();
        for (var index = 0; index < items.Count; index++)
        {
            var input = items[index];
            var order = index + 1;
            var item = input.Id is { } id ? _items.FirstOrDefault(i => i.Id == id) : null;

            if (item is null)
            {
                var newId = input.Id is { } given && given != Guid.Empty ? given : Guid.CreateVersion7();
                item = new ScorecardItem(newId, null, null, RequireText(input.Text), null, input.CompetencyId,
                    ValidWeight(input.Weight), order);
            }
            else if (item.IsCustom)
            {
                item.Rewrite(RequireText(input.Text), input.CompetencyId, ValidWeight(input.Weight));
            }

            item.Rate(input.Score, input.Note, order);
            kept.Add(item);
        }

        _items.Clear();
        _items.AddRange(kept);
        GeneralNote = string.IsNullOrWhiteSpace(generalNote) ? null : generalNote.Trim();
        Recommendation = recommendation;
    }

    public void Submit(TimeProvider clock)
    {
        EnsureDraft();
        if (Recommendation is null)
            throw new DomainException("Göndermeden önce genel önerinizi seçmelisiniz.");
        if (_items.All(i => i.Score is null))
            throw new DomainException("Göndermeden önce en az bir soruya puan vermelisiniz.");

        var now = clock.GetUtcNow();
        Status = ScorecardStatus.Submitted;
        SubmittedAt = now;
        ReopenReason = null;
        Raise(new ScorecardSubmitted(Id, InterviewId, InterviewerUserId, Recommendation.Value, now));
    }

    /// <summary>Gönderilmiş değerlendirmeyi düzeltme için açar (yalnızca İK Yöneticisi, gerekçeyle).</summary>
    public void Reopen(string reason, Guid actorUserId, TimeProvider clock)
    {
        if (!IsSubmitted)
            throw new DomainException("Yalnızca gönderilmiş değerlendirme yeniden açılabilir.");
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Yeniden açma gerekçesini yazmalısınız.");

        Status = ScorecardStatus.Draft;
        SubmittedAt = null;
        ReopenReason = reason.Trim();
        Raise(new ScorecardReopened(Id, InterviewId, InterviewerUserId, ReopenReason, actorUserId, clock.GetUtcNow()));
    }

    public decimal? WeightedScore(IScoreCalculator calculator) =>
        calculator.Calculate(_items.Where(i => i.Score is not null).Select(i => new WeightedScore(i.Score!.Value, i.Weight)));

    private void EnsureDraft()
    {
        if (IsSubmitted)
            throw new DomainException("Gönderilmiş değerlendirme değiştirilemez.");
    }

    private static string RequireText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            throw new DomainException("Eklediğiniz sorunun metnini yazmalısınız.");
        return text.Trim();
    }

    private static decimal ValidWeight(decimal? weight)
    {
        var value = weight ?? DefaultCustomWeight;
        if (value is < 0.5m or > 5m)
            throw new DomainException("Ağırlık 0,5 ile 5 arasında olmalı.");
        return value;
    }
}
