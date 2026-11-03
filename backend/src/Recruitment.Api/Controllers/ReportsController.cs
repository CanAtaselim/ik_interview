using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Recruitment.Api.Auth;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Application.Features.Dashboard;
using Recruitment.Application.Features.Reports;

namespace Recruitment.Api.Controllers;

/// <summary>
/// PDF ve Excel çıktıları: aday dosyası, liste raporları ve dashboard verisi. Çıktı kişisel veri içerir;
/// her dışa aktarma erişim kaydına yazılır. Dashboard raporu, dashboard gibi yalnızca İK Yöneticisine açıktır.
/// </summary>
[ApiController]
[Route("api/v1/reports")]
[Authorize(Policy = Policies.HrOnly)]
public sealed class ReportsController(IDispatcher dispatcher) : ControllerBase
{
    private static readonly Dictionary<string, ReportKind> Kinds = new(StringComparer.OrdinalIgnoreCase)
    {
        ["candidates"] = ReportKind.Candidates,
        ["applications"] = ReportKind.Applications,
        ["hiring-requests"] = ReportKind.HiringRequests,
        ["positions"] = ReportKind.Positions,
        ["interviews"] = ReportKind.Interviews,
        ["cv-reviews"] = ReportKind.CvReviews,
        ["blacklist"] = ReportKind.Blacklist,
    };

    /// <param name="format"><c>pdf</c> ya da <c>xlsx</c>.</param>
    [HttpGet("candidates/{id:guid}/dossier")]
    public async Task<IActionResult> CandidateDossier(Guid id, string format, CancellationToken ct)
    {
        if (!TryParseFormat(format, out var parsed))
            return InvalidFormat();

        return ToFile(await dispatcher.Query(new GetCandidateDossierQuery(id, parsed), ct));
    }

    [HttpGet("lists/{kind}")]
    public async Task<IActionResult> List(
        string kind,
        DateOnly? from,
        DateOnly? to,
        Guid? departmentId,
        Guid? positionId,
        string? search,
        string? status,
        Guid? sourceId,
        bool? inTalentPool,
        string? tag,
        string? process,
        bool? blacklisted,
        string? city,
        Guid? interviewerUserId,
        Guid? roomId,
        CancellationToken ct)
    {
        if (!Kinds.TryGetValue(kind, out var parsed))
            return Error.NotFound("Report.UnknownKind", "Bilinmeyen rapor.").ToProblemResult(this);

        var filter = new ReportFilter(from, to, departmentId, positionId)
        {
            Search = search,
            Status = status,
            SourceId = sourceId,
            InTalentPool = inTalentPool,
            Tag = tag,
            Process = process,
            Blacklisted = blacklisted,
            City = city,
            InterviewerUserId = interviewerUserId,
            RoomId = roomId,
        };
        return ToFile(await dispatcher.Query(new ExportListReportQuery(parsed, filter), ct));
    }

    [HttpGet("dashboard")]
    [Authorize(Policy = Policies.HrManagerOnly)]
    public async Task<IActionResult> Dashboard(
        DateOnly? from, DateOnly? to, Guid? departmentId, Guid? positionId, CancellationToken ct) =>
        ToFile(await dispatcher.Query(new ExportDashboardQuery(new DashboardFilter(from, to, departmentId, positionId)), ct));

    private IActionResult ToFile(Result<ReportFile> result)
    {
        if (result.IsFailure)
            return result.Error.ToProblemResult(this);

        // Tarayıcı ve ara katmanlar kişisel veri içeren çıktıyı saklamasın.
        Response.Headers.CacheControl = "no-store";
        return File(result.Value.Content, result.Value.ContentType, result.Value.FileName);
    }

    private static bool TryParseFormat(string? format, out ReportFormat parsed)
    {
        parsed = format?.ToLowerInvariant() switch
        {
            "pdf" => ReportFormat.Pdf,
            "xlsx" => ReportFormat.Xlsx,
            _ => (ReportFormat)(-1),
        };
        return Enum.IsDefined(parsed);
    }

    private IActionResult InvalidFormat() =>
        Error.Validation(new Dictionary<string, string[]> { ["format"] = ["Biçim pdf ya da xlsx olmalıdır."] }).ToProblemResult(this);
}
