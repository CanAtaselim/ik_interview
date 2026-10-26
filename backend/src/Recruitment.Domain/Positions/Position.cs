using Recruitment.Domain.Common;

namespace Recruitment.Domain.Positions;

/// <summary>
/// Aday aranan açık kadro. Onaylanan kadro talebinden otomatik açılır; İK talepsiz de açabilir.
/// </summary>
public sealed class Position : AggregateRoot
{
    public const int TitleMaxLength = 200;

    private Position() { }

    public Guid? HiringRequestId { get; private set; }
    public string Title { get; private set; } = default!;
    public Guid DepartmentId { get; private set; }
    public Guid LevelId { get; private set; }
    public int Headcount { get; private set; }
    public int FilledCount { get; private set; }
    public Guid? ResponsibleHrUserId { get; private set; }
    public string? JobDescription { get; private set; }
    public PositionStatus Status { get; private set; }

    private List<Guid> _interviewTemplateIds = [];

    /// <summary>Bu pozisyonun mülakatlarında kullanılan soru şablonları (ör. teknik ve final için ayrı).</summary>
    public IReadOnlyList<Guid> InterviewTemplateIds => _interviewTemplateIds;

    public static Position Open(
        string title,
        Guid departmentId,
        Guid levelId,
        int headcount,
        string? jobDescription,
        Guid? responsibleHrUserId,
        Guid? hiringRequestId = null)
    {
        var position = new Position
        {
            HiringRequestId = hiringRequestId,
            DepartmentId = departmentId,
            LevelId = levelId,
            Status = PositionStatus.Open,
        };
        position.SetDetails(title, headcount, jobDescription, responsibleHrUserId);
        return position;
    }

    public void UpdateDetails(string title, int headcount, string? jobDescription, Guid? responsibleHrUserId)
    {
        if (Status == PositionStatus.Cancelled)
            throw new DomainException("İptal edilmiş pozisyon düzenlenemez.");

        SetDetails(title, headcount, jobDescription, responsibleHrUserId);
    }

    /// <summary>Yalnızca açık pozisyona aday bağlanabilir.</summary>
    public bool AcceptsCandidates => Status == PositionStatus.Open;

    public void SetInterviewTemplates(IEnumerable<Guid> templateIds) =>
        _interviewTemplateIds = [.. templateIds.Where(id => id != Guid.Empty).Distinct()];

    /// <summary>İşe alım: doluluk artar. Kadro dolunca pozisyon kendiliğinden kapanmaz; kararı İK verir.</summary>
    public void RecordHire() => FilledCount++;

    public void RevertHire() => FilledCount = Math.Max(0, FilledCount - 1);

    public void PutOnHold()
    {
        if (Status != PositionStatus.Open)
            throw new DomainException("Yalnızca açık pozisyon beklemeye alınabilir.");

        Status = PositionStatus.OnHold;
    }

    public void Reopen()
    {
        if (Status is not (PositionStatus.OnHold or PositionStatus.Closed))
            throw new DomainException("Yalnızca beklemedeki veya kapatılmış pozisyon yeniden açılabilir.");

        Status = PositionStatus.Open;
    }

    public void Close()
    {
        if (Status is not (PositionStatus.Open or PositionStatus.OnHold))
            throw new DomainException("Yalnızca açık veya beklemedeki pozisyon kapatılabilir.");

        Status = PositionStatus.Closed;
    }

    public void Cancel()
    {
        if (Status is not (PositionStatus.Open or PositionStatus.OnHold))
            throw new DomainException("Yalnızca açık veya beklemedeki pozisyon iptal edilebilir.");

        Status = PositionStatus.Cancelled;
    }

    private void SetDetails(string title, int headcount, string? jobDescription, Guid? responsibleHrUserId)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new DomainException("Pozisyon adını yazmalısınız.");
        if (headcount < 1)
            throw new DomainException("Kişi sayısı en az 1 olmalı.");
        if (headcount < FilledCount)
            throw new DomainException($"Kişi sayısı, işe alınan kişi sayısından ({FilledCount}) az olamaz.");

        Title = title.Trim();
        Headcount = headcount;
        JobDescription = string.IsNullOrWhiteSpace(jobDescription) ? null : jobDescription.Trim();
        ResponsibleHrUserId = responsibleHrUserId;
    }
}
