using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Recruitment.Api.IntegrationTests.Infrastructure;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Features.Blacklist.ExpireBlacklistEntries;
using Recruitment.Domain.Audit;
using Recruitment.Domain.Users;
using Recruitment.Infrastructure.Persistence;
using Shouldly;

namespace Recruitment.Api.IntegrationTests.Blacklist;

public sealed class BlacklistEndpointsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/v1/blacklist-entries";

    // Soyadlar birbirine benzemesin diye uzun ve geniş alfabeden üretilir; yoksa testlerin
    // adayları arada bir "olası kara liste eşleşmesi" sayılıp başka testleri bozar.
    private static string Letters() =>
        new([.. Enumerable.Range(0, 14).Select(_ => (char)('a' + Random.Shared.Next(26)))]);

    private static string RandomPhone() => "0533" + Random.Shared.Next(1_000_000, 9_999_999);

    private sealed record Person(Guid Id, string FirstName, string LastName, string Email, string Phone);

    private static async Task<Person> CreateCandidate(ApiUser hr, string firstName = "Mehmet")
    {
        var person = new Person(Guid.Empty, firstName, $"Kara{Letters()}", $"{Guid.NewGuid():N}@ornek.com", RandomPhone());
        var id = await hr.Create("/api/v1/candidates", new
        {
            firstName = person.FirstName, lastName = person.LastName, email = person.Email, phone = person.Phone,
        });
        return person with { Id = id };
    }

    private static async Task<object> EntryBody(ApiUser hr, Guid candidateId, string? expiresAt = null) => new
    {
        candidateId,
        reasonId = await hr.DictionaryItemId("BlacklistReason", "Mülakata haber vermeden gelmedi"),
        description = "İki kez randevuya gelmedi, haber de vermedi.",
        expiresAt,
    };

    /// <summary>İK Yöneticisinin doğrudan eklediği, yürürlükteki kayıt.</summary>
    private async Task<(ApiUser Manager, Person Person, Guid EntryId)> ActiveEntry(string firstName = "Mehmet")
    {
        var manager = await factory.LoginAs(Role.HrManager);
        var person = await CreateCandidate(manager, firstName);
        var entryId = await manager.Create(Url, await EntryBody(manager, person.Id));
        return (manager, person, entryId);
    }

    private static async Task<List<JsonElement>> Matches(ApiUser hr, string query) =>
        (await hr.GetJson($"{Url}/matches?{query}")).EnumerateArray().ToList();

    [Fact]
    public async Task Request_BySpecialist_WaitsForManagerApproval()
    {
        var specialist = await factory.LoginAs(Role.HrSpecialist);
        var manager = await factory.LoginAs(Role.HrManager);
        var person = await CreateCandidate(specialist);

        var entryId = await specialist.Create(Url, await EntryBody(specialist, person.Id));

        var pending = await specialist.GetJson($"{Url}/{entryId}");
        pending.GetProperty("status").GetString().ShouldBe("PendingApproval");
        pending.GetProperty("reasonName").GetString().ShouldBe("Mülakata haber vermeden gelmedi");
        pending.GetProperty("description").GetString().ShouldBe("İki kez randevuya gelmedi, haber de vermedi.");
        pending.GetProperty("canApprove").GetBoolean().ShouldBeFalse();
        (await manager.GetJson($"{Url}/{entryId}")).GetProperty("canApprove").GetBoolean().ShouldBeTrue();
        // Onaylanmamış talep eşleşme uyarısı üretmez.
        (await Matches(specialist, $"email={person.Email}")).ShouldBeEmpty();
        (await specialist.GetJson($"/api/v1/candidates/{person.Id}")).GetProperty("blacklist").GetProperty("status")
            .GetString().ShouldBe("PendingApproval");

        (await specialist.Post($"{Url}/{entryId}/approve")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await manager.Post($"{Url}/{entryId}/approve")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var approved = await manager.GetJson($"{Url}/{entryId}");
        approved.GetProperty("status").GetString().ShouldBe("Active");
        approved.GetProperty("approvedByName").GetString().ShouldBe("Test Kullanıcısı");
        (await manager.Post($"{Url}/{entryId}/approve")).StatusCode.ShouldBe(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Request_ByManager_IsActiveImmediately_AndSecondOpenEntryIsRejected()
    {
        var (manager, person, entryId) = await ActiveEntry();

        var again = await manager.Post(Url, await EntryBody(manager, person.Id));

        (await manager.GetJson($"{Url}/{entryId}")).GetProperty("status").GetString().ShouldBe("Active");
        again.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Request_WithoutReasonOrDescription_Returns400()
    {
        var manager = await factory.LoginAs(Role.HrManager);
        var person = await CreateCandidate(manager);

        var response = await manager.Post(Url, new { candidateId = person.Id, description = " " });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var errors = (await response.Json()).GetProperty("errors");
        errors.GetProperty("reasonId")[0].GetString().ShouldBe("Kara liste nedeni seçmelisiniz.");
        errors.GetProperty("description")[0].GetString().ShouldBe("Kara listeye alma gerekçesini yazmalısınız.");
    }

    [Fact]
    public async Task Remove_NeedsManagerAndReason_AndAllowsANewEntryAfterwards()
    {
        var (manager, person, entryId) = await ActiveEntry();
        var specialist = await factory.LoginAs(Role.HrSpecialist);

        (await specialist.Post($"{Url}/{entryId}/remove", new { reason = "Yanlış kişi" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var noReason = await manager.Post($"{Url}/{entryId}/remove", new { reason = " " });
        var removed = await manager.Post($"{Url}/{entryId}/remove", new { reason = "Yanlış kişi eklenmiş." });

        noReason.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await noReason.Json()).GetProperty("errors").GetProperty("reason")[0].GetString()
            .ShouldBe("Kara listeden çıkarma gerekçesini yazmalısınız.");
        removed.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var detail = await manager.GetJson($"{Url}/{entryId}");
        detail.GetProperty("status").GetString().ShouldBe("Removed");
        detail.GetProperty("removalReason").GetString().ShouldBe("Yanlış kişi eklenmiş.");
        detail.GetProperty("canRemove").GetBoolean().ShouldBeFalse();
        (await Matches(manager, $"email={person.Email}")).ShouldBeEmpty();
        (await manager.GetJson($"/api/v1/candidates/{person.Id}")).GetProperty("blacklist").ValueKind.ShouldBe(JsonValueKind.Null);
        (await manager.Post(Url, await EntryBody(manager, person.Id))).StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Matches_AreExactByContact_AndPossibleBySimilarName()
    {
        var (manager, person, entryId) = await ActiveEntry("Mehmet");
        var digits = person.Phone[1..];

        var byEmail = await Matches(manager, $"email={person.Email.ToUpperInvariant()}");
        var byPhone = await Matches(manager, $"phone={Uri.EscapeDataString($"+90 ({digits[..3]}) {digits[3..]}")}");
        var bySameName = await Matches(manager, $"firstName=MEHMET&lastName={person.LastName.ToUpperInvariant()}");
        var bySimilarName = await Matches(manager, $"firstName=Mehmed&lastName={person.LastName}");
        var byOtherName = await Matches(manager, $"firstName=Zeynep&lastName=Aksoy{Letters()}");
        var excludingSelf = await Matches(manager, $"email={person.Email}&excludeCandidateId={person.Id}");

        byEmail.ShouldHaveSingleItem().GetProperty("kind").GetString().ShouldBe("Exact");
        byEmail[0].GetProperty("entryId").GetGuid().ShouldBe(entryId);
        byEmail[0].GetProperty("fullName").GetString().ShouldBe($"Mehmet {person.LastName}");
        byEmail[0].GetProperty("reasonName").GetString().ShouldBe("Mülakata haber vermeden gelmedi");
        byPhone.ShouldHaveSingleItem().GetProperty("kind").GetString().ShouldBe("Exact");
        bySameName.ShouldHaveSingleItem().GetProperty("kind").GetString().ShouldBe("Possible");
        bySimilarName.ShouldHaveSingleItem().GetProperty("kind").GetString().ShouldBe("Possible");
        byOtherName.ShouldBeEmpty();
        excludingSelf.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreatingCandidate_ThatMatchesBlacklist_NeedsConfirmation_WhichIsLogged()
    {
        var (manager, person, entryId) = await ActiveEntry();
        var specialist = await factory.LoginAs(Role.HrSpecialist);
        object Body(bool confirm) => new
        {
            firstName = "Memo", lastName = "Farklı", phone = person.Phone,
            confirmDuplicate = true, confirmBlacklist = confirm,
        };

        var warning = await specialist.Post("/api/v1/candidates", Body(confirm: false));
        var createdId = await specialist.Create("/api/v1/candidates", Body(confirm: true));

        warning.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var problem = await warning.Json();
        problem.GetProperty("code").GetString().ShouldBe("Candidate.BlacklistMatch");
        problem.GetProperty("blacklistMatches").EnumerateArray().Single().GetProperty("entryId").GetGuid().ShouldBe(entryId);

        await using var scope = factory.Services.CreateAsyncScope();
        var log = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AccessLogs
            .SingleAsync(a => a.CandidateId == createdId);
        log.Action.ShouldBe(AccessAction.BlacklistWarningAcknowledged);
        log.UserId.ShouldBe(specialist.Id);
        _ = manager;
    }

    [Fact]
    public async Task Views_AreWrittenToAccessLog()
    {
        var (manager, person, entryId) = await ActiveEntry();

        await manager.GetJson(Url);
        await manager.GetJson($"{Url}/{entryId}");

        await using var scope = factory.Services.CreateAsyncScope();
        var logs = await scope.ServiceProvider.GetRequiredService<AppDbContext>().AccessLogs
            .Where(a => a.UserId == manager.Id)
            .ToListAsync();
        logs.ShouldContain(a => a.Action == AccessAction.BlacklistListViewed && a.CandidateId == null);
        logs.ShouldContain(a => a.Action == AccessAction.BlacklistEntryViewed && a.CandidateId == person.Id);
    }

    [Fact]
    public async Task List_FiltersByStatusAndCandidateName()
    {
        var (manager, person, entryId) = await ActiveEntry("Mehmet");
        var specialist = await factory.LoginAs(Role.HrSpecialist);
        var pendingPerson = await CreateCandidate(specialist, "Ayşe");
        var pendingId = await specialist.Create(Url, await EntryBody(specialist, pendingPerson.Id));

        var byName = await manager.GetJson($"{Url}?search={person.LastName.ToUpperInvariant()}");
        var pending = (await manager.GetJson($"{Url}?status=PendingApproval&pageSize=100")).Ids().ToList();

        byName.Ids().ShouldBe([entryId]);
        var row = byName.GetProperty("items")[0];
        row.GetProperty("candidateName").GetString().ShouldBe($"Mehmet {person.LastName}");
        row.GetProperty("reasonName").GetString().ShouldBe("Mülakata haber vermeden gelmedi");
        row.GetProperty("status").GetString().ShouldBe("Active");
        pending.ShouldContain(pendingId);
        pending.ShouldNotContain(entryId);
    }

    [Fact]
    public async Task Blacklist_IsHiddenFromNonHr_AndSystemAdminCanOnlyView()
    {
        var (manager, person, entryId) = await ActiveEntry();
        var teamLead = await factory.LoginAs(Role.TeamLead);
        var admin = await factory.LoginAs(Role.SystemAdmin);

        (await teamLead.Client.GetAsync(Url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await teamLead.Client.GetAsync($"{Url}/{entryId}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await teamLead.Client.GetAsync($"{Url}/matches?email={person.Email}")).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await admin.GetJson($"{Url}/{entryId}")).GetProperty("canRemove").GetBoolean().ShouldBeFalse();
        (await admin.Post(Url, await EntryBody(manager, person.Id))).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await admin.Post($"{Url}/{entryId}/remove", new { reason = "x" })).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ExpiryJob_EndsEntriesPastTheirDate_AndTheyStopMatching()
    {
        var manager = await factory.LoginAs(Role.HrManager);
        var person = await CreateCandidate(manager);
        var entryId = await manager.Create(
            Url, await EntryBody(manager, person.Id, DateTimeOffset.UtcNow.AddDays(30).ToString("O")));
        var permanent = await ActiveEntry();
        (await Matches(manager, $"email={person.Email}")).ShouldHaveSingleItem();

        await using var scope = factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.ExecuteSqlAsync(
            $"update blacklist_entries set expires_at = now() - interval '1 minute' where id = {entryId}");
        // Günlük iş çalışmadan önce de süresi dolan kayıt eşleşmez.
        (await Matches(manager, $"email={person.Email}")).ShouldBeEmpty();

        var expired = await scope.ServiceProvider.GetRequiredService<IDispatcher>().Send(new ExpireBlacklistEntriesCommand());

        expired.Value.ShouldBeGreaterThanOrEqualTo(1);
        (await manager.GetJson($"{Url}/{entryId}")).GetProperty("status").GetString().ShouldBe("Expired");
        (await manager.GetJson($"{Url}/{permanent.EntryId}")).GetProperty("status").GetString().ShouldBe("Active");
    }
}
