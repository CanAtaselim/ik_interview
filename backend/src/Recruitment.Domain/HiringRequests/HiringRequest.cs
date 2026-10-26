using Recruitment.Domain.Common;
using Recruitment.Domain.HiringRequests.Events;
using Recruitment.Domain.Users;

namespace Recruitment.Domain.HiringRequests;

/// <summary>
/// Kadro talebi. Akış: Draft → PendingApproval → Approved | Rejected. Cancelled yalnızca taslaktan veya
/// henüz hiçbir onaycının karar vermediği onay bekleyen talepten olur; karar sonrası iptal edilemez
/// (reddedilen talep "iptal edildi" görünüp reddi gizlemesin).
/// Son onayda <see cref="HiringRequestApproved"/> üretilir ve talep için pozisyon açılır.
/// </summary>
public sealed class HiringRequest : AggregateRoot
{
    public const int TitleMaxLength = 200;

    private readonly List<string> _competencies = [];
    private readonly List<ApprovalStep> _approvalSteps = [];

    private HiringRequest() { }

    public string Title { get; private set; } = default!;
    public Guid DepartmentId { get; private set; }
    public Guid LevelId { get; private set; }
    public int Headcount { get; private set; }
    public HiringReason Reason { get; private set; }
    public string? ReplacedEmployeeName { get; private set; }
    public Urgency Urgency { get; private set; }
    public DateOnly? ExpectedStartDate { get; private set; }
    public string? Description { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public HiringRequestStatus Status { get; private set; }

    /// <summary>Sıradaki onayı verecek rol; talep onay beklemiyorsa <c>null</c>.</summary>
    public Role? AwaitingRole { get; private set; }

    public DateTimeOffset? SubmittedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    public IReadOnlyList<string> Competencies => _competencies;

    /// <summary>
    /// Onaya gönderilebilmesi için tüm alanlar dolu olmalı (yerine alınan çalışan adı yalnızca
    /// "yerine alım" nedeninde gerekir). Eski taslaklar eksik olabilir; gönderilmeden önce tamamlanır.
    /// </summary>
    public bool IsComplete =>
        !string.IsNullOrWhiteSpace(Title) &&
        ExpectedStartDate is not null &&
        !string.IsNullOrWhiteSpace(Description) &&
        _competencies.Count > 0 &&
        (Reason != HiringReason.Replacement || !string.IsNullOrWhiteSpace(ReplacedEmployeeName));
    public IReadOnlyCollection<ApprovalStep> ApprovalSteps => _approvalSteps;

    public static HiringRequest Create(HiringRequestDetails details, Guid requestedByUserId)
    {
        var request = new HiringRequest
        {
            RequestedByUserId = requestedByUserId,
            Status = HiringRequestStatus.Draft,
        };
        request.Apply(details);
        return request;
    }

    public void Update(HiringRequestDetails details)
    {
        if (Status != HiringRequestStatus.Draft)
            throw new DomainException("Yalnızca taslak durumundaki talep düzenlenebilir.");

        Apply(details);
    }

    public void Submit(IReadOnlyList<Role> approverRoles, TimeProvider clock)
    {
        if (Status != HiringRequestStatus.Draft)
            throw new DomainException("Yalnızca taslak durumundaki talep onaya gönderilebilir.");
        if (approverRoles.Count == 0)
            throw new DomainException("Kadro talebi için onay akışı tanımlı değil.");

        _approvalSteps.AddRange(approverRoles.Select((role, index) => new ApprovalStep(index + 1, role)));
        Status = HiringRequestStatus.PendingApproval;
        AwaitingRole = approverRoles[0];
        SubmittedAt = clock.GetUtcNow();
        Raise(new HiringRequestSubmitted(Id));
        Raise(new HiringRequestAwaitingApproval(Id, approverRoles[0], 1));
    }

    /// <summary>Verilen rollere ve birime sahip kullanıcı sıradaki adıma karar verebilir mi?</summary>
    public bool CanBeDecidedBy(IReadOnlySet<Role> roles, Guid? departmentId)
    {
        if (Status != HiringRequestStatus.PendingApproval || AwaitingRole is not { } role || !roles.Contains(role))
            return false;

        // Departman yöneticisi yalnızca kendi biriminin talebine karar verir.
        return role != Role.DepartmentManager || departmentId == DepartmentId;
    }

    public void Approve(Guid approverUserId, string? note, TimeProvider clock)
    {
        var now = clock.GetUtcNow();
        CurrentStep("onaylanabilir").Decide(ApprovalDecision.Approved, approverUserId, note, now);

        var next = PendingSteps().FirstOrDefault();
        if (next is not null)
        {
            AwaitingRole = next.Role;
            Raise(new HiringRequestAwaitingApproval(Id, next.Role, next.Order));
            return;
        }

        Status = HiringRequestStatus.Approved;
        AwaitingRole = null;
        CompletedAt = now;
        Raise(new HiringRequestApproved(Id));
    }

    public void Reject(Guid approverUserId, string reason, TimeProvider clock)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Ret gerekçesi yazmalısınız.");

        var now = clock.GetUtcNow();
        CurrentStep("reddedilebilir").Decide(ApprovalDecision.Rejected, approverUserId, reason, now);

        Status = HiringRequestStatus.Rejected;
        AwaitingRole = null;
        CompletedAt = now;
        Raise(new HiringRequestRejected(Id));
    }

