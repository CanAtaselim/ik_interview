using Microsoft.Extensions.Time.Testing;
using Recruitment.Domain.Common;
using Recruitment.Domain.CvReviews;
using Shouldly;

namespace Recruitment.Domain.Tests.CvReviews;

public sealed class CvReviewTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 9, 9, 0, 0, TimeSpan.Zero));
    private readonly Guid _candidate = Guid.CreateVersion7();
    private readonly Guid _lead = Guid.CreateVersion7();
    private readonly Guid _hr = Guid.CreateVersion7();
    private readonly Guid _rejectionReason = Guid.CreateVersion7();

    private CvReview Suggested() => CvReview.Suggest(
        _candidate, Guid.CreateVersion7(), Guid.CreateVersion7(), _lead, _hr, "  Go deneyimine bakar mısın?  ",
        dueAt: _clock.GetUtcNow().AddDays(3), escalateAt: _clock.GetUtcNow().AddDays(5), _clock);

    [Fact]
    public void Suggest_StartsPending_AndRaisesRequestedEvent()
    {
        var review = Suggested();

        review.Decision.ShouldBe(CvReviewDecision.Pending);
        review.IsOpen.ShouldBeTrue();
        review.SuggestionNote.ShouldBe("Go deneyimine bakar mısın?");
        review.SuggestedAt.ShouldBe(_clock.GetUtcNow());
        review.DecidedAt.ShouldBeNull();
        var requested = review.DomainEvents.OfType<CvReviewRequested>().Single();
        requested.ReviewerUserId.ShouldBe(_lead);
        requested.ActorUserId.ShouldBe(_hr);
    }

    [Fact]
    public void Decide_Positive_IsFinal()
    {
        var review = Suggested();
        _clock.Advance(TimeSpan.FromHours(4));

        review.Decide(CvReviewDecision.Positive, " Teknik olarak uygun. ", _rejectionReason, _clock);

        review.Decision.ShouldBe(CvReviewDecision.Positive);
        review.DecisionReason.ShouldBe("Teknik olarak uygun.");
        review.DecidedAt.ShouldBe(_clock.GetUtcNow());
        // Ret nedeni yalnızca olumsuz kararda saklanır.
        review.RejectionReasonId.ShouldBeNull();
        review.IsOpen.ShouldBeFalse();
        review.DomainEvents.OfType<CvReviewDecided>().Single().Decision.ShouldBe(CvReviewDecision.Positive);
        Should.Throw<DomainException>(() => review.Decide(CvReviewDecision.Negative, "Vazgeçtim", _rejectionReason, _clock))
            .Message.ShouldBe("Bu değerlendirme için karar verilmiş; karar değiştirilemez.");
    }

    [Fact]
    public void Decide_Negative_RequiresRejectionReason()
    {
        var review = Suggested();

        Should.Throw<DomainException>(() => review.Decide(CvReviewDecision.Negative, "Deneyimi az", null, _clock))
            .Message.ShouldBe("Olumsuz karar için ret nedeni seçmelisiniz.");

        review.Decide(CvReviewDecision.Negative, "Deneyimi az", _rejectionReason, _clock);
        review.RejectionReasonId.ShouldBe(_rejectionReason);
        review.IsOpen.ShouldBeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Decide_WithoutReason_Throws(string reason)
    {
        Should.Throw<DomainException>(() => Suggested().Decide(CvReviewDecision.Positive, reason, null, _clock))
            .Message.ShouldBe("Kararınızın gerekçesini yazmalısınız.");
    }

    [Fact]
    public void Decide_OnHold_KeepsReviewOpen_AndCanBeFinalizedLater()
    {
        var review = Suggested();

        review.Decide(CvReviewDecision.OnHold, "Önce mevcut adayları bitirelim", null, _clock);

        review.IsOpen.ShouldBeTrue();
        review.CanBeDecidedBy(_lead).ShouldBeTrue();
        review.CanBeDecidedBy(_hr).ShouldBeFalse();
        Should.Throw<DomainException>(() => review.Decide(CvReviewDecision.OnHold, "Hâlâ bekliyor", null, _clock));
        Should.Throw<DomainException>(() => review.Decide(CvReviewDecision.Pending, "Geri al", null, _clock));

        review.Decide(CvReviewDecision.Positive, "Görüşelim", null, _clock);
        review.Decision.ShouldBe(CvReviewDecision.Positive);
    }

    [Fact]
    public void Reminder_IsSentOnceAfterDueDate()
    {
        var review = Suggested();

        review.RemindIfOverdue(_clock).ShouldBeFalse();
        _clock.Advance(TimeSpan.FromDays(3) + TimeSpan.FromMinutes(1));

        review.IsOverdue(_clock).ShouldBeTrue();
        review.RemindIfOverdue(_clock).ShouldBeTrue();
        review.RemindIfOverdue(_clock).ShouldBeFalse();
        review.DomainEvents.OfType<CvReviewOverdue>().Single().ReviewerUserId.ShouldBe(_lead);
        // Gecikme bildirimi için süre henüz dolmadı.
        review.EscalateIfDue(_clock).ShouldBeFalse();
    }

    [Fact]
    public void Escalation_GoesToSuggestingHrOnce()
    {
        var review = Suggested();
        _clock.Advance(TimeSpan.FromDays(5) + TimeSpan.FromMinutes(1));

        review.EscalateIfDue(_clock).ShouldBeTrue();
        review.EscalateIfDue(_clock).ShouldBeFalse();
        review.DomainEvents.OfType<CvReviewEscalated>().Single().SuggestedByUserId.ShouldBe(_hr);
    }

    [Fact]
    public void DecidedOrOnHoldReview_IsNeverOverdue()
    {
        var decided = Suggested();
        decided.Decide(CvReviewDecision.Positive, "Uygun", null, _clock);
        var onHold = Suggested();
        onHold.Decide(CvReviewDecision.OnHold, "Bekleyelim", null, _clock);
        _clock.Advance(TimeSpan.FromDays(30));

        foreach (var review in new[] { decided, onHold })
        {
            review.IsOverdue(_clock).ShouldBeFalse();
            review.RemindIfOverdue(_clock).ShouldBeFalse();
            review.EscalateIfDue(_clock).ShouldBeFalse();
        }
    }
}
