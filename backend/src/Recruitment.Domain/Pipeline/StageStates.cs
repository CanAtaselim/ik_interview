namespace Recruitment.Domain.Pipeline;

/// <summary>Geçiş kararı için aşamanın dışından gelen bilgiler (CV kararları, mülakat değerlendirmeleri).</summary>
/// <param name="HasPositiveCvReview">Adayın bu süreçte en az bir olumlu CV değerlendirmesi var mı?</param>
/// <param name="PendingScorecardCount">Bu aşamadaki mülakatlar için henüz gönderilmemiş değerlendirme sayısı.</param>
/// <param name="BypassedByHrManager">İK Yöneticisi eksik değerlendirmelere rağmen gerekçeyle geçişe izin verdi.</param>
public sealed record StageTransitionContext(
    bool HasPositiveCvReview, int PendingScorecardCount, bool BypassedByHrManager);

/// <summary>State pattern: her aşama, kendisinden çıkış koşulunu bilir.</summary>
public interface IStageState
{
    TransitionCheck CanMoveTo(PipelineStageDefinition next, StageTransitionContext context);
}

public static class StageStates
{
    private static readonly IStageState Open = new OpenStage();
    private static readonly IStageState CvReview = new CvReviewStage();
    private static readonly IStageState Interview = new InterviewStage();

    public static IStageState For(StageKind kind) => kind switch
    {
        StageKind.CvReview => CvReview,
        StageKind.TechnicalInterview or StageKind.FinalInterview => Interview,
        _ => Open,
    };

    /// <summary>Çıkış koşulu olmayan aşama.</summary>
    private sealed class OpenStage : IStageState
    {
        public TransitionCheck CanMoveTo(PipelineStageDefinition next, StageTransitionContext context) =>
            TransitionCheck.Allowed;
    }

    private sealed class CvReviewStage : IStageState
    {
        public TransitionCheck CanMoveTo(PipelineStageDefinition next, StageTransitionContext context) =>
            context.HasPositiveCvReview
                ? TransitionCheck.Allowed
                : TransitionCheck.Denied("Aday bu aşamadan çıkabilmek için en az bir olumlu CV değerlendirmesi almalı.");
    }

    private sealed class InterviewStage : IStageState
    {
        public TransitionCheck CanMoveTo(PipelineStageDefinition next, StageTransitionContext context) =>
            context.PendingScorecardCount == 0 || context.BypassedByHrManager
                ? TransitionCheck.Allowed
                : TransitionCheck.Denied(
                    $"{context.PendingScorecardCount} mülakatçının değerlendirmesi bekleniyor. " +
                    "Değerlendirmeler gönderilmeden aday sonraki aşamaya geçemez.");
    }
}
