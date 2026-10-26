using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recruitment.Api.Auth;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Features.Availability.AddAvailability;
using Recruitment.Application.Features.Availability.DeleteAvailability;
using Recruitment.Application.Features.Availability.GetMyAvailability;

namespace Recruitment.Api.Controllers;

/// <summary>Giriş yapan kullanıcının mülakat müsaitliği (Faz 1: sistem içi takvim).</summary>
[ApiController]
[Route("api/v1/me/availability")]
[Authorize(Policy = Policies.CanInterview)]
public sealed class AvailabilityController(IDispatcher dispatcher) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] DateTimeOffset from, [FromQuery] DateTimeOffset to, CancellationToken ct) =>
        (await dispatcher.Query(new GetMyAvailabilityQuery(from, to), ct)).ToActionResult(this);

    [HttpPost]
    public async Task<IActionResult> Add(AddAvailabilityCommand command, CancellationToken ct) =>
        (await dispatcher.Send(command, ct)).ToCreatedResult(this);

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct) =>
        (await dispatcher.Send(new DeleteAvailabilityCommand(id), ct)).ToActionResult(this);
}
