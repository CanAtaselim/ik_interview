using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Auth.GetCurrentUser;

internal sealed class GetCurrentUserQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IQueryHandler<GetCurrentUserQuery, CurrentUserDto>
{
    public async Task<Result<CurrentUserDto>> Handle(GetCurrentUserQuery query, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == currentUser.UserId, ct);
        if (user is null)
            return AuthErrors.SessionExpired;

        // Roller token'dan okunur: kullanıcının o anki oturumunda geçerli olan yetkiler bunlardır.
        return user.ToCurrentUserDto(currentUser.Roles.Order(), currentUser.Scope);
    }
}
