using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Positions;

public static class PositionErrors
{
    public static readonly Error NotFound = Error.NotFound("Position.NotFound", "Pozisyon bulunamadı.");

    public static readonly Error InvalidTitle = Error.Validation(
        new Dictionary<string, string[]> { ["title"] = ["Listeden geçerli bir pozisyon adı seçmelisiniz."] });
}
