using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Calendar;

namespace Recruitment.Application.Features.Settings.Holidays;

/// <param name="Date">Seçilen yıldaki tarih.</param>
/// <param name="IsRecurring">Her yıl aynı güne denk gelir (ör. 29 Ekim); değilse yalnızca o yıl geçerlidir.</param>
public sealed record HolidayDto(Guid Id, string Name, DateOnly Date, bool IsRecurring);

/// <summary>Seçilen yılda resmi tatil olan günler (tekrarlayanlar dahil), tarihe göre.</summary>
public sealed record GetHolidaysQuery(int Year) : IQuery<IReadOnlyList<HolidayDto>>;

internal sealed class GetHolidaysQueryValidator : AbstractValidator<GetHolidaysQuery>
{
    public GetHolidaysQueryValidator() =>
        RuleFor(q => q.Year).InclusiveBetween(2000, 2100).WithMessage("Yıl 2000 ile 2100 arasında olmalı.");
}

internal sealed class GetHolidaysQueryHandler(IAppDbContext db) : IQueryHandler<GetHolidaysQuery, IReadOnlyList<HolidayDto>>
{
    public async Task<Result<IReadOnlyList<HolidayDto>>> Handle(GetHolidaysQuery query, CancellationToken ct) =>
        (await db.Holidays.AsNoTracking().Where(h => h.Year == null || h.Year == query.Year).ToListAsync(ct))
            .Where(h => h.Month != 2 || h.Day <= DateTime.DaysInMonth(query.Year, 2))
            .Select(h => new HolidayDto(h.Id, h.Name, new DateOnly(query.Year, h.Month, h.Day), h.Year is null))
            .OrderBy(h => h.Date).ThenBy(h => h.Name)
            .ToList();
}

/// <summary>Resmi tatil ekler. Yeni tatil bundan sonra hesaplanan son tarihleri etkiler.</summary>
public sealed record CreateHolidayCommand(string Name, DateOnly Date, bool Recurring) : ICommand<Guid>;

internal sealed class CreateHolidayCommandValidator : AbstractValidator<CreateHolidayCommand>
{
    public CreateHolidayCommandValidator()
    {
        RuleFor(c => c.Name)
            .NotEmpty().WithMessage("Tatilin adını yazmalısınız.")
            .MaximumLength(100).WithMessage("Ad en fazla 100 karakter olabilir.");
        RuleFor(c => c.Date.Year).InclusiveBetween(2000, 2100).WithName("date").WithMessage("Yıl 2000 ile 2100 arasında olmalı.");
        RuleFor(c => c)
            .Must(c => !(c.Recurring && c.Date.Month == 2 && c.Date.Day == 29))
            .WithName("date")
            .WithMessage("Her yıl tekrarlanan tatil 29 Şubat olamaz; o yıla özel ekleyin.");
    }
}

internal sealed class CreateHolidayCommandHandler(IAppDbContext db) : ICommandHandler<CreateHolidayCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateHolidayCommand command, CancellationToken ct)
    {
        var date = command.Date;
        int? year = command.Recurring ? null : date.Year;

        // Aynı gün için hem yıla özel hem tekrarlayan kayıt tutulmaz.
        var exists = await db.Holidays.AnyAsync(
            h => h.Month == date.Month && h.Day == date.Day && (h.Year == null || h.Year == date.Year), ct);
        if (exists)
            return SettingsErrors.HolidayDuplicate;

        var holiday = Holiday.Create(command.Name, date.Month, date.Day, year);
        db.Holidays.Add(holiday);
        await db.SaveChangesAsync(ct);
        return holiday.Id;
    }
}

public sealed record DeleteHolidayCommand(Guid Id) : ICommand<Unit>;

internal sealed class DeleteHolidayCommandHandler(IAppDbContext db) : ICommandHandler<DeleteHolidayCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteHolidayCommand command, CancellationToken ct)
    {
        var holiday = await db.Holidays.FirstOrDefaultAsync(h => h.Id == command.Id, ct);
        if (holiday is null)
            return SettingsErrors.HolidayNotFound;

        db.Holidays.Remove(holiday);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
