using Recruitment.Application.Common;

namespace Recruitment.Application.Features.HiringRequests;

public static class HiringRequestErrors
{
    public static readonly Error NotFound = Error.NotFound("HiringRequest.NotFound", "Kadro talebi bulunamadı.");

    public static readonly Error NotOwner = Error.Forbidden(
        "HiringRequest.NotOwner", "Bu işlemi yalnızca talebi açan kişi yapabilir.");

    public static readonly Error CannotDecide = Error.Forbidden(
        "HiringRequest.CannotDecide", "Bu talep şu anda sizin onayınızı beklemiyor.");

    public static readonly Error Incomplete = Error.BusinessRule(
        "HiringRequest.Incomplete",
        "Talebi göndermeden önce tüm alanları doldurmalısınız. Talebi düzenleyip kaydedin.");

    public static readonly Error CannotCancel = Error.Forbidden(
        "HiringRequest.CannotCancel", "Talebi yalnızca açan kişi veya İK yöneticisi iptal edebilir.");
}
