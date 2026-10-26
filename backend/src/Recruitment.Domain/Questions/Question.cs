using Recruitment.Domain.Common;

namespace Recruitment.Domain.Questions;

public enum QuestionType
{
    Technical,
    Behavioral,
    Case,
}

/// <summary>Sorunun düzenlenebilir içeriği; oluşturma ve düzenlemede aynı kurallar uygulanır.</summary>
public sealed record QuestionContent(
    Guid DepartmentId,
    string? PositionTitle,
    Guid? LevelId,
    Guid CompetencyId,
    QuestionType Type,
    int Difficulty,
    decimal Weight,
    string Text,
    string? Guidance);

/// <summary>
/// Sorunun bir sürümünün değişmez kopyası. Mülakat değerlendirmeleri soru metnini o anki haliyle
/// sakladığı için geçmiş sürümler silinmez.
/// </summary>
public sealed class QuestionVersion
{
    private QuestionVersion() { }

    internal QuestionVersion(int number, Question question, Guid createdByUserId, DateTimeOffset createdAt)
    {
        Number = number;
        Text = question.Text;
        Guidance = question.Guidance;
        Type = question.Type;
        Difficulty = question.Difficulty;
        Weight = question.Weight;
        CompetencyId = question.CompetencyId;
        CreatedByUserId = createdByUserId;
        CreatedAt = createdAt;
    }

    public int Number { get; private set; }
    public string Text { get; private set; } = default!;
    public string? Guidance { get; private set; }
    public QuestionType Type { get; private set; }
    public int Difficulty { get; private set; }
    public decimal Weight { get; private set; }
    public Guid CompetencyId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
}

/// <summary>Soru bankasındaki bir soru. Bir birime aittir; o birimin takım liderleri ve İK yönetir.</summary>
public sealed class Question : AggregateRoot
{
    public const int TextMaxLength = 2000;
    public const int GuidanceMaxLength = 4000;
    public const int PositionTitleMaxLength = 200;
    public const decimal MinWeight = 0.5m;
    public const decimal MaxWeight = 5m;

    private readonly List<QuestionVersion> _versions = [];

    private Question() { }

    public Guid DepartmentId { get; private set; }

    /// <summary>Soru belirli bir pozisyon adına yönelikse (ör. "Backend Geliştirici"); boşsa birimin geneli için.</summary>
    public string? PositionTitle { get; private set; }

    public Guid? LevelId { get; private set; }
    public Guid CompetencyId { get; private set; }
    public QuestionType Type { get; private set; }

    /// <summary>1 kolay, 2 orta, 3 zor.</summary>
    public int Difficulty { get; private set; }

    /// <summary>Puan hesabındaki ağırlık (varsayılan 1).</summary>
    public decimal Weight { get; private set; }

    public string Text { get; private set; } = default!;

    /// <summary>Beklenen cevap ve mülakatçıya ipuçları.</summary>
    public string? Guidance { get; private set; }

    public bool IsActive { get; private set; }

    /// <summary>Güncel sürüm numarası; her içerik değişikliğinde artar.</summary>
    public int Version { get; private set; }

    public IReadOnlyCollection<QuestionVersion> Versions => _versions;

    public static Question Create(QuestionContent content, Guid createdByUserId, TimeProvider clock)
    {
        var question = new Question { IsActive = true };
        question.Apply(content);
        question.AddVersion(createdByUserId, clock);
        return question;
    }

    /// <summary>İçeriği günceller; bir şey değiştiyse yeni sürüm oluşur.</summary>
    /// <returns>Yeni sürüm oluştu mu?</returns>
    public bool Update(QuestionContent content, Guid updatedByUserId, TimeProvider clock)
    {
        var before = Snapshot();
        Apply(content);
        if (Snapshot() == before)
            return false;

        AddVersion(updatedByUserId, clock);
        return true;
    }

    /// <summary>Pasif soru yeni şablonlara eklenemez; kullanıldığı yerlerde görünmeye devam eder.</summary>
    public void SetActive(bool isActive) => IsActive = isActive;

    private void Apply(QuestionContent content)
    {
        if (content.DepartmentId == Guid.Empty)
            throw new DomainException("Sorunun ait olduğu birimi seçmelisiniz.");
        if (content.CompetencyId == Guid.Empty)
            throw new DomainException("Sorunun ölçtüğü yetkinliği seçmelisiniz.");
        if (string.IsNullOrWhiteSpace(content.Text))
            throw new DomainException("Soru metnini yazmalısınız.");
        if (content.Difficulty is < 1 or > 3)
            throw new DomainException("Zorluk 1 (kolay), 2 (orta) veya 3 (zor) olmalı.");
        if (content.Weight is < MinWeight or > MaxWeight)
            throw new DomainException($"Ağırlık {MinWeight} ile {MaxWeight} arasında olmalı.");

        DepartmentId = content.DepartmentId;
        PositionTitle = string.IsNullOrWhiteSpace(content.PositionTitle) ? null : content.PositionTitle.Trim();
        LevelId = content.LevelId;
        CompetencyId = content.CompetencyId;
        Type = content.Type;
        Difficulty = content.Difficulty;
        Weight = content.Weight;
        Text = content.Text.Trim();
        Guidance = string.IsNullOrWhiteSpace(content.Guidance) ? null : content.Guidance.Trim();
    }

    private QuestionContent Snapshot() =>
        new(DepartmentId, PositionTitle, LevelId, CompetencyId, Type, Difficulty, Weight, Text, Guidance);

    private void AddVersion(Guid userId, TimeProvider clock)
    {
        Version++;
        _versions.Add(new QuestionVersion(Version, this, userId, clock.GetUtcNow()));
    }
}
