using Recruitment.Domain.Common;

namespace Recruitment.Domain.Questions;

/// <summary>Şablondaki soru ve sırası.</summary>
public sealed class InterviewTemplateQuestion
{
    private InterviewTemplateQuestion() { }

    internal InterviewTemplateQuestion(Guid questionId, int order)
    {
        QuestionId = questionId;
        Order = order;
    }

    public Guid QuestionId { get; private set; }
    public int Order { get; private set; }
}

/// <summary>
/// Mülakatta sorulacak soruların sıralı listesi. Bir birime aittir; seviye ve mülakat tipi
/// verilirse o mülakatlara önerilir.
/// </summary>
public sealed class InterviewTemplate : AggregateRoot
{
    public const int NameMaxLength = 200;
    public const int MaxQuestions = 50;

    private readonly List<InterviewTemplateQuestion> _questions = [];

    private InterviewTemplate() { }

    public string Name { get; private set; } = default!;
    public Guid DepartmentId { get; private set; }
    public Guid? LevelId { get; private set; }
    public Guid? InterviewTypeId { get; private set; }
    public bool IsActive { get; private set; }

    public IReadOnlyCollection<InterviewTemplateQuestion> Questions => _questions;

    public IReadOnlyList<Guid> OrderedQuestionIds => [.. _questions.OrderBy(q => q.Order).Select(q => q.QuestionId)];

    public static InterviewTemplate Create(
        string name, Guid departmentId, Guid? levelId, Guid? interviewTypeId, IReadOnlyCollection<Guid> questionIds)
    {
        var template = new InterviewTemplate { IsActive = true };
        template.Update(name, departmentId, levelId, interviewTypeId, questionIds);
        return template;
    }

    public void Update(
        string name, Guid departmentId, Guid? levelId, Guid? interviewTypeId, IReadOnlyCollection<Guid> questionIds)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Şablonun adını yazmalısınız.");
        if (departmentId == Guid.Empty)
            throw new DomainException("Şablonun ait olduğu birimi seçmelisiniz.");

        var ids = questionIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0)
            throw new DomainException("Şablona en az bir soru eklemelisiniz.");
        if (ids.Count > MaxQuestions)
            throw new DomainException($"Bir şablonda en fazla {MaxQuestions} soru olabilir.");

        Name = name.Trim();
        DepartmentId = departmentId;
        LevelId = levelId;
        InterviewTypeId = interviewTypeId;

        _questions.Clear();
        _questions.AddRange(ids.Select((id, index) => new InterviewTemplateQuestion(id, index + 1)));
    }

    public void SetActive(bool isActive) => IsActive = isActive;
}
