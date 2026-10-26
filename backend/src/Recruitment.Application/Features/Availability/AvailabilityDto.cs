namespace Recruitment.Application.Features.Availability;

public sealed record AvailabilityDto(Guid Id, DateTimeOffset StartsAt, DateTimeOffset EndsAt);
