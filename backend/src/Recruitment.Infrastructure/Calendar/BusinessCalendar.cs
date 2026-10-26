using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.Common;
using Recruitment.Infrastructure.Persistence;

namespace Recruitment.Infrastructure.Calendar;

/// <summary>İş günlerini Türkiye saatine ve resmi tatil tablosuna göre hesaplar.</summary>
internal sealed class BusinessCalendar(AppDbContext db) : IBusinessCalendar
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public async Task<DateTimeOffset> AddBusinessDays(DateTimeOffset from, int days, CancellationToken ct)
    {
        var holidays = await db.Holidays.AsNoTracking().ToListAsync(ct);
        return BusinessDays.Add(from, days, Zone, date => holidays.Any(h => h.OccursOn(date)));
    }
}
