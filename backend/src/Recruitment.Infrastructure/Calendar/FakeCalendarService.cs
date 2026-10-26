using Recruitment.Application.Abstractions;

namespace Recruitment.Infrastructure.Calendar;

/// <summary>
/// Exchange olmadan geliştirme için sabit meşgul saatler. Yalnızca Development ortamında ve
/// <c>Graph:UseFake=true</c> iken kullanılır. Saatler Türkiye saatine göre her gün tekrarlanır:
/// "mudur" 13:00–14:00, "takimlideri" 10:00–11:30 meşgul; "calisan" posta kutusu yok; diğerleri boş.
/// </summary>
internal sealed class FakeCalendarService : ICalendarService
{
    private static readonly TimeZoneInfo Zone = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    private static readonly Dictionary<string, (TimeSpan Start, TimeSpan End)> Daily = new(StringComparer.OrdinalIgnoreCase)
    {
        ["mudur"] = (TimeSpan.FromHours(13), TimeSpan.FromHours(14)),
        ["takimlideri"] = (TimeSpan.FromHours(10), TimeSpan.FromHours(11.5)),
    };

    public bool IsEnabled => true;

    public Task<IReadOnlyDictionary<string, MailboxBusy>> GetBusy(
        IReadOnlyCollection<string> mailboxes, DateTimeOffset from, DateTimeOffset to, CancellationToken ct)
    {
        var result = new Dictionary<string, MailboxBusy>(StringComparer.OrdinalIgnoreCase);
        foreach (var mailbox in mailboxes)
        {
            var name = mailbox.Split('@')[0];
            if (name.Equals("calisan", StringComparison.OrdinalIgnoreCase))
            {
                result[mailbox] = MailboxBusy.Of(OutlookStatus.NoMailbox);
                continue;
            }

            var slots = new List<BusySlot>();
            if (Daily.TryGetValue(name, out var window))
            {
                var firstDay = TimeZoneInfo.ConvertTime(from, Zone).Date;
                var lastDay = TimeZoneInfo.ConvertTime(to, Zone).Date;
                for (var day = firstDay; day <= lastDay; day = day.AddDays(1))
                {
                    if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                        continue;

                    var start = new DateTimeOffset(day + window.Start, Zone.GetUtcOffset(day + window.Start));
                    var end = new DateTimeOffset(day + window.End, Zone.GetUtcOffset(day + window.End));
                    if (start < to && end > from)
                        slots.Add(new BusySlot(start, end));
                }
            }

            result[mailbox] = new MailboxBusy(OutlookStatus.Ok, slots);
        }

        return Task.FromResult<IReadOnlyDictionary<string, MailboxBusy>>(result);
    }
}
