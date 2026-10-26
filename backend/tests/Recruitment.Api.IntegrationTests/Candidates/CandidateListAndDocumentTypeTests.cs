using System.Net;
using System.Text.Json;
using Recruitment.Api.IntegrationTests.Infrastructure;
using Recruitment.Domain.Users;
using Shouldly;

namespace Recruitment.Api.IntegrationTests.Candidates;

/// <summary>Aday listesi (sıralama, süreç, kara liste, CV) ve dosya türleri.</summary>
public sealed class CandidateListAndDocumentTypeTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/v1/candidates";

    private static string Marker() => "m" + Guid.NewGuid().ToString("N")[..10];

    private Task<ApiUser> Hr() => factory.LoginAs(Role.HrSpecialist);

    private static Task<Guid> CreateCandidate(ApiUser hr, string marker, string firstName = "Deniz") =>
        hr.Create(Url, new
        {
            firstName,
            lastName = $"Aday{Guid.NewGuid():N}",
            email = $"{Guid.NewGuid():N}@ornek.com",
            tags = new[] { marker },
        });

    private static async Task<Guid> Upload(ApiUser hr, Guid candidateId, byte[] content, string fileName, Guid? typeId = null)
    {
        var form = TestFiles.AsUpload(content, fileName);
        if (typeId is { } id)
            form.Add(new StringContent(id.ToString()), "typeId");

        var response = await hr.Client.PostAsync($"{Url}/{candidateId}/documents", form);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Json()).GetProperty("id").GetGuid();
    }

    private static async Task<JsonElement> ListedItem(ApiUser hr, string marker, Guid candidateId) =>
        (await hr.GetJson($"{Url}?tag={marker}")).GetProperty("items").EnumerateArray()
        .Single(i => i.GetProperty("id").GetGuid() == candidateId);

    [Fact]
    public async Task DocumentTypes_AreSeededAndManagedFromTheAdminDictionary()
    {
        var manager = await factory.LoginAs(Role.HrManager);

        var names = (await manager.GetJson("/api/v1/dictionaries/DocumentType/items")).EnumerateArray()
            .Select(i => i.GetProperty("name").GetString()).ToList();
        var created = await manager.Post("/api/v1/dictionaries/DocumentType/items", new { name = $"Referans mektubu {Marker()}" });

        names.ShouldBe(["CV", "Sertifika", "Case çalışması", "Diğer"], ignoreOrder: true);
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Upload_WithType_ShowsTypeOnCard_AndWithoutTypeCountsAsCv()
    {
        var hr = await Hr();
        var marker = Marker();
        var id = await CreateCandidate(hr, marker);
        var certificate = await hr.DictionaryItemId("DocumentType", "Sertifika");

        await Upload(hr, id, TestFiles.Pdf("Sertifika"), "aws.pdf", certificate);
        await Upload(hr, id, TestFiles.Pdf("Eski"), "eski.pdf");

        var documents = (await hr.GetJson($"{Url}/{id}")).GetProperty("documents").EnumerateArray().ToList();
        documents.Single(d => d.GetProperty("fileName").GetString() == "aws.pdf").GetProperty("typeName").GetString()
            .ShouldBe("Sertifika");
        documents.Single(d => d.GetProperty("fileName").GetString() == "eski.pdf").GetProperty("typeName").GetString()
            .ShouldBe("CV");
    }

    [Fact]
    public async Task Upload_WithUnknownOrWrongKindOfType_IsRejected()
    {
        var hr = await Hr();
        var id = await CreateCandidate(hr, Marker());
        var wrongKind = await hr.DictionaryItemId("Level", "L3");

        foreach (var typeId in new[] { Guid.NewGuid(), wrongKind })
        {
            var form = TestFiles.AsUpload(TestFiles.Pdf("x"), "cv.pdf");
            form.Add(new StringContent(typeId.ToString()), "typeId");

            var response = await hr.Client.PostAsync($"{Url}/{id}/documents", form);

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await response.Json()).GetProperty("errors").TryGetProperty("typeId", out _).ShouldBeTrue();
        }
    }

    [Fact]
    public async Task Upload_AcceptsImagesForScannedCertificates()
    {
        var hr = await Hr();
        var id = await CreateCandidate(hr, Marker());
        byte[] png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00];

        await Upload(hr, id, png, "sertifika.png");
        var fake = await hr.Client.PostAsync($"{Url}/{id}/documents", TestFiles.AsUpload("PNG degil"u8.ToArray(), "sahte.png"));

        (await hr.GetJson($"{Url}/{id}")).GetProperty("documents")[0].GetProperty("contentType").GetString()
            .ShouldBe("image/png");
        fake.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_OpensTheLatestCvOnly_NotCertificatesOrCases()
    {
        var hr = await Hr();
        var marker = Marker();
        var onlyCertificate = await CreateCandidate(hr, marker);
        var withCv = await CreateCandidate(hr, marker);
        var noDocuments = await CreateCandidate(hr, marker);
        var certificate = await hr.DictionaryItemId("DocumentType", "Sertifika");
        var cv = await hr.DictionaryItemId("DocumentType", "CV");

        await Upload(hr, onlyCertificate, TestFiles.Pdf("S"), "sertifika.pdf", certificate);
        await Upload(hr, withCv, TestFiles.Pdf("Eski CV"), "cv-eski.pdf", cv);
        var latestCv = await Upload(hr, withCv, TestFiles.Pdf("Yeni CV"), "cv-yeni.pdf", cv);
        await Upload(hr, withCv, TestFiles.Pdf("Sertifika"), "sonra-gelen-sertifika.pdf", certificate);

        (await ListedItem(hr, marker, onlyCertificate)).GetProperty("cv").ValueKind.ShouldBe(JsonValueKind.Null);
        (await ListedItem(hr, marker, onlyCertificate)).GetProperty("documentCount").GetInt32().ShouldBe(1);
        (await ListedItem(hr, marker, noDocuments)).GetProperty("cv").ValueKind.ShouldBe(JsonValueKind.Null);
        var cvOfCandidate = (await ListedItem(hr, marker, withCv)).GetProperty("cv");
        cvOfCandidate.GetProperty("documentId").GetGuid().ShouldBe(latestCv);
        cvOfCandidate.GetProperty("fileName").GetString().ShouldBe("cv-yeni.pdf");
        cvOfCandidate.GetProperty("contentType").GetString().ShouldBe("application/pdf");
    }

    [Fact]
    public async Task List_PutsTheMostRecentlyTouchedCandidateFirst()
    {
        var hr = await Hr();
        var marker = Marker();
        var first = await CreateCandidate(hr, marker);
        var second = await CreateCandidate(hr, marker);
        var third = await CreateCandidate(hr, marker);
        await factory.ProcessOutbox();

        (await hr.GetJson($"{Url}?tag={marker}")).Ids().ShouldBe([third, second, first]);

        // En eski aday üzerinde işlem yapılınca (ön görüşme açıldı) listenin başına çıkar.
        (await hr.Post($"{Url}/{first}/screenings")).StatusCode.ShouldBe(HttpStatusCode.Created);
        await factory.ProcessOutbox();

        (await hr.GetJson($"{Url}?tag={marker}")).Ids().ShouldBe([first, third, second]);
    }

    [Fact]
    public async Task List_ShowsOpenProcessesWithStageAndPosition_AndFallsBackToTheLastClosedOne()
    {
        var hr = await factory.LoginAs("Yazılım", Role.HrSpecialist);
        var marker = Marker();
        var candidate = await CreateCandidate(hr, marker);
        var idle = await CreateCandidate(hr, marker);
        var positionId = await hr.Create("/api/v1/positions", new
        {
            title = "Backend Geliştirici",
            departmentId = await hr.DictionaryItemId("Department", "Yazılım"),
            levelId = await hr.DictionaryItemId("Level", "L3"),
            headcount = 2,
        });

        var applicationId = await hr.Create("/api/v1/applications", new { candidateId = candidate, positionId });

        var open = (await ListedItem(hr, marker, candidate)).GetProperty("processes").EnumerateArray().Single();
        open.GetProperty("applicationId").GetGuid().ShouldBe(applicationId);
        open.GetProperty("positionTitle").GetString().ShouldBe("Backend Geliştirici");
        open.GetProperty("stageKind").GetString().ShouldBe("Applied");
        open.GetProperty("status").GetString().ShouldBe("Active");
        (await ListedItem(hr, marker, idle)).GetProperty("processes").GetArrayLength().ShouldBe(0);

        var reason = await hr.DictionaryItemId("RejectionReason", "Deneyim yetersiz");
        var closed = await hr.Post($"/api/v1/applications/{applicationId}/close", new { status = "Rejected", rejectionReasonId = reason });
        closed.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var last = (await ListedItem(hr, marker, candidate)).GetProperty("processes").EnumerateArray().Single();
        last.GetProperty("status").GetString().ShouldBe("Rejected");
    }

    [Fact]
    public async Task List_FlagsBlacklistedCandidates()
    {
        var specialist = await Hr();
        var manager = await factory.LoginAs(Role.HrManager);
        var marker = Marker();
        var active = await CreateCandidate(manager, marker);
        var pending = await CreateCandidate(specialist, marker);
        var clean = await CreateCandidate(manager, marker);
        var reasonId = await manager.DictionaryItemId("BlacklistReason", "Mülakata haber vermeden gelmedi");
        object Entry(Guid candidateId) => new { candidateId, reasonId, description = "Randevuya gelmedi." };

        await manager.Create("/api/v1/blacklist-entries", Entry(active));
        await specialist.Create("/api/v1/blacklist-entries", Entry(pending));

        (await ListedItem(manager, marker, active)).GetProperty("blacklist").GetString().ShouldBe("Active");
        (await ListedItem(manager, marker, pending)).GetProperty("blacklist").GetString().ShouldBe("PendingApproval");
        (await ListedItem(manager, marker, clean)).GetProperty("blacklist").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Download_Inline_ServesPdfAndImagesInTheBrowser_ButOtherTypesAreStillDownloaded()
    {
        var hr = await Hr();
        var id = await CreateCandidate(hr, Marker());
        var pdf = await Upload(hr, id, TestFiles.Pdf("CV"), "cv.pdf");
        var docx = await Upload(hr, id, TestFiles.Docx("CV"), "cv.docx");
        var anonymous = factory.CreateApiClient();

        async Task<string> LinkOf(Guid documentId) =>
            (await (await hr.Post($"{Url}/{id}/documents/{documentId}/download-link")).Json())
            .GetProperty("url").GetString()!;

        var pdfUrl = await LinkOf(pdf);
        var inlinePdf = await anonymous.GetAsync($"{pdfUrl}?inline=true");
        var attachedPdf = await anonymous.GetAsync(pdfUrl);
        var inlineDocx = await anonymous.GetAsync($"{await LinkOf(docx)}?inline=true");

        inlinePdf.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("inline");
        inlinePdf.Content.Headers.ContentDisposition!.FileNameStar.ShouldBe("cv.pdf");
        inlinePdf.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        attachedPdf.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
        inlineDocx.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
    }

    [Fact]
    public async Task List_FiltersByProcessBlacklistAndCity()
    {
        var hr = await factory.LoginAs("Yazılım", Role.HrSpecialist);
        var manager = await factory.LoginAs(Role.HrManager);
        var marker = Marker();
        var inProcess = await CreateCandidate(hr, marker);
        var idle = await CreateCandidate(hr, marker);
        var blacklisted = await CreateCandidate(manager, marker);
        var positionId = await hr.Create("/api/v1/positions", new
        {
            title = "Filtre Pozisyonu",
            departmentId = await hr.DictionaryItemId("Department", "Yazılım"),
            levelId = await hr.DictionaryItemId("Level", "L3"),
            headcount = 1,
        });
        await hr.Create("/api/v1/applications", new { candidateId = inProcess, positionId });
        await manager.Create("/api/v1/blacklist-entries", new
        {
            candidateId = blacklisted,
            reasonId = await manager.DictionaryItemId("BlacklistReason", "Mülakata haber vermeden gelmedi"),
            description = "Randevuya gelmedi.",
        });
        var ankara = await hr.Create(Url, new
        {
            firstName = "Şehirli", lastName = $"Aday{Guid.NewGuid():N}", email = $"{Guid.NewGuid():N}@ornek.com",
            city = "Ankara", tags = new[] { marker },
        });

        string Ids(string query) => string.Join(",", Sorted(hr, marker, query).Result);

        (await Sorted(hr, marker, "process=Active")).ShouldBe([inProcess]);
        (await Sorted(hr, marker, "process=None")).ShouldBe(new[] { idle, blacklisted, ankara }.OrderBy(i => i));
        (await Sorted(hr, marker, "process=Hired")).ShouldBeEmpty();
        (await Sorted(hr, marker, "blacklisted=true")).ShouldBe([blacklisted]);
        (await Sorted(hr, marker, "blacklisted=false")).ShouldBe(new[] { inProcess, idle, ankara }.OrderBy(i => i));
        (await Sorted(hr, marker, "city=ANKA")).ShouldBe([ankara]);
        (await Sorted(hr, marker, "process=None&blacklisted=false&city=ankara")).ShouldBe([ankara]);
        Ids("").ShouldNotBeEmpty();
    }

    private static async Task<IEnumerable<Guid>> Sorted(ApiUser hr, string marker, string query) =>
        (await hr.GetJson($"{Url}?tag={marker}&{query}")).Ids().OrderBy(i => i).ToList();
}
