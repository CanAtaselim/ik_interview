using Microsoft.Extensions.Time.Testing;
using Recruitment.Domain.Common;
using Recruitment.Domain.Scorecards;
using Shouldly;

namespace Recruitment.Domain.Tests.Scorecards;

public sealed class ScorecardTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero));
    private readonly Guid _technical = Guid.CreateVersion7();
    private readonly Guid _communication = Guid.CreateVersion7();

    private Scorecard Started() => Scorecard.Start(
        Guid.CreateVersion7(),
        Guid.CreateVersion7(),
        [
            new QuestionSnapshot(Guid.CreateVersion7(), 3, "REST sürümleme?", "URL veya başlık", _technical, 2m),
            new QuestionSnapshot(Guid.CreateVersion7(), 1, "Zor bir ekip anı?", null, _communication, 1m),
        ]);

    private static List<ScorecardItemInput> Inputs(Scorecard scorecard, params int?[] scores) =>
        [.. scorecard.OrderedItems.Select((item, index) => new ScorecardItemInput(item.Id, null, null, null, scores[index], null))];

    [Fact]
    public void Start_CopiesTemplateQuestionsAsTheyAreNow()
    {
        var scorecard = Started();

        scorecard.Status.ShouldBe(ScorecardStatus.Draft);
        scorecard.OrderedItems.Select(i => i.Text).ShouldBe(["REST sürümleme?", "Zor bir ekip anı?"]);
        var first = scorecard.OrderedItems[0];
        first.QuestionVersion.ShouldBe(3);
        first.Guidance.ShouldBe("URL veya başlık");
        first.Weight.ShouldBe(2m);
        first.IsCustom.ShouldBeFalse();
    }

    [Fact]
    public void UpdateDraft_ScoresReordersRemovesAndAddsCustomQuestions()
    {
        var scorecard = Started();
        var (rest, team) = (scorecard.OrderedItems[0], scorecard.OrderedItems[1]);

        scorecard.UpdateDraft(
            [
                new ScorecardItemInput(team.Id, "Değiştirilemez", null, null, 4, " İyi örnek verdi "),
                new ScorecardItemInput(null, " Kafka deneyimi? ", _technical, 1.5m, 5, null),
            ],
            " Güçlü aday ",
            Recommendation.Hire);

        scorecard.OrderedItems.Select(i => i.Text).ShouldBe(["Zor bir ekip anı?", "Kafka deneyimi?"]);
        scorecard.Items.ShouldNotContain(rest);
        var custom = scorecard.OrderedItems[1];
        custom.IsCustom.ShouldBeTrue();
        custom.Weight.ShouldBe(1.5m);
        scorecard.OrderedItems[0].Note.ShouldBe("İyi örnek verdi");
        scorecard.GeneralNote.ShouldBe("Güçlü aday");
        scorecard.Recommendation.ShouldBe(Recommendation.Hire);
    }

    [Fact]
    public void UpdateDraft_WithClientGeneratedId_AddsCustomQuestionOnlyOnce()
    {
        var scorecard = Started();
        var customId = Guid.NewGuid();
        List<ScorecardItemInput> Inputs(int? score) =>
        [
            .. scorecard.OrderedItems.Where(i => !i.IsCustom).Select(i => new ScorecardItemInput(i.Id, null, null, null, null, null)),
            new ScorecardItemInput(customId, "Kafka deneyimi?", null, null, score, null),
        ];

        // Otomatik kayıt aynı listeyi tekrar gönderir; soru bir kez eklenir, sonra güncellenir.
        scorecard.UpdateDraft(Inputs(null), null, null);
        scorecard.UpdateDraft(Inputs(4), null, null);

        scorecard.Items.Count.ShouldBe(3);
        var custom = scorecard.Items.Single(i => i.Id == customId);
        custom.IsCustom.ShouldBeTrue();
        custom.Score.ShouldBe(4);
    }

    [Fact]
    public void UpdateDraft_RejectsOutOfRangeScoreAndEmptyCustomQuestion()
    {
        var scorecard = Started();

        Should.Throw<DomainException>(() => scorecard.UpdateDraft(Inputs(scorecard, 6, null), null, null));
        Should.Throw<DomainException>(() => scorecard.UpdateDraft([new ScorecardItemInput(null, " ", null, null, 3, null)], null, null))
            .Message.ShouldBe("Eklediğiniz sorunun metnini yazmalısınız.");
        Should.Throw<DomainException>(() => scorecard.UpdateDraft([new ScorecardItemInput(Guid.NewGuid(), null, null, null, 3, null)], null, null));
    }

    [Fact]
    public void Submit_NeedsRecommendationAndAScore_ThenLocks()
    {
        var scorecard = Started();

        scorecard.UpdateDraft(Inputs(scorecard, null, null), null, Recommendation.Hire);
        Should.Throw<DomainException>(() => scorecard.Submit(_clock))
            .Message.ShouldBe("Göndermeden önce en az bir soruya puan vermelisiniz.");
        scorecard.UpdateDraft(Inputs(scorecard, 4, null), null, null);
        Should.Throw<DomainException>(() => scorecard.Submit(_clock))
            .Message.ShouldBe("Göndermeden önce genel önerinizi seçmelisiniz.");

        scorecard.UpdateDraft(Inputs(scorecard, 4, null), null, Recommendation.StrongHire);
        scorecard.Submit(_clock);

        scorecard.IsSubmitted.ShouldBeTrue();
        scorecard.SubmittedAt.ShouldBe(_clock.GetUtcNow());
        scorecard.DomainEvents.OfType<ScorecardSubmitted>().Single().Recommendation.ShouldBe(Recommendation.StrongHire);
        Should.Throw<DomainException>(() => scorecard.UpdateDraft(Inputs(scorecard, 1, 1), null, Recommendation.NoHire))
            .Message.ShouldBe("Gönderilmiş değerlendirme değiştirilemez.");
    }

    [Fact]
    public void Reopen_RequiresReason_AndUnlocks()
    {
        var scorecard = Started();
        scorecard.UpdateDraft(Inputs(scorecard, 3, 3), null, Recommendation.Undecided);
        Should.Throw<DomainException>(() => scorecard.Reopen("Gerekçe", Guid.NewGuid(), _clock));
        scorecard.Submit(_clock);

        Should.Throw<DomainException>(() => scorecard.Reopen(" ", Guid.NewGuid(), _clock));
        scorecard.Reopen(" Yanlış adaya puan verilmiş ", Guid.NewGuid(), _clock);

        scorecard.Status.ShouldBe(ScorecardStatus.Draft);
        scorecard.ReopenReason.ShouldBe("Yanlış adaya puan verilmiş");
        scorecard.DomainEvents.OfType<ScorecardReopened>().Single().Reason.ShouldBe("Yanlış adaya puan verilmiş");
    }

    [Fact]
    public void WeightedAverage_IgnoresUnscoredItems()
    {
        var calculator = new WeightedAverageCalculator();
        var scorecard = Started();

        scorecard.WeightedScore(calculator).ShouldBeNull();
        // (5×2 + 2×1) / 3 = 4
        scorecard.UpdateDraft(Inputs(scorecard, 5, 2), null, null);
        scorecard.WeightedScore(calculator).ShouldBe(4m);
        scorecard.UpdateDraft(Inputs(scorecard, null, 2), null, null);
        scorecard.WeightedScore(calculator).ShouldBe(2m);
        calculator.Calculate([new WeightedScore(3, 1), new WeightedScore(4, 2)]).ShouldBe(3.67m);
    }
}
