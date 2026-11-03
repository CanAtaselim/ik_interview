using System.Net;
using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Recruitment.Api.IntegrationTests.Infrastructure;
using Recruitment.Domain.Audit;
using Recruitment.Domain.Users;
using Recruitment.Infrastructure.Persistence;
using Shouldly;
using UglyToad.PdfPig;

namespace Recruitment.Api.IntegrationTests.Reports;

public sealed class ReportsEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Department = "Yazılım";

    private static readonly string[] ListKinds =
        ["candidates", "applications", "hiring-requests", "positions", "interviews", "cv-reviews", "blacklist"];

    private Task<ApiUser> Manager() => factory.LoginAs(Department, Role.HrManager);

    /// <summary>Türkçe karakterli bir adayı, bir pozisyona başvurmuş halde oluşturur.</summary>
    private static async Task<(Guid CandidateId, string LastName)> SeedCandidate(ApiUser hr)
    {
        var positionId = await hr.Create("/api/v1/positions", new
        {
            title = "Rapor Geliştirici",
            departmentId = await hr.DictionaryItemId("Department", Department),
            levelId = await hr.DictionaryItemId("Level", "L3"),
            headcount = 1,
        });
        var lastName = $"Çağlayan{Guid.NewGuid():N}";
        var candidateId = await hr.Create("/api/v1/candidates", new
        {
            firstName = "Şükrü",
            lastName,
            email = $"{Guid.NewGuid():N}@ornek.com",
            tags = new[] { "C#", "Docker" },
        });
        await hr.Create("/api/v1/applications", new { candidateId, positionId });
        return (candidateId, lastName);
    }

    /// <summary>PdfPig kelime boşluklarını güvenilir vermez; karşılaştırma boşluksuz yapılır.</summary>
    private static string AllText(PdfDocument pdf) => NoSpaces(string.Concat(pdf.GetPages().Select(page => page.Text)));

    private static string NoSpaces(string text) => string.Concat(text.Where(c => !char.IsWhiteSpace(c)));

    [Fact]
    public async Task Dossier_Xlsx_HasOneSheetPerSectionWithTheCandidateData()
    {
        var hr = await Manager();
        var (candidateId, lastName) = await SeedCandidate(hr);

        var response = await hr.Client.GetAsync($"/api/v1/reports/candidates/{candidateId}/dossier?format=xlsx");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        response.Content.Headers.ContentDisposition!.FileNameStar.ShouldEndWith(".xlsx");
        response.Headers.CacheControl!.NoStore.ShouldBeTrue();

        using var workbook = new XLWorkbook(await response.Content.ReadAsStreamAsync());
        var sheets = workbook.Worksheets.Select(s => s.Name).ToList();
        sheets.ShouldBe(
        [
            "Aday bilgileri", "Belgeler", "Süreçler (başvurular)", "Aşama geçmişi", "CV değerlendirmeleri", "Mülakatlar",
            "Değerlendirme formları (özet)", "Değerlendirme formları (soru ba", "Kara liste kayıtları", "Gönderilen e-postalar",
            "Zaman çizelgesi",
        ]);

        var profile = workbook.Worksheet("Aday bilgileri").CellsUsed().Select(c => c.GetFormattedString()).ToList();
        profile.ShouldContain($"Şükrü {lastName}");
        profile.ShouldContain("C#, Docker");

        var applications = workbook.Worksheet("Süreçler (başvurular)").CellsUsed().Select(c => c.GetFormattedString()).ToList();
        applications.ShouldContain("Rapor Geliştirici");
        applications.ShouldContain("Süreçte");
    }

    [Fact]
    public async Task Dossier_Pdf_RendersTurkishCharactersAndSections()
    {
        var hr = await Manager();
        var (candidateId, lastName) = await SeedCandidate(hr);

        var response = await hr.Client.GetAsync($"/api/v1/reports/candidates/{candidateId}/dossier?format=pdf");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        var bytes = await response.Content.ReadAsByteArrayAsync();
        System.Text.Encoding.ASCII.GetString(bytes, 0, 5).ShouldBe("%PDF-");

        using var pdf = PdfDocument.Open(bytes);
        var text = AllText(pdf);
        text.ShouldContain(NoSpaces("Aday dosyası"));
        text.ShouldContain(NoSpaces($"Şükrü {lastName}"));
        text.ShouldContain(NoSpaces("Süreçler (başvurular)"));
        text.ShouldContain(NoSpaces("Rapor Geliştirici"));
        text.ShouldContain("Gizli");
    }

    [Fact]
    public async Task Dossier_IsRecordedInTheAccessLog()
    {
        var hr = await Manager();
        var (candidateId, _) = await SeedCandidate(hr);

        (await hr.Client.GetAsync($"/api/v1/reports/candidates/{candidateId}/dossier?format=pdf")).EnsureSuccessStatusCode();

        await using var scope = factory.Services.CreateAsyncScope();
        var log = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AccessLogs
            .SingleAsync(a => a.CandidateId == candidateId && a.Action == AccessAction.ReportExported);
        log.UserId.ShouldBe(hr.Id);
        log.Detail.ShouldBe("Aday dosyası (PDF)");
    }

    [Fact]
    public async Task EveryListReport_ReturnsAnExcelFile_AndAcceptsFilters()
    {
        var hr = await Manager();
        var (_, lastName) = await SeedCandidate(hr);
        var departmentId = await hr.DictionaryItemId("Department", Department);

        foreach (var kind in ListKinds)
        {
            var response = await hr.Client.GetAsync($"/api/v1/reports/lists/{kind}");
            response.StatusCode.ShouldBe(HttpStatusCode.OK, kind);
            using var workbook = new XLWorkbook(await response.Content.ReadAsStreamAsync());
            workbook.Worksheets.Count.ShouldBe(1, kind);

            var filtered = await hr.Client.GetAsync(
                $"/api/v1/reports/lists/{kind}?from=2020-01-01&to=2099-12-31&departmentId={departmentId}");
            filtered.StatusCode.ShouldBe(HttpStatusCode.OK, kind);
        }

        var candidates = await hr.Client.GetAsync("/api/v1/reports/lists/candidates");
        using var sheet = new XLWorkbook(await candidates.Content.ReadAsStreamAsync());
        sheet.Worksheet(1).CellsUsed().Select(c => c.GetFormattedString()).ShouldContain(lastName);

        var future = await hr.Client.GetAsync("/api/v1/reports/lists/candidates?from=2099-01-01");
        using var empty = new XLWorkbook(await future.Content.ReadAsStreamAsync());
        empty.Worksheet(1).CellsUsed().Select(c => c.GetFormattedString()).ShouldNotContain(lastName);
    }

    private static async Task<List<string>> Cells(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var workbook = new XLWorkbook(await response.Content.ReadAsStreamAsync());
        return workbook.Worksheet(1).CellsUsed().Select(c => c.GetFormattedString()).ToList();
    }

    [Fact]
    public async Task ListReports_ApplyTheSameFiltersAsTheirScreens()
    {
        var hr = await Manager();
        var (_, lastName) = await SeedCandidate(hr);
        var (_, otherLastName) = await SeedCandidate(hr);

        var bySearch = await Cells(await hr.Client.GetAsync($"/api/v1/reports/lists/candidates?search={Uri.EscapeDataString(lastName)}"));
        bySearch.ShouldContain(lastName);
        bySearch.ShouldNotContain(otherLastName);

        var byTag = await Cells(await hr.Client.GetAsync("/api/v1/reports/lists/candidates?tag=Docker&process=Active"));
        byTag.ShouldContain(lastName);
        var noProcess = await Cells(await hr.Client.GetAsync("/api/v1/reports/lists/candidates?process=None"));
        noProcess.ShouldNotContain(lastName);

        var withdrawn = await Cells(await hr.Client.GetAsync("/api/v1/reports/lists/applications?status=Withdrawn"));
        withdrawn.ShouldNotContain($"Şükrü {lastName}");
        var active = await Cells(await hr.Client.GetAsync($"/api/v1/reports/lists/applications?status=Active&search={Uri.EscapeDataString(lastName)}"));
        active.ShouldContain($"Şükrü {lastName}");
        active.ShouldNotContain($"Şükrü {otherLastName}");

        (await Cells(await hr.Client.GetAsync("/api/v1/reports/lists/positions?status=Closed&search=Rapor"))).ShouldNotContain("Rapor Geliştirici");
        (await Cells(await hr.Client.GetAsync("/api/v1/reports/lists/positions?search=Rapor"))).ShouldContain("Rapor Geliştirici");
        (await Cells(await hr.Client.GetAsync("/api/v1/reports/lists/cv-reviews?status=Overdue"))).ShouldNotContain(lastName);
    }

    [Fact]
    public async Task ListReports_RejectUnknownStatusValues()
    {
        var hr = await Manager();
        (await hr.Client.GetAsync("/api/v1/reports/lists/applications?status=Bilinmeyen")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await hr.Client.GetAsync("/api/v1/reports/lists/candidates?process=Bilinmeyen")).StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        // Durumu olmayan raporda status yok sayılır.
        (await hr.Client.GetAsync("/api/v1/reports/lists/candidates?status=Bilinmeyen")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task DashboardReport_IsExcel_ForManagerOnly()
    {
        var manager = await Manager();
        await SeedCandidate(manager);

        var response = await manager.Client.GetAsync("/api/v1/reports/dashboard");
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        using var workbook = new XLWorkbook(await response.Content.ReadAsStreamAsync());
        workbook.Worksheets.Select(s => s.Name).ShouldContain("Özet");
        workbook.Worksheets.Select(s => s.Name).ShouldContain("Süreç - Huni");
        workbook.Worksheets.Select(s => s.Name).Distinct().Count().ShouldBe(workbook.Worksheets.Count);

        var specialist = await factory.LoginAs(Department, Role.HrSpecialist);
        (await specialist.Client.GetAsync("/api/v1/reports/dashboard")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reports_AreForbiddenForNonHrRoles_AndSpecialistCanExportDossier()
    {
        var manager = await Manager();
        var (candidateId, _) = await SeedCandidate(manager);

        foreach (var role in new[] { Role.TeamLead, Role.DepartmentManager, Role.Executive })
        {
            var user = await factory.LoginAs(Department, role);
            (await user.Client.GetAsync($"/api/v1/reports/candidates/{candidateId}/dossier?format=pdf"))
                .StatusCode.ShouldBe(HttpStatusCode.Forbidden, role.ToString());
            (await user.Client.GetAsync("/api/v1/reports/lists/candidates")).StatusCode.ShouldBe(HttpStatusCode.Forbidden, role.ToString());
        }

        var specialist = await factory.LoginAs(Department, Role.HrSpecialist);
        (await specialist.Client.GetAsync($"/api/v1/reports/candidates/{candidateId}/dossier?format=pdf"))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task InvalidRequests_AreRejected()
    {
        var hr = await Manager();
        var (candidateId, _) = await SeedCandidate(hr);

        (await hr.Client.GetAsync($"/api/v1/reports/candidates/{candidateId}/dossier?format=docx"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await hr.Client.GetAsync($"/api/v1/reports/candidates/{Guid.NewGuid()}/dossier?format=pdf"))
            .StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await hr.Client.GetAsync("/api/v1/reports/lists/bilinmeyen")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await hr.Client.GetAsync("/api/v1/reports/lists/candidates?from=2026-12-31&to=2026-01-01"))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
