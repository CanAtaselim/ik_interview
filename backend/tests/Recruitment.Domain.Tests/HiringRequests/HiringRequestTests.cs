using Microsoft.Extensions.Time.Testing;
using Recruitment.Domain.Common;
using Recruitment.Domain.HiringRequests;
using Recruitment.Domain.HiringRequests.Events;
using Recruitment.Domain.Users;
using Shouldly;

namespace Recruitment.Domain.Tests.HiringRequests;

public sealed class HiringRequestTests
{
    private static readonly Role[] DefaultFlow = [Role.DepartmentManager, Role.HrManager];

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 9, 9, 0, 0, TimeSpan.Zero));
    private readonly Guid _departmentId = Guid.CreateVersion7();
    private readonly Guid _requesterId = Guid.CreateVersion7();
    private readonly Guid _approverId = Guid.CreateVersion7();

    private HiringRequestDetails Details(
        string title = "Backend Geliştirici",
        int headcount = 2,
        HiringReason reason = HiringReason.NewHeadcount,
        string? replacedEmployee = null,
        params string[] competencies) =>
        new(title, _departmentId, Guid.CreateVersion7(), headcount, reason, replacedEmployee, Urgency.Normal,
            new DateOnly(2026, 12, 1), competencies, "Ödeme ekibi için.");

    private HiringRequest Draft() => HiringRequest.Create(Details(), _requesterId);

    private HiringRequest Submitted()
    {
        var request = Draft();
        request.Submit(DefaultFlow, _clock);
        request.ClearDomainEvents();
        return request;
    }

    [Fact]
    public void IsComplete_RequiresEveryFieldExceptReplacedEmployeeForNewHeadcount()
    {
        HiringRequest With(HiringRequestDetails details) => HiringRequest.Create(details, _requesterId);

        With(Details(competencies: "C#")).IsComplete.ShouldBeTrue();
        With(Details()).IsComplete.ShouldBeFalse(); // yetkinlik yok
        With(Details(competencies: "C#") with { Description = "  " }).IsComplete.ShouldBeFalse();
        With(Details(competencies: "C#") with { ExpectedStartDate = null }).IsComplete.ShouldBeFalse();
        With(Details(reason: HiringReason.Replacement, replacedEmployee: "Ayşe Y.", competencies: "C#")).IsComplete.ShouldBeTrue();
    }

    [Fact]
    public void Create_WithValidDetails_StartsAsDraft()
    {
        var request = HiringRequest.Create(Details(competencies: [" C# ", "c#", "PostgreSQL", " "]), _requesterId);

        request.Status.ShouldBe(HiringRequestStatus.Draft);
        request.RequestedByUserId.ShouldBe(_requesterId);
        request.AwaitingRole.ShouldBeNull();
        request.Competencies.ShouldBe(["C#", "PostgreSQL"]);
        request.DomainEvents.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(" ", 1)]
    [InlineData("Backend Geliştirici", 0)]
    public void Create_WithInvalidDetails_Throws(string title, int headcount)
    {
        Should.Throw<DomainException>(() => HiringRequest.Create(Details(title, headcount), _requesterId));
    }

    [Fact]
    public void Create_AsReplacementWithoutEmployeeName_Throws()
    {
        Should.Throw<DomainException>(() =>
            HiringRequest.Create(Details(reason: HiringReason.Replacement), _requesterId));
    }

    [Fact]
    public void Create_AsNewHeadcount_IgnoresReplacedEmployeeName()
    {
        var request = HiringRequest.Create(Details(replacedEmployee: "Ali Veli"), _requesterId);

        request.ReplacedEmployeeName.ShouldBeNull();
    }

    [Fact]
    public void Update_WhenDraft_ChangesDetails()
    {
        var request = Draft();

        request.Update(Details("Kıdemli Backend Geliştirici", 3));

        request.Title.ShouldBe("Kıdemli Backend Geliştirici");
        request.Headcount.ShouldBe(3);
    }

    [Fact]
    public void Update_WhenSubmitted_Throws()
    {
        Should.Throw<DomainException>(() => Submitted().Update(Details()));
    }

    [Fact]
    public void Submit_WhenDraft_CreatesStepsAndAwaitsFirstApprover()
    {
        var request = Draft();

        request.Submit(DefaultFlow, _clock);

        request.Status.ShouldBe(HiringRequestStatus.PendingApproval);
        request.AwaitingRole.ShouldBe(Role.DepartmentManager);
        request.SubmittedAt.ShouldBe(_clock.GetUtcNow());
        request.ApprovalSteps.OrderBy(s => s.Order).Select(s => s.Role).ShouldBe(DefaultFlow);
        request.ApprovalSteps.ShouldAllBe(s => s.Decision == ApprovalDecision.Pending);
        request.DomainEvents.Count.ShouldBe(2);
        request.DomainEvents[0].ShouldBeOfType<HiringRequestSubmitted>();
        // İlk onaycıya bildirim gitsin diye.
        request.DomainEvents[1].ShouldBe(new HiringRequestAwaitingApproval(request.Id, Role.DepartmentManager, 1));
    }

    [Fact]
    public void Submit_WhenAlreadySubmitted_Throws()
    {
        Should.Throw<DomainException>(() => Submitted().Submit(DefaultFlow, _clock));
    }

    [Fact]
    public void Submit_WithEmptyFlow_Throws()
    {
        Should.Throw<DomainException>(() => Draft().Submit([], _clock));
    }

    [Fact]
    public void CanBeDecidedBy_DepartmentManagerOfSameDepartment_ReturnsTrue()
    {
        Submitted().CanBeDecidedBy(new HashSet<Role> { Role.DepartmentManager }, _departmentId).ShouldBeTrue();
    }

    [Fact]
    public void CanBeDecidedBy_DepartmentManagerOfOtherDepartment_ReturnsFalse()
    {
        var request = Submitted();

        request.CanBeDecidedBy(new HashSet<Role> { Role.DepartmentManager }, Guid.CreateVersion7()).ShouldBeFalse();
        request.CanBeDecidedBy(new HashSet<Role> { Role.DepartmentManager }, null).ShouldBeFalse();
    }

    [Fact]
    public void CanBeDecidedBy_RoleOfLaterStep_ReturnsFalseUntilItsTurn()
    {
        var request = Submitted();
        var hrManager = new HashSet<Role> { Role.HrManager };

        request.CanBeDecidedBy(hrManager, null).ShouldBeFalse();

        request.Approve(_approverId, null, _clock);

        request.CanBeDecidedBy(hrManager, null).ShouldBeTrue();
    }

    [Fact]
    public void CanBeDecidedBy_WhenNotPendingApproval_ReturnsFalse()
    {
        Draft().CanBeDecidedBy(new HashSet<Role> { Role.DepartmentManager }, _departmentId).ShouldBeFalse();
    }

    [Fact]
    public void Approve_FirstOfTwoSteps_MovesToNextApproverWithoutCompleting()
    {
        var request = Submitted();

        request.Approve(_approverId, "Bütçe uygun.", _clock);

        request.Status.ShouldBe(HiringRequestStatus.PendingApproval);
        request.AwaitingRole.ShouldBe(Role.HrManager);
        request.DomainEvents.ShouldHaveSingleItem().ShouldBe(new HiringRequestAwaitingApproval(request.Id, Role.HrManager, 2));
        var first = request.ApprovalSteps.Single(s => s.Order == 1);
        first.Decision.ShouldBe(ApprovalDecision.Approved);
        first.DecidedByUserId.ShouldBe(_approverId);
        first.Note.ShouldBe("Bütçe uygun.");
        first.DecidedAt.ShouldBe(_clock.GetUtcNow());
    }

    [Fact]
    public void Approve_LastStep_ApprovesRequestAndRaisesEvent()
    {
        var request = Submitted();
        request.Approve(_approverId, null, _clock);
        request.ClearDomainEvents();

        request.Approve(_approverId, null, _clock);

        request.Status.ShouldBe(HiringRequestStatus.Approved);
        request.AwaitingRole.ShouldBeNull();
        request.CompletedAt.ShouldBe(_clock.GetUtcNow());
        request.DomainEvents.ShouldHaveSingleItem().ShouldBe(new HiringRequestApproved(request.Id));
    }

    [Fact]
    public void Approve_WhenDraft_Throws()
    {
        Should.Throw<DomainException>(() => Draft().Approve(_approverId, null, _clock));
    }

    [Fact]
    public void Reject_WithReason_RejectsRequestAndLeavesLaterStepsPending()
    {
        var request = Submitted();

        request.Reject(_approverId, "Bu çeyrekte bütçe yok.", _clock);

        request.Status.ShouldBe(HiringRequestStatus.Rejected);
        request.AwaitingRole.ShouldBeNull();
        request.ApprovalSteps.Single(s => s.Order == 1).Note.ShouldBe("Bu çeyrekte bütçe yok.");
        request.ApprovalSteps.Single(s => s.Order == 2).Decision.ShouldBe(ApprovalDecision.Pending);
        request.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<HiringRequestRejected>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reject_WithoutReason_Throws(string reason)
    {
        Should.Throw<DomainException>(() => Submitted().Reject(_approverId, reason, _clock));
    }

    [Fact]
    public void Approve_AfterRejection_Throws()
    {
        var request = Submitted();
        request.Reject(_approverId, "Uygun değil.", _clock);

        Should.Throw<DomainException>(() => request.Approve(_approverId, null, _clock));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Cancel_FromDraftOrPending_CancelsRequest(bool submitted)
    {
        var request = submitted ? Submitted() : Draft();

        request.Cancel(_clock);

        request.Status.ShouldBe(HiringRequestStatus.Cancelled);
        request.AwaitingRole.ShouldBeNull();
        request.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<HiringRequestCancelled>();
    }

    [Fact]
    public void Cancel_AfterAnApproverDecided_Throws_AndKeepsTheDecision()
    {
        var approved = Submitted();
        approved.Approve(_approverId, null, _clock);
        var rejected = Submitted();
        rejected.Reject(_approverId, "Uygun değil.", _clock);
        var fullyApproved = Submitted();
        fullyApproved.Approve(_approverId, null, _clock);
        fullyApproved.Approve(_approverId, null, _clock);

        foreach (var request in new[] { approved, rejected, fullyApproved })
        {
            var status = request.Status;
            request.CanBeCancelled.ShouldBeFalse();
            Should.Throw<DomainException>(() => request.Cancel(_clock));
            request.Status.ShouldBe(status);
        }

        rejected.Status.ShouldBe(HiringRequestStatus.Rejected);
    }

    [Fact]
    public void CanBeCancelled_IsTrueOnlyForDraftAndUntouchedPendingRequests()
    {
        Draft().CanBeCancelled.ShouldBeTrue();
        Submitted().CanBeCancelled.ShouldBeTrue();

        var cancelled = Draft();
        cancelled.Cancel(_clock);
        cancelled.CanBeCancelled.ShouldBeFalse();
    }

    [Fact]
    public void Cancel_WhenAlreadyCancelled_Throws()
    {
        var request = Draft();
        request.Cancel(_clock);

        Should.Throw<DomainException>(() => request.Cancel(_clock));
    }
}
