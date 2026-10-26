using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Pipeline;

public static class PipelineErrors
{
    public static readonly Error NotFound = Error.NotFound("Application.NotFound", "Süreç bulunamadı.");

    public static readonly Error PositionNotOpen = Error.BusinessRule(
        "Application.PositionNotOpen", "Yalnızca açık pozisyona aday eklenebilir.");

    public static readonly Error AlreadyInProcess = Error.Conflict(
        "Application.AlreadyInProcess", "Adayın bu pozisyonda devam eden bir süreci zaten var.");

    public static readonly Error BypassNotAllowed = Error.Forbidden(
        "Application.BypassNotAllowed", "Değerlendirmeler beklenmeden geçişe yalnızca İK Yöneticisi izin verebilir.");

    /// <summary>Mülakat değerlendirmeleri gönderilmeden aşamadan çıkılamaz; İK Yöneticisi gerekçeyle geçirebilir.</summary>
    public static Error ScorecardsPending(int pendingCount, bool canBypass) => new(
        "Application.ScorecardsPending",
        $"{pendingCount} mülakatçının değerlendirmesi bekleniyor. Değerlendirmeler gönderilmeden aday sonraki aşamaya geçemez.",
        ErrorType.BusinessRule,
        Extensions: new Dictionary<string, object?> { ["pendingCount"] = pendingCount, ["canBypass"] = canBypass });

    public static readonly Error TemplateMissing = Error.BusinessRule(
        "Application.TemplateMissing", "Süreç aşamaları tanımlı değil. Sistem yöneticinize haber verin.");
}
