using Microsoft.Extensions.Time.Testing;
using Recruitment.Domain.Common;
using Recruitment.Domain.Questions;
using Shouldly;

namespace Recruitment.Domain.Tests.Questions;

public sealed class QuestionTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 19, 9, 0, 0, TimeSpan.Zero));
    private readonly Guid _department = Guid.CreateVersion7();
    private readonly Guid _competency = Guid.CreateVersion7();
    private readonly Guid _lead = Guid.CreateVersion7();

    private QuestionContent Content(string text = " Bir REST API'yi nasıl sürümlersiniz? ", int difficulty = 2, decimal weight = 1) =>
        new(_department, " Backend Geliştirici ", null, _competency, QuestionType.Technical, difficulty, weight, text, " URL veya başlık ile ");

    [Fact]
    public void Create_StartsActiveAtVersionOne_WithTrimmedText()
    {
        var question = Question.Create(Content(), _lead, _clock);

        question.IsActive.ShouldBeTrue();
        question.Version.ShouldBe(1);
        question.Text.ShouldBe("Bir REST API'yi nasıl sürümlersiniz?");
        question.PositionTitle.ShouldBe("Backend Geliştirici");
        question.Guidance.ShouldBe("URL veya başlık ile");
        var version = question.Versions.Single();
        version.Number.ShouldBe(1);
        version.Text.ShouldBe(question.Text);
        version.CreatedByUserId.ShouldBe(_lead);
    }

    [Fact]
    public void Update_WithChanges_CreatesNewVersion_AndKeepsOldText()
    {
        var question = Question.Create(Content(), _lead, _clock);
        _clock.Advance(TimeSpan.FromDays(1));

        question.Update(Content(text: "REST API sürümleme stratejileri nelerdir?", difficulty: 3), _lead, _clock).ShouldBeTrue();

        question.Version.ShouldBe(2);
        question.Difficulty.ShouldBe(3);
        question.Versions.Select(v => v.Text).ShouldBe(["Bir REST API'yi nasıl sürümlersiniz?", "REST API sürümleme stratejileri nelerdir?"]);
        question.Versions.Last().CreatedAt.ShouldBe(_clock.GetUtcNow());
    }

    [Fact]
    public void Update_WithoutChanges_DoesNotCreateVersion()
    {
        var question = Question.Create(Content(), _lead, _clock);

        question.Update(Content(), _lead, _clock).ShouldBeFalse();

        question.Version.ShouldBe(1);
        question.Versions.Count.ShouldBe(1);
    }

    [Theory]
    [InlineData("", 2, 1)]
    [InlineData("Soru", 0, 1)]
    [InlineData("Soru", 4, 1)]
    [InlineData("Soru", 2, 0.4)]
    [InlineData("Soru", 2, 5.5)]
    public void Create_WithInvalidContent_Throws(string text, int difficulty, double weight)
    {
        Should.Throw<DomainException>(() => Question.Create(Content(text, difficulty, (decimal)weight), _lead, _clock));
    }

    [Fact]
    public void Template_KeepsQuestionOrder_AndRequiresQuestions()
    {
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();

        var template = InterviewTemplate.Create(" Backend teknik ", _department, null, null, [second, first, second]);

        template.Name.ShouldBe("Backend teknik");
        template.IsActive.ShouldBeTrue();
        template.OrderedQuestionIds.ShouldBe([second, first]);

        template.Update("Backend teknik", _department, null, null, [first]);
        template.OrderedQuestionIds.ShouldBe([first]);

        Should.Throw<DomainException>(() => template.Update("Boş", _department, null, null, []))
            .Message.ShouldBe("Şablona en az bir soru eklemelisiniz.");
        Should.Throw<DomainException>(() => InterviewTemplate.Create(" ", _department, null, null, [first]));
    }
}
