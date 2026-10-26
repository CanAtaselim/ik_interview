using Recruitment.Domain.Approvals;
using Recruitment.Domain.Common;
using Recruitment.Domain.Users;
using Shouldly;

namespace Recruitment.Domain.Tests.Approvals;

public sealed class ApprovalFlowDefinitionTests
{
    private static ApprovalFlowDefinition Default() => ApprovalFlowDefinition.Create(
        ApprovalFlowKind.HiringRequest, [Role.DepartmentManager, Role.HrManager]);

    [Fact]
    public void ReplaceSteps_ChangesOrderAndRoles_InPlace()
    {
        var flow = Default();

        flow.ReplaceSteps([Role.HrManager, Role.DepartmentManager]);

        flow.ApproverRoles.ShouldBe([Role.HrManager, Role.DepartmentManager]);
        flow.Steps.Select(s => s.Order).ShouldBe([1, 2], ignoreOrder: true);
    }

    [Fact]
    public void ReplaceSteps_CanShrinkToASingleStep_AndGrowBack()
    {
        var flow = Default();

        flow.ReplaceSteps([Role.HrManager]);
        flow.ApproverRoles.ShouldBe([Role.HrManager]);
        flow.Steps.Count.ShouldBe(1);

        flow.ReplaceSteps([Role.DepartmentManager, Role.HrManager]);
        flow.ApproverRoles.ShouldBe([Role.DepartmentManager, Role.HrManager]);
    }

    [Fact]
    public void ReplaceSteps_RejectsEmptyDuplicateAndRolesThatCannotApprove()
    {
        var flow = Default();

        Should.Throw<DomainException>(() => flow.ReplaceSteps([]));
        Should.Throw<DomainException>(() => flow.ReplaceSteps([Role.HrManager, Role.HrManager]))
            .Message.ShouldBe("Aynı rol onay akışında yalnızca bir kez yer alabilir.");
        Should.Throw<DomainException>(() => flow.ReplaceSteps([Role.Executive]))
            .Message.ShouldBe("Onay akışına yalnızca Departman Yöneticisi ve İK Yöneticisi eklenebilir.");
        flow.ApproverRoles.ShouldBe([Role.DepartmentManager, Role.HrManager]);
    }
}
