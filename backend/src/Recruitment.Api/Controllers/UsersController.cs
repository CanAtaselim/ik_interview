using Microsoft.AspNetCore.Mvc;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Users.SearchUsers;
using Recruitment.Domain.Users;

namespace Recruitment.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
public sealed class UsersController(IDispatcher dispatcher) : ControllerBase
{
    /// <summary>Kullanıcı seçim listeleri için arama; <c>roles</c> birden çok kez verilebilir.</summary>
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? search,
        [FromQuery] Role[]? roles,
        CancellationToken ct,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = Paging.DefaultPageSize)
    {
        var result = await dispatcher.Query(new SearchUsersQuery(search, roles, page, pageSize), ct);
        return result.ToActionResult(this);
    }
}
