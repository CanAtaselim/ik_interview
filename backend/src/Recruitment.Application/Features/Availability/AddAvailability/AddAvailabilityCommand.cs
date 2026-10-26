using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Interviews;

namespace Recruitment.Application.Features.Availability.AddAvailability;

/// <summary>Giriş yapan kullanıcı mülakat için müsait olduğu bir zaman aralığı ekler.</summary>
public sealed record AddAvailabilityCommand(DateTimeOffset StartsAt, DateTimeOffset EndsAt) : ICommand<Guid>;

internal sealed class AddAvailabilityCommandValidator : AbstractValidator<AddAvailabilityCommand>
{
    public AddAvailabilityCommandValidator() =>
        RuleFor(c => c.EndsAt)
            .GreaterThan(c => c.StartsAt)
            .WithMessage("Bitiş saati başlangıçtan sonra olmalı.");
}

internal sealed class AddAvailabilityCommandHandler(IAppDbContext db, ICurrentUser currentUser, TimeProvider clock)
    : ICommandHandler<AddAvailabilityCommand, Guid>
{
    public async Task<Result<Guid>> Handle(AddAvailabilityCommand command, CancellationToken ct)
    {
        var overlaps = await db.InterviewerAvailabilities.AnyAsync(
            a => a.UserId == currentUser.UserId && a.StartsAt < command.EndsAt && command.StartsAt < a.EndsAt, ct);
        if (overlaps)
            return Error.Conflict("Availability.Overlaps", "Bu aralık mevcut müsaitliğinizle çakışıyor.");

        var availability = InterviewerAvailability.Create(currentUser.UserId, command.StartsAt, command.EndsAt, clock);
        db.InterviewerAvailabilities.Add(availability);
        await db.SaveChangesAsync(ct);
        return availability.Id;
    }
}
