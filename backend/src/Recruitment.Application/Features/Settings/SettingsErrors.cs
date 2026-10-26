using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Settings;

public static class SettingsErrors
{
    public static readonly Error EmailTemplateNotFound =
        Error.NotFound("EmailTemplate.NotFound", "E-posta şablonu bulunamadı.");

    public static readonly Error ApprovalFlowMissing = Error.NotFound(
        "ApprovalFlow.NotFound", "Onay akışı tanımlı değil. Sistem yöneticinize haber verin.");

    public static readonly Error PipelineMissing = Error.NotFound(
        "Pipeline.NotFound", "Süreç aşamaları tanımlı değil. Sistem yöneticinize haber verin.");

    public static readonly Error RoleMappingNotFound =
        Error.NotFound("RoleMapping.NotFound", "Rol eşlemesi bulunamadı.");

    public static readonly Error RoleMappingDuplicate = Error.Conflict(
        "RoleMapping.Duplicate", "Bu AD grubu zaten bu role eşlenmiş.");

    public static readonly Error LastAdminMapping = Error.BusinessRule(
        "RoleMapping.LastAdminMapping",
        "Sistem Admin rolüne eşlenmiş son grup silinemez; silinirse ayarlara kimse erişemez. Önce başka bir grubu Sistem Admin rolüne eşleyin.");

    public static readonly Error HolidayNotFound = Error.NotFound("Holiday.NotFound", "Resmi tatil bulunamadı.");

    public static readonly Error HolidayDuplicate = Error.Conflict(
        "Holiday.Duplicate", "Bu tarih zaten resmi tatil olarak tanımlı.");

    public static Error Field(string field, string message) =>
        Error.Validation(new Dictionary<string, string[]> { [field] = [message] });
}