    /// <summary>
    /// Taslak veya hiçbir onaycının henüz karar vermediği onay bekleyen talep iptal edilebilir.
    /// </summary>
    public bool CanBeCancelled =>
        Status == HiringRequestStatus.Draft ||
        (Status == HiringRequestStatus.PendingApproval && _approvalSteps.All(s => s.Decision == ApprovalDecision.Pending));

    public void Cancel(TimeProvider clock)
    {
        switch (Status)
        {
            case HiringRequestStatus.Cancelled:
                throw new DomainException("Talep zaten iptal edilmiş.");
            case HiringRequestStatus.Approved:
                throw new DomainException("Onaylanmış talep iptal edilemez.");
            case HiringRequestStatus.Rejected:
                throw new DomainException("Reddedilmiş talep iptal edilemez; ret kararı kayıtta kalır.");
        }

        if (!CanBeCancelled)
            throw new DomainException("Bir onaycı karar verdikten sonra talep iptal edilemez.");

        Status = HiringRequestStatus.Cancelled;
        AwaitingRole = null;
        CompletedAt ??= clock.GetUtcNow();
        Raise(new HiringRequestCancelled(Id));
    }

    private IEnumerable<ApprovalStep> PendingSteps() =>
        _approvalSteps.Where(s => s.Decision == ApprovalDecision.Pending).OrderBy(s => s.Order);

    private ApprovalStep CurrentStep(string action)
    {
        if (Status != HiringRequestStatus.PendingApproval)
            throw new DomainException($"Yalnızca onay bekleyen talep {action}.");

        return PendingSteps().First();
    }

    private void Apply(HiringRequestDetails details)
    {
        if (string.IsNullOrWhiteSpace(details.Title))
            throw new DomainException("Pozisyon adını yazmalısınız.");
        if (details.Headcount < 1)
            throw new DomainException("Kişi sayısı en az 1 olmalı.");
        if (details.Reason == HiringReason.Replacement && string.IsNullOrWhiteSpace(details.ReplacedEmployeeName))
            throw new DomainException("Yerine alım yapılacak çalışanın adını yazmalısınız.");

        Title = details.Title.Trim();
        DepartmentId = details.DepartmentId;
        LevelId = details.LevelId;
        Headcount = details.Headcount;
        Reason = details.Reason;
        ReplacedEmployeeName = details.Reason == HiringReason.Replacement ? details.ReplacedEmployeeName!.Trim() : null;
        Urgency = details.Urgency;
        ExpectedStartDate = details.ExpectedStartDate;
        Description = string.IsNullOrWhiteSpace(details.Description) ? null : details.Description.Trim();

        _competencies.Clear();
        _competencies.AddRange(details.Competencies
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase));
    }
}
