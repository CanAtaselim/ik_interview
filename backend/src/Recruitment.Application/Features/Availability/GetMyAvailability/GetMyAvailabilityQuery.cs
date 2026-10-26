using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Availability.GetMyAvailability;

/// <summary>Giriş yapan kullanıcının verilen aralıktaki müsaitlikleri.</summary>
public sealed record GetMyAvailabilityQuery(DateTimeOffset From, DateTimeOffset To) : IQuery<IReadOnlyList<AvailabilityDto>>
{
    public static readonly TimeSpan MaxRange = TimeSpan.FromDays(62);
}

internal sealed class GetMyAvailabilityQueryValidator : AbstractValidator<GetMyAvailabilityQuery>
{
    public GetMyAvailabilityQueryValidator() =>
        RuleFor(q => q.To)
            .Must((q, to) => to > q.From && to - q.From <= GetMyAvailabilityQuery.MaxRange)
            .WithMessage("Tarih aralığı en fazla iki ay olabilir ve bitiş başlangıçtan sonra olmalı.");
}

internal sealed class GetMyAvailabilityQueryHandler(IAppDbContext db, ICurrentUser currentUser)
    : IQueryHandler<GetMyAvailabilityQuery, IReadOnlyList<AvailabilityDto>>
{
    public async Task<Result<IReadOnlyList<AvailabilityDto>>> Handle(GetMyAvailabilityQuery query, CancellationToken ct) =>
        await db.InterviewerAvailabilities.AsNoTracking()
            .Where(a => a.UserId == currentUser.UserId && a.StartsAt < query.To && a.EndsAt > query.From)
            .OrderBy(a => a.StartsAt)
            .Select(a => new AvailabilityDto(a.Id, a.StartsAt, a.EndsAt))
            .ToListAsync(ct);
}
