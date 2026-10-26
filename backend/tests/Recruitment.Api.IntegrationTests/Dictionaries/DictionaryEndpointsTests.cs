using System.Net;
using Recruitment.Api.IntegrationTests.Infrastructure;
using Recruitment.Domain.Users;
using Shouldly;

namespace Recruitment.Api.IntegrationTests.Dictionaries;

public sealed class DictionaryEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static string Items(string type) => $"/api/v1/dictionaries/{type}/items";

    [Fact]
    public async Task SeededDictionaries_AreAvailableToAnyUser()
    {
        var teamLead = await factory.LoginAs(Role.TeamLead);

        var levels = await teamLead.GetJson(Items("Level"));

        levels.EnumerateArray().Select(l => l.GetProperty("name").GetString())
            .ShouldBe(["L1", "L2", "L3", "L4"]);
    }

    [Fact]
    public async Task Login_WithNewDirectoryDepartment_AddsItToDepartments()
    {
        var department = $"Birim {Guid.NewGuid():N}";

        var user = await factory.LoginAs(department, Role.TeamLead);
        await factory.LoginAs(department, Role.TeamLead);

        var matches = (await user.GetJson(Items("Department"))).EnumerateArray()
            .Count(d => d.GetProperty("name").GetString() == department);
        matches.ShouldBe(1);
    }

    [Fact]
    public async Task Create_ByManager_AddsItemAtEnd_AndRejectsDuplicateName()
    {
        var hrManager = await factory.LoginAs(Role.HrManager);
        var name = $"Kaynak {Guid.NewGuid():N}";

        var id = await hrManager.Create(Items("CandidateSource"), new { name = $"  {name} " });
        var duplicate = await hrManager.Post(Items("CandidateSource"), new { name = name.ToUpperInvariant() });

        var created = (await hrManager.GetJson(Items("CandidateSource"))).EnumerateArray()
            .Single(i => i.GetProperty("id").GetGuid() == id);
        created.GetProperty("name").GetString().ShouldBe(name);
        created.GetProperty("isActive").GetBoolean().ShouldBeTrue();
        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Create_WithBlankName_Returns400()
    {
        var admin = await factory.LoginAs(Role.SystemAdmin);

        var response = await admin.Post(Items("Room"), new { name = " " });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Json()).GetProperty("errors").GetProperty("name")[0].GetString().ShouldBe("Bir ad yazmalısınız.");
    }

    [Fact]
    public async Task CreateAndUpdate_ByNonManager_Returns403()
    {
        var hrSpecialist = await factory.LoginAs(Role.HrSpecialist);
        var levelId = await hrSpecialist.DictionaryItemId("Level", "L4");

        (await hrSpecialist.Post(Items("Level"), new { name = "Principal" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await hrSpecialist.Put($"/api/v1/dictionary-items/{levelId}", new { name = "X", sortOrder = 1, isActive = false }))
            .StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Update_Deactivating_HidesItemFromSelectionListsOnly()
    {
        var admin = await factory.LoginAs(Role.SystemAdmin);
        var id = await admin.Create(Items("Room"), new { name = $"Oda {Guid.NewGuid():N}" });

        (await admin.Put($"/api/v1/dictionary-items/{id}", new { name = "Toplantı Odası A", sortOrder = 5, isActive = false }))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var selectable = (await admin.GetJson(Items("Room"))).EnumerateArray().Select(i => i.GetProperty("id").GetGuid());
        var all = (await admin.GetJson(Items("Room") + "?includeInactive=true")).EnumerateArray()
            .Single(i => i.GetProperty("id").GetGuid() == id);
        selectable.ShouldNotContain(id);
        all.GetProperty("name").GetString().ShouldBe("Toplantı Odası A");
        all.GetProperty("isActive").GetBoolean().ShouldBeFalse();
    }
}
