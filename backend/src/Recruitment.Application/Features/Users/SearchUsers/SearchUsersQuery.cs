using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Users;

namespace Recruitment.Application.Features.Users.SearchUsers;

public sealed record UserSummaryDto(Guid Id, string DisplayName, string? Title, string? Department);

/// <summary>
/// Kullanıcı seçim listeleri için arama. Yalnızca uygulamaya en az bir kez giriş yapmış
/// (dolayısıyla dizinden eşitlenmiş) aktif kullanıcılar döner.
/// </summary>
/// <param name="Roles">Verilirse yalnızca bu rollerden en az birine sahip kullanıcılar.</param>
public sealed record SearchUsersQuery(
    string? Search,
    IReadOnlyCollection<Role>? Roles,
    int Page = 1,
    int PageSize = Paging.DefaultPageSize) : IQuery<PagedResult<UserSummaryDto>>;
