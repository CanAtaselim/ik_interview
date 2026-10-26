using Microsoft.Extensions.Time.Testing;
using Recruitment.Domain.Common;
using Recruitment.Domain.Interviews;
using Shouldly;

namespace Recruitment.Domain.Tests.Interviews;

public sealed class InterviewTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero));
    private readonly Guid _hr = Guid.CreateVersion7();
    private readonly Guid _lead = Guid.CreateVersion7();
    private readonly Guid _manager = Guid.CreateVersion7();
    private readonly Guid _room = Guid.CreateVersion7();

    private InterviewPlan Plan(
        TimeSpan? startsIn = null,
        InterviewMode mode = InterviewMode.InPerson,
        int duration = 60,
        Guid[]? interviewers = null) =>
        new(
            Guid.CreateVersion7(),
            mode,
            _clock.GetUtcNow() + (startsIn ?? TimeSpan.FromDays(2)),
            duration,
            _room,
            " https://teams.example.com/abc ",
            " Mimari sorulara ağırlık verin. ",
            interviewers ?? [_lead]);

    private Interview Scheduled(InterviewPlan? plan = null) =>
        Interview.Schedule(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), plan ?? Plan(), _hr, _clock);

    [Fact]
    public void Schedule_SetsTimesAndInterviewers_AndRaisesEvent()
    {
        var interview = Scheduled(Plan(interviewers: [_lead, _manager, _lead]));

        interview.Status.ShouldBe(InterviewStatus.Scheduled);
        interview.EndsAt.ShouldBe(interview.StartsAt.AddMinutes(60));
        interview.InterviewerUserIds.ShouldBe([_lead, _manager]);
        interview.Note.ShouldBe("Mimari sorulara ağırlık verin.");
        // Yüz yüze mülakatın bağlantısı tutulmaz.
        interview.RoomId.ShouldBe(_room);
        interview.MeetingUrl.ShouldBeNull();
        interview.DayReminderSentAt.ShouldBeNull();
        interview.DomainEvents.OfType<InterviewScheduled>().Single().InterviewerUserIds.ShouldBe([_lead, _manager]);
    }

    [Fact]
    public void Schedule_Online_KeepsLinkAndDropsRoom()
    {
        var interview = Scheduled(Plan(mode: InterviewMode.Online));

        interview.MeetingUrl.ShouldBe("https://teams.example.com/abc");
        interview.RoomId.ShouldBeNull();
    }

    [Fact]
    public void Schedule_RejectsPastTimeInvalidDurationAndMissingInterviewers()
    {
        Should.Throw<DomainException>(() => Scheduled(Plan(startsIn: TimeSpan.FromMinutes(-1))))
            .Message.ShouldBe("Geçmiş bir saate mülakat planlanamaz.");
        Should.Throw<DomainException>(() => Scheduled(Plan(duration: 10)));
        Should.Throw<DomainException>(() => Scheduled(Plan(duration: 481)));
        Should.Throw<DomainException>(() => Scheduled(Plan(interviewers: [])))
            .Message.ShouldBe("En az bir mülakatçı seçmelisiniz.");
        Should.Throw<DomainException>(() => Scheduled(Plan(interviewers: [.. Enumerable.Range(0, 11).Select(_ => Guid.CreateVersion7())])));
    }

    [Fact]
    public void Reschedule_ReplacesInterviewers_AndReportsPreviousOnes()
    {
        var interview = Scheduled();
        var previousStart = interview.StartsAt;

        interview.Reschedule(Plan(startsIn: TimeSpan.FromDays(3), interviewers: [_manager]), _hr, _clock);

        interview.InterviewerUserIds.ShouldBe([_manager]);
        interview.StartsAt.ShouldBe(previousStart.AddDays(1));
        var rescheduled = interview.DomainEvents.OfType<InterviewRescheduled>().Single();
        rescheduled.PreviousStartsAt.ShouldBe(previousStart);
        rescheduled.StartsAt.ShouldBe(previousStart.AddDays(1));
        rescheduled.PreviousInterviewerUserIds.ShouldBe([_lead]);
        rescheduled.InterviewerUserIds.ShouldBe([_manager]);
    }

    [Fact]
    public void Cancel_RequiresReason_AndClosesInterview()
    {
        var interview = Scheduled();

        Should.Throw<DomainException>(() => interview.Cancel(" ", _hr, _clock));
        interview.Cancel(" Aday başka teklif kabul etti. ", _hr, _clock);

        interview.Status.ShouldBe(InterviewStatus.Cancelled);
        interview.CancellationReason.ShouldBe("Aday başka teklif kabul etti.");
        Should.Throw<DomainException>(() => interview.Reschedule(Plan(), _hr, _clock))
            .Message.ShouldBe("Yalnızca planlanmış (sonuçlanmamış) mülakat değiştirilebilir.");
    }

    [Fact]
    public void RecordOutcome_OnlyAfterStart_AndOnlyCompletedOrNoShow()
    {
        var interview = Scheduled(Plan(startsIn: TimeSpan.FromHours(2)));

        Should.Throw<DomainException>(() => interview.RecordOutcome(InterviewStatus.Completed, _hr, _clock))
            .Message.ShouldBe("Mülakatın sonucu, başlangıç saatinden sonra girilebilir.");
        _clock.Advance(TimeSpan.FromHours(2));
        Should.Throw<DomainException>(() => interview.RecordOutcome(InterviewStatus.Cancelled, _hr, _clock));

        interview.RecordOutcome(InterviewStatus.NoShow, _hr, _clock);

        interview.Status.ShouldBe(InterviewStatus.NoShow);
        interview.OutcomeRecordedAt.ShouldBe(_clock.GetUtcNow());
        interview.DomainEvents.OfType<InterviewOutcomeRecorded>().Single().Status.ShouldBe(InterviewStatus.NoShow);
    }

    [Fact]
    public void Reminders_GoOutOnceAt24HoursAndOnceAt1Hour()
    {
        var interview = Scheduled(Plan(startsIn: TimeSpan.FromDays(2)));

        interview.SendDueReminder(_clock).ShouldBeFalse();
        _clock.Advance(TimeSpan.FromHours(25));
        interview.SendDueReminder(_clock).ShouldBeTrue();
        interview.SendDueReminder(_clock).ShouldBeFalse();
        _clock.Advance(TimeSpan.FromHours(22) + TimeSpan.FromMinutes(5));
        interview.SendDueReminder(_clock).ShouldBeTrue();
        interview.SendDueReminder(_clock).ShouldBeFalse();

        interview.DomainEvents.OfType<InterviewReminderDue>().Select(e => e.HoursBefore).ShouldBe([24, 1]);
    }

    [Fact]
    public void Reminders_ForInterviewPlannedWithinADay_SkipThe24HourOne()
    {
        var interview = Scheduled(Plan(startsIn: TimeSpan.FromHours(5)));

        interview.DayReminderSentAt.ShouldNotBeNull();
        interview.SendDueReminder(_clock).ShouldBeFalse();
        _clock.Advance(TimeSpan.FromHours(4) + TimeSpan.FromMinutes(30));
        interview.SendDueReminder(_clock).ShouldBeTrue();
        interview.DomainEvents.OfType<InterviewReminderDue>().Single().HoursBefore.ShouldBe(1);
    }

    [Fact]
    public void Reschedule_ToNewTime_ResetsReminders()
    {
        var interview = Scheduled(Plan(startsIn: TimeSpan.FromHours(5)));

        interview.Reschedule(Plan(startsIn: TimeSpan.FromDays(3)), _hr, _clock);

        interview.DayReminderSentAt.ShouldBeNull();
        interview.HourReminderSentAt.ShouldBeNull();
    }

    [Fact]
    public void Availability_ValidatesRange()
    {
        var now = _clock.GetUtcNow();

        var slot = InterviewerAvailability.Create(_lead, now.AddHours(1), now.AddHours(3), _clock);
        slot.Overlaps(now.AddHours(2), now.AddHours(4)).ShouldBeTrue();
        slot.Overlaps(now.AddHours(3), now.AddHours(4)).ShouldBeFalse();

        Should.Throw<DomainException>(() => InterviewerAvailability.Create(_lead, now.AddHours(3), now.AddHours(1), _clock));
        Should.Throw<DomainException>(() => InterviewerAvailability.Create(_lead, now.AddHours(1), now.AddHours(16), _clock));
        Should.Throw<DomainException>(() => InterviewerAvailability.Create(_lead, now.AddHours(-3), now.AddHours(-1), _clock));
    }
}
