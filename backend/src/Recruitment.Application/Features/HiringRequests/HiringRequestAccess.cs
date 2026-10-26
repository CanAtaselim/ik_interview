using Recruitment.Application.Abstractions;
using Recruitment.Domain.HiringRequests;
using Recruitment.Domain.HiringRequests.Specifications;
using Recruitment.Domain.Users;

namespace Recruitment.Application.Features.HiringRequests;

/// <summary>Kadro taleplerinde veri bazlı yetki: kim hangi talebi görür, kim ne yapabilir.</summary>
internal static class HiringRequestAccess
{
    public static HiringRequestVisibleTo VisibleTo(ICurrentUser user) =>
        new(user.UserId, user.IsHr, user.IsInRole(Role.DepartmentManager) ? user.DepartmentId : null);

    public static HiringRequestAwaitingDecisionBy AwaitingDecisionBy(ICurrentUser user) =>
        new(user.Roles, user.DepartmentId);

    public static bool IsOwner(this HiringRequest request, ICurrentUser user) =>
        request.RequestedByUserId == user.UserId;

    public static bool CanBeCancelledBy(this HiringRequest request, ICurrentUser user) =>
        request.CanBeCancelled &&
        (request.IsOwner(user) || user.IsInRole(Role.HrManager));

    public static HiringRequestPermissions PermissionsFor(this HiringRequest request, ICurrentUser user)
    {
        var isOwnDraft = request.IsOwner(user) && request.Status == HiringRequestStatus.Draft;

        return new HiringRequestPermissions(
            CanEdit: isOwnDraft,
            CanSubmit: isOwnDraft,
            CanDecide: request.CanBeDecidedBy(user.Roles, user.DepartmentId),
            CanCancel: request.CanBeCancelledBy(user));
    }
}
