using Recruitment.Domain.Common;
using Recruitment.Domain.Positions;
using Shouldly;

namespace Recruitment.Domain.Tests.Positions;

public sealed class PositionTests
{
    private static Position OpenPosition(int headcount = 2) =>
        Position.Open("Backend Geliştirici", Guid.CreateVersion7(), Guid.CreateVersion7(), headcount, " İş tanımı ", null);

    [Fact]
    public void Open_WithValidDetails_StartsOpenWithNoHires()
    {
        var position = OpenPosition();

        position.Status.ShouldBe(PositionStatus.Open);
        position.FilledCount.ShouldBe(0);
        position.JobDescription.ShouldBe("İş tanımı");
        position.HiringRequestId.ShouldBeNull();
    }

    [Theory]
    [InlineData("", 1)]
    [InlineData("Backend Geliştirici", 0)]
    public void Open_WithInvalidDetails_Throws(string title, int headcount)
    {
        Should.Throw<DomainException>(() =>
            Position.Open(title, Guid.CreateVersion7(), Guid.CreateVersion7(), headcount, null, null));
    }

    [Fact]
    public void PutOnHold_ThenReopen_ReturnsToOpen()
    {
        var position = OpenPosition();

        position.PutOnHold();
        position.Status.ShouldBe(PositionStatus.OnHold);

        position.Reopen();
        position.Status.ShouldBe(PositionStatus.Open);
    }

    [Fact]
    public void Reopen_WhenClosed_ReturnsToOpen()
    {
        var position = OpenPosition();
        position.Close();

        position.Reopen();

        position.Status.ShouldBe(PositionStatus.Open);
    }

    [Fact]
    public void Reopen_WhenAlreadyOpen_Throws()
    {
        Should.Throw<DomainException>(() => OpenPosition().Reopen());
    }

    [Fact]
    public void PutOnHold_WhenClosed_Throws()
    {
        var position = OpenPosition();
        position.Close();

        Should.Throw<DomainException>(position.PutOnHold);
    }

    [Fact]
    public void Cancel_IsTerminal()
    {
        var position = OpenPosition();
        position.Cancel();

        position.Status.ShouldBe(PositionStatus.Cancelled);
        Should.Throw<DomainException>(position.Reopen);
        Should.Throw<DomainException>(position.Close);
        Should.Throw<DomainException>(() => position.UpdateDetails("Yeni ad", 1, null, null));
    }

    [Fact]
    public void UpdateDetails_WhenOpen_ChangesDetails()
    {
        var position = OpenPosition();
        var hrUserId = Guid.CreateVersion7();

        position.UpdateDetails("Kıdemli Backend Geliştirici", 3, null, hrUserId);

        position.Title.ShouldBe("Kıdemli Backend Geliştirici");
        position.Headcount.ShouldBe(3);
        position.JobDescription.ShouldBeNull();
        position.ResponsibleHrUserId.ShouldBe(hrUserId);
    }
}
