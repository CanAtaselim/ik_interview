using System.Text;
using Microsoft.Extensions.Options;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.Notifications;
using Recruitment.Infrastructure.Email;
using Shouldly;

namespace Recruitment.Api.IntegrationTests.Notifications;

public sealed class IcsGeneratorTests
{
    private static string Create(string summary, string? description = null) =>
        new IcsGenerator(
                Options.Create(new SmtpOptions { FromAddress = "ik@sirket.local", FromName = "İK İşe Alım" }),
                TimeProvider.System)
            .Create(new CalendarInvite(
                "interview-1@ik-ise-alim", 2, CalendarMethod.Request,
                new DateTimeOffset(2026, 10, 21, 14, 0, 0, TimeSpan.FromHours(3)),
                new DateTimeOffset(2026, 10, 21, 15, 0, 0, TimeSpan.FromHours(3)),
                summary, description, "Toplantı Odası; 3. kat", null,
                [new CalendarAttendee("Zeynep Kaya", "zeynep@sirket.local")]));

    [Fact]
    public void Create_WritesUtcTimesAndEscapesText()
    {
        // Uzun satırlar katlanır; içerik katlanmamış haliyle kontrol edilir.
        var ics = Create("Mülakat: Deniz", "Satır 1\nSatır 2").Replace("\r\n ", string.Empty);

        ics.ShouldContain("DTSTART:20261021T110000Z");
        ics.ShouldContain("DTEND:20261021T120000Z");
        ics.ShouldContain("SEQUENCE:2");
        ics.ShouldContain("LOCATION:Toplantı Odası\\; 3. kat");
        ics.ShouldContain("DESCRIPTION:Satır 1\\nSatır 2");
        ics.ShouldContain("ATTENDEE;CN=Zeynep Kaya;ROLE=REQ-PARTICIPANT;RSVP=TRUE:mailto:zeynep@sirket.local");
        ics.ShouldEndWith("END:VCALENDAR\r\n");
    }

    [Fact]
    public void Create_FoldsLongLinesWithoutSplittingTurkishCharacters()
    {
        var summary = string.Concat(Enumerable.Repeat("Çağrı Şükrü Öztürk ğüşiöç ", 10));

        var ics = Create(summary);

        var lines = ics.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        lines.ShouldAllBe(line => Encoding.UTF8.GetByteCount(line) <= 75);
        // Katlanmış satırlar birleştirilince metin aynen geri gelir.
        var unfolded = ics.Replace("\r\n ", string.Empty);
        unfolded.ShouldContain($"SUMMARY:{summary}");
    }
}
