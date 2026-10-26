using Microsoft.Extensions.Time.Testing;
using Recruitment.Domain.Common;
using Recruitment.Domain.Pipeline;
using Recruitment.Domain.Pipeline.Events;
using Shouldly;

namespace Recruitment.Domain.Tests.Pipeline;

public sealed class PipelineTests
{
    private static readonly StageTransitionContext NoObstacles = new(true, 0, false);

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 9, 9, 0, 0, TimeSpan.Zero));
    private readonly PipelineTemplate _template = PipelineTemplate.CreateDefault();
    private readonly Guid _candidateId = Guid.CreateVersion7();
    private readonly Guid _positionId = Guid.CreateVersion7();
    private readonly Guid _hr = Guid.CreateVersion7();

    private PipelineStageDefinition Stage(StageKind kind) => _template.Stages.Single(s => s.Kind == kind);

    private JobApplication Started()
    {
        var application = JobApplication.Start(_candidateId, _positionId, _template.FirstStage, _hr, _clock);
        application.ClearDomainEvents();
        return application;
    }

    private JobApplication At(StageKind kind, StageTransitionContext? context = null)
    {
        var application = Started();
        foreach (var stage in _template.OrderedStages.Skip(1).Where(s => !s.IsOptional))
        {
            if (_template.FindStage(application.StageId)!.Kind == kind)
                break;
            _clock.Advance(TimeSpan.FromHours(1));
            application.MoveTo(_template, stage.Id, context ?? NoObstacles, _hr, _clock);
        }

        application.ClearDomainEvents();
        return application;
    }

    [Fact]
    public void DefaultTemplate_HasExpectedStagesWithOptionalCaseAndReferenceCheck()
    {
        _template.OrderedStages.Select(s => s.Kind).ShouldBe(
        [
            StageKind.Applied, StageKind.HrScreening, StageKind.CvReview, StageKind.TechnicalInterview,
            StageKind.CaseStudy, StageKind.FinalInterview, StageKind.ReferenceCheck, StageKind.Offer, StageKind.Hired,
        ]);
        _template.Stages.Where(s => s.IsOptional).Select(s => s.Kind)
            .ShouldBe([StageKind.CaseStudy, StageKind.ReferenceCheck], ignoreOrder: true);
        _template.FirstStage.Kind.ShouldBe(StageKind.Applied);
    }

    [Fact]
    public void Start_BeginsAtFirstStageAsActive()
    {
        var application = JobApplication.Start(_candidateId, _positionId, _template.FirstStage, _hr, _clock);

        application.Status.ShouldBe(ApplicationStatus.Active);
        application.StageId.ShouldBe(Stage(StageKind.Applied).Id);
        application.StageEnteredAt.ShouldBe(_clock.GetUtcNow());
        application.DomainEvents.ShouldHaveSingleItem()
            .ShouldBe(new ApplicationStarted(application.Id, _candidateId, _positionId, _hr, _clock.GetUtcNow()));
    }

    [Fact]
    public void MoveTo_NextStage_AdvancesAndRaisesEvent()
    {
        var application = Started();
        _clock.Advance(TimeSpan.FromDays(2));

        application.MoveTo(_template, Stage(StageKind.HrScreening).Id, NoObstacles, _hr, _clock);

        application.StageId.ShouldBe(Stage(StageKind.HrScreening).Id);
        application.StageEnteredAt.ShouldBe(_clock.GetUtcNow());
        application.DomainEvents.ShouldHaveSingleItem().ShouldBe(new ApplicationStageChanged(
            application.Id, _candidateId, Stage(StageKind.Applied).Id, Stage(StageKind.HrScreening).Id, _hr, _clock.GetUtcNow()));
    }

    [Fact]
    public void MoveTo_SkippingRequiredStage_Throws()
    {
        var error = Should.Throw<DomainException>(() =>
            Started().MoveTo(_template, Stage(StageKind.CvReview).Id, NoObstacles, _hr, _clock));

        error.Message.ShouldContain("İK ön görüşme");
    }

    [Fact]
    public void MoveTo_SkippingOptionalStage_IsAllowed()
    {
        var application = At(StageKind.TechnicalInterview);

        application.MoveTo(_template, Stage(StageKind.FinalInterview).Id, NoObstacles, _hr, _clock);

        application.StageId.ShouldBe(Stage(StageKind.FinalInterview).Id);
    }

    [Fact]
    public void MoveTo_IntoOptionalStage_IsAllowed()
    {
        var application = At(StageKind.TechnicalInterview);

        application.MoveTo(_template, Stage(StageKind.CaseStudy).Id, NoObstacles, _hr, _clock);

        application.StageId.ShouldBe(Stage(StageKind.CaseStudy).Id);
    }

    [Fact]
    public void MoveTo_BackwardsOrSameStage_Throws()
    {
        var application = At(StageKind.CvReview);

        Should.Throw<DomainException>(() =>
            application.MoveTo(_template, Stage(StageKind.Applied).Id, NoObstacles, _hr, _clock));
        Should.Throw<DomainException>(() =>
            application.MoveTo(_template, Stage(StageKind.CvReview).Id, NoObstacles, _hr, _clock));
    }

    [Fact]
    public void MoveTo_StageOfAnotherTemplate_Throws()
    {
        var foreignStage = PipelineTemplate.CreateDefault().FirstStage;

        Should.Throw<DomainException>(() =>
            Started().MoveTo(_template, foreignStage.Id, NoObstacles, _hr, _clock));
    }

    [Fact]
    public void MoveTo_OutOfCvReviewWithoutPositiveReview_Throws()
    {
        var application = At(StageKind.CvReview);

        Should.Throw<DomainException>(() => application.MoveTo(
            _template, Stage(StageKind.TechnicalInterview).Id, new StageTransitionContext(false, 0, false), _hr, _clock));
        application.StageId.ShouldBe(Stage(StageKind.CvReview).Id);
    }

    [Theory]
    [InlineData(StageKind.TechnicalInterview, StageKind.FinalInterview)]
    [InlineData(StageKind.FinalInterview, StageKind.Offer)]
    public void MoveTo_OutOfInterviewWithPendingScorecards_ThrowsUnlessBypassed(StageKind from, StageKind to)
    {
        var application = At(from);

        var error = Should.Throw<DomainException>(() => application.MoveTo(
            _template, Stage(to).Id, new StageTransitionContext(true, 2, false), _hr, _clock));
        error.Message.ShouldContain("2 mülakatçının");

        // İK Yöneticisinin izni gerekçe ister; gerekçe geçmişe ve event'e yazılır.
        Should.Throw<DomainException>(() => application.MoveTo(
            _template, Stage(to).Id, new StageTransitionContext(true, 2, true), _hr, _clock));
        application.MoveTo(_template, Stage(to).Id, new StageTransitionContext(true, 2, true), _hr, _clock, " Mülakatçı izinde ");
        application.StageId.ShouldBe(Stage(to).Id);
        application.History.Last().BypassReason.ShouldBe("Mülakatçı izinde");
        application.DomainEvents.OfType<ApplicationStageChanged>().Last().BypassReason.ShouldBe("Mülakatçı izinde");
    }

    [Fact]
    public void MoveTo_WithBypassWhenNothingIsPending_DoesNotRecordReason()
    {
        var application = At(StageKind.TechnicalInterview);

        application.MoveTo(
            _template, Stage(StageKind.FinalInterview).Id, new StageTransitionContext(true, 0, true), _hr, _clock, "Gerek yok");

        application.History.Last().BypassReason.ShouldBeNull();
    }

    private List<StageEdit> Edits(Func<PipelineStageDefinition, StageEdit>? change = null) =>
        [.. _template.OrderedStages.Select(s => change?.Invoke(s) ?? new StageEdit(s.Id, s.Name, s.IsOptional))];

    [Fact]
    public void EditStages_RenamesStagesAndTogglesOptionalOnesOnly()
    {
        var caseStudy = Stage(StageKind.CaseStudy);

        _template.EditStages(Edits(s => s.Kind switch
        {
            StageKind.Applied => new StageEdit(s.Id, "  Başvuru alındı ", false),
            StageKind.CaseStudy => new StageEdit(s.Id, s.Name, false),
            _ => new StageEdit(s.Id, s.Name, s.IsOptional),
        }));

        Stage(StageKind.Applied).Name.ShouldBe("Başvuru alındı");
        caseStudy.IsOptional.ShouldBeFalse();
        // Sıra ve türler sabit kalır.
        _template.OrderedStages.Select(s => s.Kind).ShouldBe(PipelineTemplate.CreateDefault().OrderedStages.Select(s => s.Kind));
    }

    [Theory]
    [InlineData(StageKind.CvReview)]
    [InlineData(StageKind.TechnicalInterview)]
    [InlineData(StageKind.Offer)]
    public void EditStages_RejectsMakingRuleStagesOptional(StageKind kind)
    {
        var edits = Edits(s => s.Kind == kind ? new StageEdit(s.Id, s.Name, true) : new StageEdit(s.Id, s.Name, s.IsOptional));

        Should.Throw<DomainException>(() => _template.EditStages(edits)).Message.ShouldContain("atlanabilir yapılamaz");
        Stage(kind).IsOptional.ShouldBeFalse();
    }

    [Fact]
    public void EditStages_RejectsDuplicateNamesBlankNamesAndMissingStages()
    {
        var first = _template.OrderedStages[0];
        var second = _template.OrderedStages[1];

        Should.Throw<DomainException>(() => _template.EditStages(
            Edits(s => s.Id == second.Id ? new StageEdit(s.Id, " BAŞVURU ", false) : new StageEdit(s.Id, s.Name, s.IsOptional))))
            .Message.ShouldBe("İki aşama aynı adı taşıyamaz.");
        Should.Throw<DomainException>(() => _template.EditStages(
            Edits(s => s.Id == first.Id ? new StageEdit(s.Id, " ", false) : new StageEdit(s.Id, s.Name, s.IsOptional))));
        Should.Throw<DomainException>(() => _template.EditStages(Edits().Skip(1).ToList()))
            .Message.ShouldBe("Aşamaların tümü gönderilmeli.");
    }

    [Fact]
    public void MoveTo_HiredStage_CompletesApplicationAndRaisesHired()
    {
        var application = At(StageKind.Offer);
        _clock.Advance(TimeSpan.FromDays(1));

        application.MoveTo(_template, Stage(StageKind.Hired).Id, NoObstacles, _hr, _clock);

        application.Status.ShouldBe(ApplicationStatus.Hired);
        application.ClosedAt.ShouldBe(_clock.GetUtcNow());
        application.DomainEvents.OfType<ApplicationHired>().ShouldHaveSingleItem()
            .ShouldBe(new ApplicationHired(application.Id, _positionId));
    }

    [Theory]
    [InlineData(ApplicationStatus.Rejected)]
    [InlineData(ApplicationStatus.Withdrawn)]
    public void Close_FromAnyStage_EndsApplicationWithReason(ApplicationStatus status)
    {
        var application = At(StageKind.TechnicalInterview);
        var reasonId = Guid.CreateVersion7();

        application.Close(status, reasonId, " Teknik mülakatta yetersiz ", _hr, _clock);

        application.Status.ShouldBe(status);
        application.RejectionReasonId.ShouldBe(reasonId);
        application.ClosingNote.ShouldBe("Teknik mülakatta yetersiz");
        application.StageId.ShouldBe(Stage(StageKind.TechnicalInterview).Id);
        application.DomainEvents.ShouldHaveSingleItem()
            .ShouldBe(new ApplicationClosed(application.Id, _candidateId, status, _hr, _clock.GetUtcNow()));
    }

    [Fact]
    public void Close_WithoutReasonOrWithInvalidStatus_Throws()
    {
        Should.Throw<DomainException>(() => Started().Close(ApplicationStatus.Rejected, Guid.Empty, null, _hr, _clock));
        Should.Throw<DomainException>(() =>
            Started().Close(ApplicationStatus.Hired, Guid.CreateVersion7(), null, _hr, _clock));
    }

    [Fact]
    public void ClosedApplication_CannotBeMovedOrClosedAgain()
    {
        var application = Started();
        application.Close(ApplicationStatus.Rejected, Guid.CreateVersion7(), null, _hr, _clock);

        Should.Throw<DomainException>(() =>
            application.MoveTo(_template, Stage(StageKind.HrScreening).Id, NoObstacles, _hr, _clock));
        Should.Throw<DomainException>(() =>
            application.Close(ApplicationStatus.Withdrawn, Guid.CreateVersion7(), null, _hr, _clock));
    }

    [Fact]
    public void Undo_WithinWindow_RestoresPreviousStageAndItsEntryTime()
    {
        var application = Started();
        var enteredApplied = application.StageEnteredAt;
        _clock.Advance(TimeSpan.FromDays(3));
        application.MoveTo(_template, Stage(StageKind.HrScreening).Id, NoObstacles, _hr, _clock);
        application.ClearDomainEvents();
        _clock.Advance(TimeSpan.FromMinutes(9));

        application.CanUndo(_clock).ShouldBeTrue();
        application.UndoLastChange(_hr, _clock);

        application.StageId.ShouldBe(Stage(StageKind.Applied).Id);
        application.StageEnteredAt.ShouldBe(enteredApplied);
        application.CanUndo(_clock).ShouldBeFalse();
        application.DomainEvents.ShouldHaveSingleItem().ShouldBeOfType<ApplicationChangeUndone>();
    }

    [Fact]
    public void Undo_AfterWindow_Throws()
    {
        var application = Started();
        application.MoveTo(_template, Stage(StageKind.HrScreening).Id, NoObstacles, _hr, _clock);
        _clock.Advance(TimeSpan.FromMinutes(10) + TimeSpan.FromSeconds(1));

        application.CanUndo(_clock).ShouldBeFalse();
        Should.Throw<DomainException>(() => application.UndoLastChange(_hr, _clock)).Message.ShouldContain("10 dakika");
    }

    [Fact]
    public void Undo_OfRejection_ReopensApplication()
    {
        var application = At(StageKind.CvReview);
        application.Close(ApplicationStatus.Rejected, Guid.CreateVersion7(), "Not", _hr, _clock);

        application.UndoLastChange(_hr, _clock);

        application.Status.ShouldBe(ApplicationStatus.Active);
        application.RejectionReasonId.ShouldBeNull();
        application.ClosingNote.ShouldBeNull();
        application.ClosedAt.ShouldBeNull();
        application.StageId.ShouldBe(Stage(StageKind.CvReview).Id);
    }

    [Fact]
    public void Undo_OfHire_ReopensApplicationAndRaisesHireReverted()
    {
        var application = At(StageKind.Offer);
        application.MoveTo(_template, Stage(StageKind.Hired).Id, NoObstacles, _hr, _clock);
        application.ClearDomainEvents();

        application.UndoLastChange(_hr, _clock);

        application.Status.ShouldBe(ApplicationStatus.Active);
        application.StageId.ShouldBe(Stage(StageKind.Offer).Id);
        application.DomainEvents.OfType<ApplicationHireReverted>().ShouldHaveSingleItem();
    }

    [Fact]
    public void Undo_Twice_OrWithoutAnyChange_Throws()
    {
        Should.Throw<DomainException>(() => Started().UndoLastChange(_hr, _clock));

        var application = Started();
        application.MoveTo(_template, Stage(StageKind.HrScreening).Id, NoObstacles, _hr, _clock);
        application.UndoLastChange(_hr, _clock);

        Should.Throw<DomainException>(() => application.UndoLastChange(_hr, _clock));
    }
}
