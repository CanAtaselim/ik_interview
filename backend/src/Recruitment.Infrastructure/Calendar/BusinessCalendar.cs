using Recruitment.Application.Abstractions;
using Recruitment.Domain.Common;

namespace Recruitment.Infrastructure.Calendar;

/// <summary>İş günlerini Türkiye saatine göre hesaplar; yalnızca hafta sonları sayılmaz.</summary>
internal sealed class BusinessCalendar : IBusinessCalendar
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public Task<DateTimeOffset> AddBusinessDays(DateTimeOffset from, int days, CancellationToken ct) =>
        Task.FromResult(BusinessDays.Add(from, days, Zone));
}
