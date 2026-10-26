namespace Recruitment.Domain.Scorecards;

/// <summary>Puanlanmış bir madde: puan ve ağırlığı.</summary>
public readonly record struct WeightedScore(int Score, decimal Weight);

/// <summary>Strategy pattern: değerlendirme puanının nasıl hesaplanacağı.</summary>
public interface IScoreCalculator
{
    /// <returns>Puanlanmış madde yoksa <c>null</c>.</returns>
    decimal? Calculate(IEnumerable<WeightedScore> scores);
}

/// <summary>Varsayılan hesap: Σ(puan × ağırlık) / Σ(ağırlık); puansız maddeler hesaba girmez.</summary>
public sealed class WeightedAverageCalculator : IScoreCalculator
{
    public decimal? Calculate(IEnumerable<WeightedScore> scores)
    {
        var list = scores.Where(s => s.Weight > 0).ToList();
        if (list.Count == 0)
            return null;

        var total = list.Sum(s => s.Score * s.Weight);
        return Math.Round(total / list.Sum(s => s.Weight), 2, MidpointRounding.AwayFromZero);
    }
}
