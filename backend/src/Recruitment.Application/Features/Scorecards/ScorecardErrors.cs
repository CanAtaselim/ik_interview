using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Scorecards;

public static class ScorecardErrors
{
    public static readonly Error NotFound = Error.NotFound("Scorecard.NotFound", "Değerlendirme bulunamadı.");

    public static readonly Error NotInterviewer = Error.Forbidden(
        "Scorecard.NotInterviewer", "Bu mülakatı yalnızca atanmış mülakatçılar değerlendirebilir.");

    public static readonly Error NotOwner = Error.Forbidden(
        "Scorecard.NotOwner", "Yalnızca kendi değerlendirmenizi düzenleyebilirsiniz.");

    public static readonly Error InterviewClosed = Error.BusinessRule(
        "Scorecard.InterviewClosed", "İptal edilen veya adayın gelmediği mülakat değerlendirilemez.");

    public static readonly Error NotStartedYet = Error.BusinessRule(
        "Scorecard.NotStartedYet", "Değerlendirmeyi mülakat başladıktan sonra gönderebilirsiniz.");
}
