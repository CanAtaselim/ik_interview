using System.Globalization;
using System.Text;
using Microsoft.Extensions.Options;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.Notifications;

namespace Recruitment.Infrastructure.Email;

/// <summary>RFC 5545 takvim daveti (.ics). Zamanlar UTC yazılır; takvim uygulaması yerel saate çevirir.</summary>
internal sealed class IcsGenerator(IOptions<SmtpOptions> smtp, TimeProvider clock) : IIcsGenerator
{
    public string Create(CalendarInvite invite)
    {
        var cancel = invite.Method == CalendarMethod.Cancel;
        var lines = new List<string>
        {
            "BEGIN:VCALENDAR",
            "PRODID:-//IK Ise Alim//TR",
            "VERSION:2.0",
            "CALSCALE:GREGORIAN",
            $"METHOD:{(cancel ? "CANCEL" : "REQUEST")}",
            "BEGIN:VEVENT",
            $"UID:{invite.Uid}",
            $"SEQUENCE:{invite.Sequence}",
            $"DTSTAMP:{Utc(clock.GetUtcNow())}",
            $"DTSTART:{Utc(invite.StartsAt)}",
            $"DTEND:{Utc(invite.EndsAt)}",
            $"SUMMARY:{Escape(invite.Summary)}",
            $"STATUS:{(cancel ? "CANCELLED" : "CONFIRMED")}",
        };

        if (!string.IsNullOrWhiteSpace(invite.Description))
            lines.Add($"DESCRIPTION:{Escape(invite.Description)}");
        if (!string.IsNullOrWhiteSpace(invite.Location))
            lines.Add($"LOCATION:{Escape(invite.Location)}");
        if (!string.IsNullOrWhiteSpace(invite.Url))
            lines.Add($"URL:{invite.Url}");

        var organizer = smtp.Value;
        if (!string.IsNullOrWhiteSpace(organizer.FromAddress))
            lines.Add($"ORGANIZER;CN={Escape(organizer.FromName)}:mailto:{organizer.FromAddress}");
        lines.AddRange(invite.Attendees.Select(a =>
            $"ATTENDEE;CN={Escape(a.Name)};ROLE=REQ-PARTICIPANT;RSVP=TRUE:mailto:{a.Email}"));

        lines.Add("END:VEVENT");
        lines.Add("END:VCALENDAR");
        return string.Join("\r\n", lines.SelectMany(Fold)) + "\r\n";
    }

    private static string Utc(DateTimeOffset value) =>
        value.UtcDateTime.ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\").Replace(";", "\\;").Replace(",", "\\,").Replace("\r\n", "\\n").Replace("\n", "\\n");

    // Satırlar 75 bayttan uzun olmamalı; devamı boşlukla başlayan yeni satıra katlanır (UTF-8 sınırına dikkat ederek).
    private static IEnumerable<string> Fold(string line)
    {
        const int limit = 73;
        var builder = new StringBuilder();
        var bytes = 0;
        var first = true;
        foreach (var rune in line.EnumerateRunes())
        {
            var size = rune.Utf8SequenceLength;
            if (bytes + size > limit)
            {
                yield return (first ? string.Empty : " ") + builder;
                builder.Clear();
                bytes = 0;
                first = false;
            }
            builder.Append(rune.ToString());
            bytes += size;
        }
        yield return (first ? string.Empty : " ") + builder;
    }
}
