using Recruitment.Domain.Common;
using Recruitment.Domain.Users;
using Shouldly;

namespace Recruitment.Domain.Tests.Users;

public sealed class RoleGroupMappingTests
{
    [Fact]
    public void Create_WithBlankGroupName_Throws()
    {
        Should.Throw<DomainException>(() => RoleGroupMapping.Create(" ", Role.HrManager));
    }

    [Fact]
    public void Create_WithPaddedGroupName_TrimsIt()
    {
        var mapping = RoleGroupMapping.Create("  IK-Yoneticileri ", Role.HrManager);

        mapping.GroupName.ShouldBe("IK-Yoneticileri");
        mapping.Role.ShouldBe(Role.HrManager);
    }
}
