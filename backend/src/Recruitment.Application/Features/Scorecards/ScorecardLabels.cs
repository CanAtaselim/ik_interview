using Recruitment.Domain.Scorecards;

namespace Recruitment.Application.Features.Scorecards;

/// <summary>Zaman çizelgesi gibi sunucuda yazılan metinler için Türkçe etiketler.</summary>
internal static class ScorecardLabels
{
    public static string Label(this Recommendation recommendation) => recommendation switch
    {
        Recommendation.StrongHire => "Kesinlikle alınsın",
        Recommendation.Hire => "Alınsın",
        Recommendation.Undecided => "Kararsız",
        Recommendation.NoHire => "Alınmasın",
        _ => "Kesinlikle alınmasın",
    };

    public static string Label(this DebriefDecision decision) => decision switch
    {
        DebriefDecision.Proceed => "Sonraki aşamaya geçsin",
        DebriefDecision.Hold => "Beklesin",
        _ => "Olumsuz",
    };
}
