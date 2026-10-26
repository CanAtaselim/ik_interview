using Recruitment.Domain.Common;
using Recruitment.Domain.Users;

namespace Recruitment.Domain.Approvals;

// Offer (teklif onayı) Faz 2'de eklenecek.
public enum ApprovalFlowKind
{
    HiringRequest,
}

public sealed class ApprovalFlowStep
{
    private ApprovalFlowStep() { }

    internal ApprovalFlowStep(int order, Role role)
    {
        Order = order;
        Role = role;
    }

    public int Order { get; private set; }
    public Role Role { get; private set; }

    internal void ChangeRole(Role role) => Role = role;
}

/// <summary>Bir onay sürecinde hangi rollerin hangi sırayla onay vereceği.</summary>
public sealed class ApprovalFlowDefinition : AggregateRoot
{
    private readonly List<ApprovalFlowStep> _steps = [];

    private ApprovalFlowDefinition() { }

    public ApprovalFlowKind Kind { get; private set; }
    public IReadOnlyCollection<ApprovalFlowStep> Steps => _steps;

    public IReadOnlyList<Role> ApproverRoles => _steps.OrderBy(s => s.Order).Select(s => s.Role).ToList();

    /// <summary>
    /// Onay verebilen roller (kadro talebini onaylama yetkisi bu rollerdedir). Başka bir rol akışa
    /// konursa adım hiç kimse tarafından onaylanamaz.
    /// </summary>
    public static readonly IReadOnlyList<Role> AllowedApproverRoles = [Role.DepartmentManager, Role.HrManager];

    public static ApprovalFlowDefinition Create(ApprovalFlowKind kind, IReadOnlyList<Role> approverRoles)
    {
        if (approverRoles.Count == 0)
            throw new DomainException("Onay akışında en az bir adım olmalı.");

        var flow = new ApprovalFlowDefinition { Kind = kind };
        flow._steps.AddRange(approverRoles.Select((role, index) => new ApprovalFlowStep(index + 1, role)));
        return flow;
    }

    /// <summary>
    /// Akışı yeni sırayla değiştirir. Yalnızca bundan sonra onaya gönderilen talepleri etkiler;
    /// onay bekleyen taleplerin adımları gönderildikleri andaki akışla kalır.
    /// </summary>
    public void ReplaceSteps(IReadOnlyList<Role> approverRoles)
    {
        if (approverRoles.Count == 0)
            throw new DomainException("Onay akışında en az bir adım olmalı.");
        if (approverRoles.Distinct().Count() != approverRoles.Count)
            throw new DomainException("Aynı rol onay akışında yalnızca bir kez yer alabilir.");
        if (approverRoles.Any(role => !AllowedApproverRoles.Contains(role)))
            throw new DomainException("Onay akışına yalnızca Departman Yöneticisi ve İK Yöneticisi eklenebilir.");

        // Adımlar yerinde güncellenir (aynı anahtarla sil-ekle yerine), fazlası silinir veya eklenir.
        var ordered = _steps.OrderBy(s => s.Order).ToList();
        for (var index = 0; index < approverRoles.Count; index++)
        {
            if (index < ordered.Count)
                ordered[index].ChangeRole(approverRoles[index]);
            else
                _steps.Add(new ApprovalFlowStep(index + 1, approverRoles[index]));
        }

        _steps.RemoveAll(s => s.Order > approverRoles.Count);
    }
}
