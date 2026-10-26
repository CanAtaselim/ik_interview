using System.Net;
using System.Text.Json;
using Recruitment.Api.IntegrationTests.Infrastructure;
using Recruitment.Domain.Users;
using Shouldly;

namespace Recruitment.Api.IntegrationTests.Dictionaries;

/// <summary>Kullanılan sözlük öğesi yeniden adlandırılamaz ve silinemez; kullanımı kalkınca serbest kalır.</summary>
public sealed class DictionaryUsageRulesTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static string Items(string type) => $"/api/v1/dictionaries/{type}/items";

    private static string Item(Guid id) => $"/api/v1/dictionary-items/{id}";

    private static string Marker() => "m" + Guid.NewGuid().ToString("N")[..10];

    private static object Edit(string name, bool isActive = true) => new { name, sortOrder = 1, isActive };

    private Task<ApiUser> Admin() => factory.LoginAs(Role.SystemAdmin);

    private static async Task<Guid> Candidate(ApiUser hr, Guid? sourceId = null) =>
        await hr.Create("/api/v1/candidates", new
        {
            firstName = "Deniz",
            lastName = $"Aday{Guid.NewGuid():N}",
            email = $"{Guid.NewGuid():N}@ornek.com",
            sourceId,
        });

    [Fact]
    public async Task UnusedItem_CanBeRenamedAndDeleted_AndItsNameBecomesAvailableAgain()
    {
        var admin = await Admin();
        var name = $"Oda {Marker()}";
        var id = await admin.Create(Items("Room"), new { name });

        (await admin.Put(Item(id), Edit(name + " B"))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await admin.Client.DeleteAsync(Item(id))).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await admin.GetJson(Items("Room") + "?includeInactive=true")).EnumerateArray()
            .Select(i => i.GetProperty("id").GetGuid()).ShouldNotContain(id);
        (await admin.Client.DeleteAsync(Item(id))).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await admin.Post(Items("Room"), new { name = name + " B" })).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task UsedItem_CannotBeRenamedOrDeleted_UntilTheUsageIsGone()
    {
        var admin = await Admin();
        var hr = await factory.LoginAs(Role.HrSpecialist);
        var name = $"Belge {Marker()}";
        var typeId = await admin.Create(Items("DocumentType"), new { name });
        var candidateId = await Candidate(hr);
        var upload = TestFiles.AsUpload(TestFiles.Pdf("x"), "belge.pdf");
        upload.Add(new StringContent(typeId.ToString()), "typeId");
        var documentId = (await (await hr.Client.PostAsync($"/api/v1/candidates/{candidateId}/documents", upload)).Json())
            .GetProperty("id").GetGuid();

        var rename = await admin.Put(Item(typeId), Edit(name + " 2"));
        var delete = await admin.Client.DeleteAsync(Item(typeId));

        rename.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await rename.Json();
        problem.GetProperty("code").GetString().ShouldBe("Dictionary.InUse");
        problem.GetProperty("detail").GetString()!.ShouldContain("1 aday dosyasında");
        problem.GetProperty("usages")[0].GetProperty("count").GetInt32().ShouldBe(1);
        delete.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        // Pasif yapmak ve sırayı değiştirmek (ad aynı kalır) kullanımdayken de serbesttir.
        (await admin.Put(Item(typeId), Edit(name, isActive: false))).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await hr.Client.DeleteAsync($"/api/v1/candidates/{candidateId}/documents/{documentId}"))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await admin.Put(Item(typeId), Edit(name + " 2"))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await admin.Client.DeleteAsync(Item(typeId))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ItemsUsedByTextValues_AreProtectedToo_ForExamplePositionTitles()
    {
        var admin = await Admin();
        var hr = await factory.LoginAs("Yazılım", Role.HrSpecialist);
        var title = $"Pozisyon {Marker()}";
        var id = await admin.Create(Items("PositionTitle"), new { name = title });
        await hr.Create("/api/v1/positions", new
        {
            title = title.ToUpperInvariant(),
            departmentId = await hr.DictionaryItemId("Department", "Yazılım"),
            levelId = await hr.DictionaryItemId("Level", "L3"),
            headcount = 1,
        });

        var rename = await admin.Put(Item(id), Edit(title + " 2"));

        rename.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await rename.Json()).GetProperty("detail").GetString()!.ShouldContain("1 pozisyonda");
        (await admin.Client.DeleteAsync(Item(id))).StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task CvDocumentType_IsProtected()
    {
        var admin = await Admin();
        var cvId = await admin.DictionaryItemId("DocumentType", "CV");

        var rename = await admin.Put(Item(cvId), Edit("Özgeçmiş"));
        var delete = await admin.Client.DeleteAsync(Item(cvId));

        rename.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        delete.StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
        (await admin.Put(Item(cvId), Edit("CV", isActive: true))).StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AdminList_PagesSearchesAndFiltersOnTheServer_AndShowsUsage()
    {
        var admin = await Admin();
        var hr = await factory.LoginAs(Role.HrSpecialist);
        var marker = Marker();
        var ids = new List<Guid>();
        for (var i = 1; i <= 5; i++)
            ids.Add(await admin.Create(Items("CandidateSource"), new { name = $"Kaynak {marker} {i}" }));
        await Candidate(hr, ids[0]);
        await admin.Put(Item(ids[4]), Edit($"Kaynak {marker} 5", isActive: false));
        string List(string query) => $"/api/v1/dictionaries/CandidateSource/admin-items?search={marker.ToUpperInvariant()}{query}";

        var first = await admin.GetJson(List("&pageSize=2"));
        var second = await admin.GetJson(List("&pageSize=2&page=3"));
        var inactive = await admin.GetJson(List("&isActive=false"));
        var active = await admin.GetJson(List("&isActive=true&pageSize=10"));

        first.GetProperty("totalCount").GetInt32().ShouldBe(5);
        first.GetProperty("items").GetArrayLength().ShouldBe(2);
        second.GetProperty("items").GetArrayLength().ShouldBe(1);
        inactive.Ids().ShouldBe([ids[4]]);
        active.GetProperty("totalCount").GetInt32().ShouldBe(4);
        var used = active.GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetGuid() == ids[0]);
        used.GetProperty("usageCount").GetInt32().ShouldBe(1);
        used.GetProperty("usages")[0].GetProperty("area").GetString().ShouldBe("adayda");
        used.GetProperty("isSystem").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task AdminListAndDelete_ByNonManager_Return403()
    {
        var hr = await factory.LoginAs(Role.HrSpecialist);
        var levelId = await hr.DictionaryItemId("Level", "L4");

        (await hr.Client.GetAsync("/api/v1/dictionaries/Level/admin-items")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await hr.Client.DeleteAsync(Item(levelId))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminList_WorksForEveryDictionaryType_AndCountsEducationLevelsStoredAsText()
    {
        var admin = await Admin();
        var hr = await factory.LoginAs(Role.HrSpecialist);
        var candidateId = await Candidate(hr);
        var screeningId = (await (await hr.Post($"/api/v1/candidates/{candidateId}/screenings")).Json()).GetProperty("id").GetGuid();
        var update = await hr.Put($"/api/v1/candidates/{candidateId}/screenings/{screeningId}", new
        {
            educations = new[] { new { school = "ODTÜ", department = "Bilgisayar", degree = "Yüksek Lisans", graduationYear = 2020 } },
        });
        update.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        foreach (var type in Enum.GetNames<Recruitment.Domain.Dictionaries.DictionaryType>())
        {
            var response = await admin.Client.GetAsync($"/api/v1/dictionaries/{type}/admin-items?pageSize=100");
            response.StatusCode.ShouldBe(HttpStatusCode.OK, type);
        }

        var education = await admin.GetJson("/api/v1/dictionaries/EducationLevel/admin-items?search=yüksek");
        var master = education.GetProperty("items").EnumerateArray().Single();
        master.GetProperty("usageCount").GetInt32().ShouldBeGreaterThanOrEqualTo(1);
        master.GetProperty("usages")[0].GetProperty("area").GetString().ShouldBe("ön görüşme kaydında");
    }
}
