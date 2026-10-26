using System.Text.Json;
using Recruitment.Api.IntegrationTests.Infrastructure;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.Users;
using Shouldly;

namespace Recruitment.Api.IntegrationTests.Interviews;

public sealed class OutlookAvailabilityTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/v1/interviews/schedules";

    private static string Range(DateTimeOffset day) =>
        $"from={Uri.EscapeDataString(day.ToString("O"))}&to={Uri.EscapeDataString(day.AddDays(1).ToString("O"))}";

    private static JsonElement Person(JsonElement schedule, Guid userId) =>
        schedule.GetProperty("interviewers").EnumerateArray().Single(u => u.GetProperty("userId").GetGuid() == userId);

    [Fact]
    public async Task Schedules_ReportOutlookAsNotConfigured_WhenDisabled()
    {
        factory.Calendar.IsEnabled = false;
        var hr = await factory.LoginAs("İnsan Kaynakları", Role.HrSpecialist);
        var lead = await factory.LoginAs("Yazılım", Role.TeamLead);
        var day = DateTimeOffset.UtcNow.Date.AddDays(10);

        var schedule = await hr.GetJson($"{Url}?{Range(day)}&userIds={lead.Id}");

        var person = Person(schedule, lead.Id);
        person.GetProperty("outlook").GetString().ShouldBe("NotConfigured");
        person.GetProperty("outlookBusy").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Schedules_ShowOutlookBusyTimes_AndPerPersonStatus()
    {
        factory.Calendar.IsEnabled = true;
        var hr = await factory.LoginAs("İnsan Kaynakları", Role.HrSpecialist);
        var busyLead = await factory.LoginAs("Yazılım", Role.TeamLead);
        var unreachable = await factory.LoginAs("Yazılım", Role.TeamLead);
        var noMailbox = await factory.LoginAs("Yazılım", Role.TeamLead);
        var free = await factory.LoginAs("Yazılım", Role.TeamLead);
        var day = new DateTimeOffset(DateTimeOffset.UtcNow.Date.AddDays(12), TimeSpan.Zero);

        factory.Calendar.Set($"{busyLead.Username}@test.local", new MailboxBusy(OutlookStatus.Ok, [new BusySlot(day.AddHours(7), day.AddHours(8))]));
        factory.Calendar.Set($"{unreachable.Username}@test.local", MailboxBusy.Of(OutlookStatus.Unavailable));
        factory.Calendar.Set($"{noMailbox.Username}@test.local", MailboxBusy.Of(OutlookStatus.NoMailbox));

        var schedule = await hr.GetJson(
            $"{Url}?{Range(day)}&userIds={busyLead.Id}&userIds={unreachable.Id}&userIds={noMailbox.Id}&userIds={free.Id}");

        var busy = Person(schedule, busyLead.Id);
        busy.GetProperty("outlook").GetString().ShouldBe("Ok");
        var slot = busy.GetProperty("outlookBusy").EnumerateArray().Single();
        slot.GetProperty("startsAt").GetDateTimeOffset().ShouldBe(day.AddHours(7));
        slot.GetProperty("endsAt").GetDateTimeOffset().ShouldBe(day.AddHours(8));
        // Konu/katılımcı bilgisi hiçbir zaman taşınmaz.
        slot.EnumerateObject().Select(p => p.Name).Order().ShouldBe(["endsAt", "startsAt"]);

        Person(schedule, unreachable.Id).GetProperty("outlook").GetString().ShouldBe("Unavailable");
        Person(schedule, noMailbox.Id).GetProperty("outlook").GetString().ShouldBe("NoMailbox");
        var freePerson = Person(schedule, free.Id);
        freePerson.GetProperty("outlook").GetString().ShouldBe("Ok");
        freePerson.GetProperty("outlookBusy").GetArrayLength().ShouldBe(0);
    }
}
