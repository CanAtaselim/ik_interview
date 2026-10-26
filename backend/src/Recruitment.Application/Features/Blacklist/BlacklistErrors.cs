using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Blacklist;

public static class BlacklistErrors
{
    public static readonly Error NotFound = Error.NotFound("Blacklist.NotFound", "Kara liste kaydı bulunamadı.");

    public static readonly Error AlreadyOpen = Error.Conflict(
        "Blacklist.AlreadyOpen", "Bu aday için onay bekleyen veya yürürlükte bir kara liste kaydı zaten var.");
}
