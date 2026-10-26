namespace Recruitment.Domain.Scorecards;

public enum ScorecardStatus
{
    Draft,
    Submitted,
}

/// <summary>Mülakatçının adayla ilgili genel önerisi.</summary>
public enum Recommendation
{
    StrongHire,
    Hire,
    Undecided,
    NoHire,
    StrongNoHire,
}

/// <summary>Tüm değerlendirmeler gönderildikten sonra İK'nın kaydettiği ortak karar.</summary>
public enum DebriefDecision
{
    /// <summary>Aday sonraki aşamaya geçsin.</summary>
    Proceed,

    /// <summary>Aday bekletilsin (ör. diğer adaylar görülsün).</summary>
    Hold,

    /// <summary>Süreç olumsuz sonuçlansın.</summary>
    Reject,
}

/// <summary>Sabit puanlama rubriği; arayüz her puanın yanında bu anlamı gösterir.</summary>
public static class ScoreRubric
{
    public const int Min = 1;
    public const int Max = 5;

    public static readonly IReadOnlyDictionary<int, string> Labels = new Dictionary<int, string>
    {
        [1] = "Yetersiz",
        [2] = "Beklentinin altında",
        [3] = "Beklentiyi karşılıyor",
        [4] = "Beklentinin üstünde",
        [5] = "Mükemmel",
    };
}
