using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Recruitment.Api.IntegrationTests.Infrastructure;
using Recruitment.Domain.Audit;
using Recruitment.Domain.Users;
using Recruitment.Infrastructure.Persistence;
using Shouldly;

namespace Recruitment.Api.IntegrationTests.Candidates;

public sealed class CandidateEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/v1/candidates";

    private static string Marker() => "m" + Guid.NewGuid().ToString("N")[..10];

    private static string RandomPhone() => "0532" + Random.Shared.Next(1_000_000, 9_999_999);

    private static object Body(
        string firstName = "Işıl",
        string lastName = "Çağlayan",
        string? email = null,
        string? phone = null,
        string[]? tags = null,
        Guid? sourceId = null,
        bool confirmDuplicate = false) =>
        new
        {
            firstName,
            lastName,
            email,
            phone,
            linkedInUrl = "https://www.linkedin.com/in/isil",
            city = "İzmir",
            sourceId,
            tags = tags ?? [],
            notes = "Referansla geldi.",
            confirmDuplicate,
        };

    private Task<ApiUser> Hr() => factory.LoginAs(Role.HrSpecialist);

    private static string Search(string term, string marker) =>
        $"{Url}?search={Uri.EscapeDataString($"{term} {marker}")}";

    [Fact]
    public async Task Create_WithNameAndPhoneOnly_AddsCandidateToPool()
    {
        var hr = await Hr();
        var phone = RandomPhone();

        var id = await hr.Create(Url, new { firstName = "Ali", lastName = "Veli", phone });

        var candidate = await hr.GetJson($"{Url}/{id}");
        candidate.GetProperty("firstName").GetString().ShouldBe("Ali");
        candidate.GetProperty("phone").GetString().ShouldBe(phone);
        candidate.GetProperty("email").ValueKind.ShouldBe(JsonValueKind.Null);
        candidate.GetProperty("inTalentPool").GetBoolean().ShouldBeTrue();
        candidate.GetProperty("documents").GetArrayLength().ShouldBe(0);
        candidate.GetProperty("screenings").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Create_WithoutNameOrContact_Returns400WithFieldErrors()
    {
        var hr = await Hr();

        var response = await hr.Post(Url, new { firstName = " ", lastName = "", email = "", phone = " " });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Json()).GetProperty("errors");
        errors.GetProperty("firstName")[0].GetString().ShouldBe("Adayın adını yazmalısınız.");
        errors.GetProperty("lastName")[0].GetString().ShouldBe("Adayın soyadını yazmalısınız.");
        errors.GetProperty("email")[0].GetString().ShouldBe("E-posta veya telefon alanlarından en az biri dolu olmalı.");
    }

    [Fact]
    public async Task Create_WithMalformedContactOrLinks_Returns400()
    {
        var hr = await Hr();

        var response = await hr.Post(Url, new
        {
            firstName = "Ali", lastName = "Veli", email = "ali-at-ornek", phone = "12",
            linkedInUrl = "linkedin.com/in/ali", gitHubUrl = "javascript:alert(1)",
        });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Json()).GetProperty("errors");
        errors.GetProperty("email")[0].GetString().ShouldBe("Geçerli bir e-posta adresi yazın.");
        errors.GetProperty("phone")[0].GetString().ShouldBe("Geçerli bir telefon numarası yazın.");
        errors.TryGetProperty("linkedInUrl", out _).ShouldBeTrue();
        errors.TryGetProperty("gitHubUrl", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Candidates_AreForHrOnly()
    {
        var teamLead = await factory.LoginAs(Role.TeamLead);
        var executive = await factory.LoginAs(Role.Executive);

        (await teamLead.Client.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await teamLead.Post(Url, Body(email: "x@ornek.com"))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await executive.Client.GetAsync($"{Url}/{Guid.NewGuid()}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Create_WithExistingPhoneInAnotherFormat_WarnsThenAddsWhenConfirmed()
    {
        var hr = await Hr();
        var digits = Random.Shared.Next(1_000_000, 9_999_999).ToString();
        var firstId = await hr.Create(Url, Body(phone: $"+90 (532) {digits[..3]} {digits[3..5]} {digits[5..]}"));

        var warning = await hr.Post(Url, Body(firstName: "Işıl", lastName: "Demir", phone: $"0532{digits}"));
        warning.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await warning.Json();
        problem.GetProperty("code").GetString().ShouldBe("Candidate.PossibleDuplicate");
        var duplicate = problem.GetProperty("duplicates").EnumerateArray().Single();
        duplicate.GetProperty("id").GetGuid().ShouldBe(firstId);
        duplicate.GetProperty("fullName").GetString().ShouldBe("Işıl Çağlayan");

        var secondId = await hr.Create(
            Url, Body(firstName: "Işıl", lastName: "Demir", phone: $"0532{digits}", confirmDuplicate: true));

        (await hr.GetJson($"{Url}/{secondId}")).GetProperty("possibleDuplicates").EnumerateArray()
            .Select(d => d.GetProperty("id").GetGuid()).ShouldBe([firstId]);
        await factory.ProcessOutbox();
        (await hr.GetJson($"{Url}/{secondId}/timeline")).GetProperty("items")[0].GetProperty("summary").GetString()
            .ShouldBe("Aday, mükerrer kayıt uyarısına rağmen eklendi.");
    }

    [Fact]
    public async Task Duplicates_AreFoundByEmailIgnoringCase_AndExcludeTheCandidateItself()
    {
        var hr = await Hr();
        var email = $"{Marker()}@ornek.com";
        var id = await hr.Create(Url, Body(email: email));

        var found = await hr.GetJson($"{Url}/duplicates?email={email.ToUpperInvariant()}");
        var excluded = await hr.GetJson($"{Url}/duplicates?email={email}&excludeId={id}");
        var partialPhone = await hr.GetJson($"{Url}/duplicates?phone=0532");

        found.EnumerateArray().Single().GetProperty("id").GetGuid().ShouldBe(id);
        excluded.GetArrayLength().ShouldBe(0);
        partialPhone.GetArrayLength().ShouldBe(0);
    }

    [Theory]
    [InlineData("IŞIL")]
    [InlineData("ışıl çağlayan")]
    [InlineData("çağ")]
    [InlineData("ornek")]
    [InlineData("İZMİR")]
    [InlineData("react")]
    public async Task Search_FindsByNameContactCityAndTag_WithPrefixAndTurkishCase(string term)
    {
        var hr = await Hr();
        var marker = Marker();
        var id = await hr.Create(Url, Body(email: $"isil.{marker}@ornek.com", tags: ["React", marker]));

        var found = await hr.GetJson(Search(term, marker));

        found.Ids().ShouldBe([id]);
    }

    [Fact]
    public async Task Search_ByPhoneDigits_FindsCandidate_AndUnrelatedTermFindsNothing()
    {
        var hr = await Hr();
        var marker = Marker();
        var phone = RandomPhone();
        var id = await hr.Create(Url, Body(phone: phone, tags: [marker]));

        (await hr.GetJson(Search(phone[1..8], marker))).Ids().ShouldBe([id]);
        (await hr.GetJson(Search("kubernetes", marker))).Ids().ShouldBeEmpty();
        // Notlar aranmaz.
        (await hr.GetJson(Search("referansla", marker))).Ids().ShouldBeEmpty();
    }

    [Fact]
    public async Task List_FiltersBySourceTagAndTalentPool()
    {
        var hrManager = await factory.LoginAs(Role.HrManager);
        var marker = Marker();
        var sourceId = await hrManager.Create("/api/v1/dictionaries/CandidateSource/items", new { name = $"Kaynak {marker}" });
        var withSource = await hrManager.Create(Url, Body(email: $"a.{marker}@ornek.com", tags: [marker], sourceId: sourceId));
        var outOfPool = await hrManager.Create(Url, Body(email: $"b.{marker}@ornek.com", tags: [marker, "Go"]));
        // Aday eklenince havuza girer; havuzdan çıkarılan aday süzgeçte ayrışır.
        await hrManager.Post($"{Url}/{outOfPool}/talent-pool", new { inTalentPool = false });

        (await hrManager.GetJson($"{Url}?tag={marker}")).Ids().ShouldBe([outOfPool, withSource]);
        (await hrManager.GetJson($"{Url}?tag={marker}&sourceId={sourceId}")).Ids().ShouldBe([withSource]);
        (await hrManager.GetJson($"{Url}?tag={marker}&inTalentPool=true")).Ids().ShouldBe([withSource]);
        (await hrManager.GetJson($"{Url}?tag={marker}&inTalentPool=false")).Ids().ShouldBe([outOfPool]);
        var listed = (await hrManager.GetJson($"{Url}?tag={marker}&sourceId={sourceId}")).GetProperty("items")[0];
        listed.GetProperty("sourceName").GetString().ShouldBe($"Kaynak {marker}");
        listed.GetProperty("tags").EnumerateArray().Select(t => t.GetString()).ShouldBe([marker]);
    }

    [Fact]
    public async Task Update_ChangesProfile_AndWarnsOnlyWhenContactChangesToAnExistingOne()
    {
        var hr = await Hr();
        var marker = Marker();
        var takenEmail = $"taken.{marker}@ornek.com";
        await hr.Create(Url, Body(email: takenEmail));
        var id = await hr.Create(Url, Body(email: $"own.{marker}@ornek.com"));

        var renamed = await hr.Put($"{Url}/{id}", Body(lastName: "Demir", email: $"own.{marker}@ornek.com", tags: ["Vue"]));
        var conflict = await hr.Put($"{Url}/{id}", Body(email: takenEmail));
        var confirmed = await hr.Put($"{Url}/{id}", Body(email: takenEmail, confirmDuplicate: true));
        // İletişim bilgisi değişmediyse aynı uyarı tekrar gösterilmez.
        var unchanged = await hr.Put($"{Url}/{id}", Body(lastName: "Kaya", email: takenEmail));

        renamed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        conflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        confirmed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        unchanged.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var candidate = await hr.GetJson($"{Url}/{id}");
        candidate.GetProperty("lastName").GetString().ShouldBe("Kaya");
        candidate.GetProperty("email").GetString().ShouldBe(takenEmail);
    }

    [Fact]
    public async Task TalentPool_Toggle_IsRecordedInTimelineWithActor()
    {
        var hr = await Hr();
        var id = await hr.Create(Url, Body(email: $"{Marker()}@ornek.com"));

        (await hr.Post($"{Url}/{id}/talent-pool", new { inTalentPool = false })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await hr.Post($"{Url}/{id}/talent-pool", new { inTalentPool = true })).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await factory.ProcessOutbox();

        (await hr.GetJson($"{Url}/{id}")).GetProperty("inTalentPool").GetBoolean().ShouldBeTrue();
        var timeline = (await hr.GetJson($"{Url}/{id}/timeline")).GetProperty("items").EnumerateArray().ToList();
        timeline.Select(t => t.GetProperty("summary").GetString())
            .ShouldBe(["Yetenek havuzuna alındı.", "Yetenek havuzundan çıkarıldı.", "Aday eklendi."]);
        timeline.ShouldAllBe(t => t.GetProperty("actorName").GetString() == "Test Kullanıcısı");
    }

    [Fact]
    public async Task UploadDocx_StoresFile_AndMakesItsContentSearchableAfterProcessing()
    {
        var hr = await Hr();
        var marker = Marker();
        var id = await hr.Create(Url, Body(email: $"{marker}@ornek.com", tags: [marker]));

        var upload = await hr.Client.PostAsync(
            $"{Url}/{id}/documents",
            TestFiles.AsUpload(TestFiles.Docx("Mikroservis mimarisi", "ÇEVİK yöntemler"), "C:\\belgeler\\Özgeçmiş.docx"));

        upload.StatusCode.ShouldBe(HttpStatusCode.Created);
        var document = (await hr.GetJson($"{Url}/{id}")).GetProperty("documents").EnumerateArray().Single();
        document.GetProperty("fileName").GetString().ShouldBe("Özgeçmiş.docx");
        document.GetProperty("contentType").GetString()
            .ShouldBe("application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        document.GetProperty("uploadedByName").GetString().ShouldBe("Test Kullanıcısı");

        (await hr.GetJson(Search("mikroservis", marker))).Ids().ShouldBeEmpty();
        await factory.ProcessOutbox();

        (await hr.GetJson(Search("mikroservis", marker))).Ids().ShouldBe([id]);
        (await hr.GetJson(Search("çevik yöntem", marker))).Ids().ShouldBe([id]);
        (await hr.GetJson($"{Url}/{id}/timeline")).GetProperty("items")[0].GetProperty("summary").GetString()
            .ShouldBe("Dosya yüklendi: Özgeçmiş.docx");
        (await hr.GetJson($"{Url}?tag={marker}")).GetProperty("items")[0].GetProperty("documentCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task UploadPdf_MakesItsContentSearchableAfterProcessing()
    {
        var hr = await Hr();
        var marker = Marker();
        var id = await hr.Create(Url, Body(email: $"{marker}@ornek.com", tags: [marker]));

        var upload = await hr.Client.PostAsync(
            $"{Url}/{id}/documents", TestFiles.AsUpload(TestFiles.Pdf("Kubernetes Terraform deneyimi"), "cv.pdf"));
        await factory.ProcessOutbox();

        upload.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await hr.GetJson(Search("terraform", marker))).Ids().ShouldBe([id]);
    }

    [Fact]
    public async Task Upload_OfUnreadableFile_StillStoresIt()
    {
        var hr = await Hr();
        var id = await hr.Create(Url, Body(email: $"{Marker()}@ornek.com"));

        // Başlığı PDF ama gövdesi bozuk: saklanır, yalnızca aranamaz.
        var upload = await hr.Client.PostAsync(
            $"{Url}/{id}/documents", TestFiles.AsUpload("%PDF-1.7 bozuk icerik"u8.ToArray(), "bozuk.pdf"));
        var processed = await factory.ProcessOutbox();

        upload.StatusCode.ShouldBe(HttpStatusCode.Created);
        processed.ShouldBeGreaterThan(0);
        (await hr.GetJson($"{Url}/{id}")).GetProperty("documents").GetArrayLength().ShouldBe(1);
    }

    [Theory]
    [InlineData("virus.exe", "MZ zararli", "Yalnızca PDF, Word (DOC, DOCX) veya resim (PNG, JPG) dosyası yükleyebilirsiniz.")]
    [InlineData("cv.pdf", "MZ aslinda exe", "Dosyanın içeriği uzantısıyla uyuşmuyor. Dosyayı kontrol edip tekrar yükleyin.")]
    [InlineData("cv.docx", "%PDF-1.4 aslinda pdf", "Dosyanın içeriği uzantısıyla uyuşmuyor. Dosyayı kontrol edip tekrar yükleyin.")]
    [InlineData("cv.pdf", "", "Boş dosya yüklenemez.")]
    public async Task Upload_OfDisallowedOrMismatchedFile_Returns400(string fileName, string content, string message)
    {
        var hr = await Hr();
        var id = await hr.Create(Url, Body(email: $"{Marker()}@ornek.com"));

        var upload = await hr.Client.PostAsync(
            $"{Url}/{id}/documents", TestFiles.AsUpload(System.Text.Encoding.ASCII.GetBytes(content), fileName));

        upload.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await upload.Json()).GetProperty("errors").GetProperty("file")[0].GetString().ShouldBe(message);
        (await hr.GetJson($"{Url}/{id}")).GetProperty("documents").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Upload_LargerThanLimit_Returns400()
    {
        var hr = await Hr();
        var id = await hr.Create(Url, Body(email: $"{Marker()}@ornek.com"));
        var tooLarge = new byte[10 * 1024 * 1024 + 1];
        "%PDF"u8.CopyTo(tooLarge);

        var upload = await hr.Client.PostAsync($"{Url}/{id}/documents", TestFiles.AsUpload(tooLarge, "buyuk.pdf"));

        upload.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await upload.Json()).GetProperty("errors").GetProperty("file")[0].GetString()
            .ShouldBe("Dosya en fazla 10 MB olabilir.");
    }

    [Fact]
    public async Task DownloadLink_ServesFileWithoutSession_AndIsLoggedAndTamperProof()
    {
        var hr = await Hr();
        var id = await hr.Create(Url, Body(email: $"{Marker()}@ornek.com"));
        var pdf = TestFiles.Pdf("Ozgecmis");
        var documentId = (await (await hr.Client.PostAsync($"{Url}/{id}/documents", TestFiles.AsUpload(pdf, "cv.pdf"))).Json())
            .GetProperty("id").GetGuid();

        var link = await (await hr.Post($"{Url}/{id}/documents/{documentId}/download-link")).Json();
        var url = link.GetProperty("url").GetString()!;
        var anonymous = factory.CreateApiClient();
        var download = await anonymous.GetAsync(url);
        var tampered = await anonymous.GetAsync(url[..^6] + "AAAAAA");

        url.ShouldStartWith("/api/v1/files/");
        download.StatusCode.ShouldBe(HttpStatusCode.OK);
        download.Content.Headers.ContentType!.MediaType.ShouldBe("application/pdf");
        download.Content.Headers.ContentDisposition!.FileNameStar.ShouldBe("cv.pdf");
        (await download.Content.ReadAsByteArrayAsync()).ShouldBe(pdf);
        tampered.StatusCode.ShouldBe(HttpStatusCode.NotFound);

        await using var scope = factory.Services.CreateAsyncScope();
        var log = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AccessLogs
            .SingleAsync(a => a.DocumentId == documentId);
        log.UserId.ShouldBe(hr.Id);
        log.CandidateId.ShouldBe(id);
        log.Action.ShouldBe(AccessAction.CvDownloaded);
    }

    [Fact]
    public async Task DownloadLink_ForDocumentOfAnotherCandidate_Returns404()
    {
        var hr = await Hr();
        var owner = await hr.Create(Url, Body(email: $"{Marker()}@ornek.com"));
        var other = await hr.Create(Url, Body(email: $"{Marker()}@ornek.com"));
        var documentId = (await (await hr.Client.PostAsync(
                $"{Url}/{owner}/documents", TestFiles.AsUpload(TestFiles.Pdf("x"), "cv.pdf"))).Json())
            .GetProperty("id").GetGuid();

        (await hr.Post($"{Url}/{other}/documents/{documentId}/download-link")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteDocument_RemovesItFromCardSearchAndExistingLinks()
    {
        var hr = await Hr();
        var marker = Marker();
        var id = await hr.Create(Url, Body(email: $"{marker}@ornek.com", tags: [marker]));
        var documentId = (await (await hr.Client.PostAsync(
                $"{Url}/{id}/documents", TestFiles.AsUpload(TestFiles.Docx("Elasticsearch"), "cv.docx"))).Json())
            .GetProperty("id").GetGuid();
        await factory.ProcessOutbox();
        var url = (await (await hr.Post($"{Url}/{id}/documents/{documentId}/download-link")).Json())
            .GetProperty("url").GetString()!;
        (await hr.GetJson(Search("elasticsearch", marker))).Ids().ShouldBe([id]);

        var deleted = await hr.Client.DeleteAsync($"{Url}/{id}/documents/{documentId}");

        deleted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await hr.GetJson($"{Url}/{id}")).GetProperty("documents").GetArrayLength().ShouldBe(0);
        (await hr.GetJson(Search("elasticsearch", marker))).Ids().ShouldBeEmpty();
        (await factory.CreateApiClient().GetAsync(url)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await hr.Client.DeleteAsync($"{Url}/{id}/documents/{documentId}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Screening_StartedEmpty_ThenSavedWithAllFields_IsReturnedOnCard()
    {
        var hr = await Hr();
        var marker = Marker();
        var id = await hr.Create(Url, Body(email: $"{marker}@ornek.com", tags: [marker]));

        var screeningId = await hr.Create($"{Url}/{id}/screenings", new { });
        var empty = (await hr.GetJson($"{Url}/{id}")).GetProperty("screenings").EnumerateArray().Single();
        empty.GetProperty("interviewerName").GetString().ShouldBe("Test Kullanıcısı");
        empty.GetProperty("overallRating").ValueKind.ShouldBe(JsonValueKind.Null);

        var saved = await hr.Put($"{Url}/{id}/screenings/{screeningId}", new
        {
            channel = "Teams",
            interviewedAt = "2026-10-08T11:30:00+03:00",
            educations = new[] { new { school = "ODTÜ", department = "Bilgisayar Mühendisliği", degree = "Lisans", graduationYear = 2018 } },
            educationNote = "Yüksek lisans devam ediyor",
            languages = new[] { new { language = "İngilizce", level = "Advanced" } },
            languagesNote = "Teknik toplantı yürütebiliyor",
            softSkills = "İletişimi güçlü",
            technicalSkillTags = new[] { "Kafka", "Go" },
            technicalSkills = "Dağıtık sistemler",
            experienceSummary = "7 yıl backend",
            motivation = "Ürün şirketi arıyor",
            totalExperienceMonths = 88,
            fieldExperienceMonths = 62,
            expectedSalary = new { amount = 185000.50, currency = "TRY", isNet = true },
            expectedSalaryMax = 210000,
            noticePeriodWeeks = 4,
            hrComment = "Pazarlık payı var",
            currentSalary = new { amount = 150000, currency = "TRY", isNet = false },
            workModel = "Hybrid",
            locationProvince = "İstanbul",
            locationDistrict = "Kadıköy",
            locationNote = "Anadolu yakasında oturuyor",
            openToRelocation = false,
            hasTravelRestriction = true,
            militaryStatus = "Done",
            overallRating = 4,
        });

        saved.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var screening = (await hr.GetJson($"{Url}/{id}")).GetProperty("screenings").EnumerateArray().Single();
        screening.GetProperty("channel").GetString().ShouldBe("Teams");
        screening.GetProperty("interviewedAt").GetDateTimeOffset().ShouldBe(DateTimeOffset.Parse("2026-10-08T08:30:00Z"));
        screening.GetProperty("educations")[0].GetProperty("school").GetString().ShouldBe("ODTÜ");
        screening.GetProperty("educations")[0].GetProperty("graduationYear").GetInt32().ShouldBe(2018);
        screening.GetProperty("languages")[0].GetProperty("level").GetString().ShouldBe("Advanced");
        screening.GetProperty("technicalSkillTags").EnumerateArray().Select(t => t.GetString()).ShouldBe(["Kafka", "Go"]);
        screening.GetProperty("totalExperienceMonths").GetInt32().ShouldBe(88);
        screening.GetProperty("fieldExperienceMonths").GetInt32().ShouldBe(62);
        screening.GetProperty("educationNote").GetString().ShouldBe("Yüksek lisans devam ediyor");
        screening.GetProperty("languagesNote").GetString().ShouldBe("Teknik toplantı yürütebiliyor");
        screening.GetProperty("expectedSalary").GetProperty("amount").GetDecimal().ShouldBe(185000.50m);
        screening.GetProperty("expectedSalaryMax").GetDecimal().ShouldBe(210000m);
        screening.GetProperty("locationProvince").GetString().ShouldBe("İstanbul");
        screening.GetProperty("locationDistrict").GetString().ShouldBe("Kadıköy");
        screening.GetProperty("locationNote").GetString().ShouldBe("Anadolu yakasında oturuyor");
        screening.GetProperty("hasTravelRestriction").GetBoolean().ShouldBeTrue();
        screening.TryGetProperty("currentBenefits", out _).ShouldBeFalse();
        screening.TryGetProperty("otherProcesses", out _).ShouldBeFalse();
        screening.GetProperty("expectedSalary").GetProperty("isNet").GetBoolean().ShouldBeTrue();
        screening.GetProperty("currentSalary").GetProperty("isNet").GetBoolean().ShouldBeFalse();
        screening.GetProperty("workModel").GetString().ShouldBe("Hybrid");
        screening.GetProperty("openToRelocation").GetBoolean().ShouldBeFalse();
        screening.GetProperty("militaryStatus").GetString().ShouldBe("Done");
        screening.GetProperty("overallRating").GetInt32().ShouldBe(4);
        screening.GetProperty("hrComment").GetString().ShouldBe("Pazarlık payı var");

        // Yetkinlikler aranır; İK yorumu aranmaz.
        (await hr.GetJson(Search("kafka", marker))).Ids().ShouldBe([id]);
        (await hr.GetJson(Search("dağıtık", marker))).Ids().ShouldBe([id]);
        (await hr.GetJson(Search("pazarlık", marker))).Ids().ShouldBeEmpty();
    }

    [Fact]
    public async Task Screening_SavedAgainWithEmptyForm_ClearsEverything()
    {
        var hr = await Hr();
        var id = await hr.Create(Url, Body(email: $"{Marker()}@ornek.com"));
        var screeningId = await hr.Create($"{Url}/{id}/screenings", new { });
        await hr.Put($"{Url}/{id}/screenings/{screeningId}", new
        {
            overallRating = 5,
            expectedSalary = new { amount = 100000, currency = "TRY", isNet = true },
            educations = new[] { new { school = "İTÜ" } },
        });

        var cleared = await hr.Put($"{Url}/{id}/screenings/{screeningId}", new { });

        cleared.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var screening = (await hr.GetJson($"{Url}/{id}")).GetProperty("screenings").EnumerateArray().Single();
        screening.GetProperty("overallRating").ValueKind.ShouldBe(JsonValueKind.Null);
        screening.GetProperty("expectedSalary").ValueKind.ShouldBe(JsonValueKind.Null);
        screening.GetProperty("educations").GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task Screening_WorkModelAny_IsStoredAndReturned()
    {
        var hr = await Hr();
        var id = await hr.Create(Url, Body(email: $"{Marker()}@ornek.com"));
        var screeningId = await hr.Create($"{Url}/{id}/screenings", new { });

        (await hr.Put($"{Url}/{id}/screenings/{screeningId}", new { workModel = "Any" })).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await hr.GetJson($"{Url}/{id}")).GetProperty("screenings").EnumerateArray().Single()
            .GetProperty("workModel").GetString().ShouldBe("Any");
    }

    [Fact]
    public async Task Screening_CanBeDeletedAfterSaving_AndLeavesATimelineEntry()
    {
        var hr = await Hr();
        var marker = Marker();
        var id = await hr.Create(Url, Body(email: $"{marker}@ornek.com", tags: [marker]));
        var kept = await hr.Create($"{Url}/{id}/screenings", new { });
        var removed = await hr.Create($"{Url}/{id}/screenings", new { });
        await hr.Put($"{Url}/{id}/screenings/{removed}", new
        {
            overallRating = 3,
            technicalSkillTags = new[] { "Silinecek" + marker },
            // JSON kolonunda tutulan alt kayıtlar da olsun: silme bunlar yüzünden patlamamalı.
            educations = new[] { new { school = "ODTÜ", department = "Bilgisayar", degree = "Lisans", graduationYear = 2018 } },
            languages = new[] { new { language = "İngilizce", level = "Advanced" } },
        });

        (await hr.Delete($"{Url}/{id}/screenings/{removed}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var screenings = (await hr.GetJson($"{Url}/{id}")).GetProperty("screenings").EnumerateArray().ToList();
        screenings.Select(s => s.GetProperty("id").GetGuid()).ShouldBe([kept]);
        (await hr.GetJson(Search("silinecek" + marker.ToLowerInvariant(), marker))).Ids().ShouldBeEmpty();
        (await hr.Delete($"{Url}/{id}/screenings/{removed}")).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        await factory.ProcessOutbox();
        var timeline = await hr.GetJson($"{Url}/{id}/timeline");
        timeline.GetProperty("items").EnumerateArray()
            .Select(e => e.GetProperty("type").GetString()).ShouldContain("ScreeningDeleted");

        var lead = await factory.LoginAs(Role.TeamLead);
        (await lead.Delete($"{Url}/{id}/screenings/{kept}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Screening_WithOutOfRangeValues_Returns400_AndUnknownScreeningReturns404()
    {
        var hr = await Hr();
        var id = await hr.Create(Url, Body(email: $"{Marker()}@ornek.com"));
        var screeningId = await hr.Create($"{Url}/{id}/screenings", new { });

        var invalid = await hr.Put($"{Url}/{id}/screenings/{screeningId}", new
        {
            overallRating = 6, totalExperienceMonths = -1, expectedSalary = new { amount = -5, currency = "TRY", isNet = true },
            locationDistrict = "Kadıköy",
        });
        var badRange = await hr.Put($"{Url}/{id}/screenings/{screeningId}", new
        {
            expectedSalary = new { amount = 100000, currency = "TRY", isNet = true }, expectedSalaryMax = 90000,
        });
        var unknown = await hr.Put($"{Url}/{id}/screenings/{Guid.NewGuid()}", new { overallRating = 3 });

        invalid.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await invalid.Json()).GetProperty("errors");
        errors.GetProperty("overallRating")[0].GetString().ShouldBe("Genel puan 1 ile 5 arasında olmalı.");
        errors.GetProperty("totalExperienceMonths")[0].GetString().ShouldBe("Toplam deneyim 0 ile 70 yıl arasında olmalı.");
        errors.GetProperty("locationDistrict")[0].GetString().ShouldBe("İlçeyi seçmeden önce ili seçin.");
        badRange.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await badRange.Json()).GetProperty("errors").GetProperty("expectedSalaryMax")[0].GetString()
            .ShouldBe("Ücret beklentisinin üst değeri alt değerinden küçük olamaz.");
        errors.GetProperty("expectedSalary")[0].GetString().ShouldBe("Ücret beklentisi negatif olamaz.");
        unknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
