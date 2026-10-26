using Recruitment.Domain.Common;

namespace Recruitment.Domain.Calendar;

/// <summary>
/// Resmi tatil. <see cref="Year"/> boşsa her yıl aynı gün tekrarlanır (ör. 29 Ekim); doluysa
/// yalnızca o yıl geçerlidir (ör. tarihi her yıl değişen dini bayramlar).
/// </summary>
public sealed class Holiday : Entity
{
    private Holiday() { }

    public string Name { get; private set; } = default!;
    public int Month { get; private set; }
    public int Day { get; private set; }
    public int? Year { get; private set; }

    public static Holiday Create(string name, int month, int day, int? year = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Tatilin adı boş olamaz.");
        if (month is < 1 or > 12 || day < 1 || day > DateTime.DaysInMonth(year ?? 2024, month))
            throw new DomainException("Tatil tarihi geçerli değil.");

        return new Holiday { Name = name.Trim(), Month = month, Day = day, Year = year };
    }

    public bool OccursOn(DateOnly date) =>
        date.Month == Month && date.Day == Day && (Year is null || Year == date.Year);
}
