using Microsoft.Extensions.Time.Testing;
using Recruitment.Domain.Candidates;
using Recruitment.Domain.Candidates.Events;
using Recruitment.Domain.Common;
using Shouldly;

namespace Recruitment.Domain.Tests.Candidates;

public sealed class CandidateTests
{
    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 10, 9, 9, 0, 0, TimeSpan.Zero));
    private readonly Guid _hrUserId = Guid.CreateVersion7();

    private static CandidateProfile Profile(
        string firstName = "Işıl",
        string lastName = "Çağlayan",
        string? email = "Isil.Caglayan@Ornek.com",
        string? phone = "+90 532 123 45 67",
        params string[] tags) =>
        new(firstName, lastName, email, phone, null, null, "İzmir", null, tags, "Referansla geldi.");

    private static ScreeningDetails Details(
        int? rating = null,
        int? totalMonths = null,
        int? noticeWeeks = null,
        string? hrComment = null,
        string? technicalSkills = null,
        IReadOnlyList<Education>? educations = null,
        IReadOnlyList<LanguageSkill>? languages = null,
        params string[] technicalTags) =>
        new(null, null, educations ?? [], null, languages ?? [], null, null, technicalTags, technicalSkills, null, null,
            totalMonths, null, null, null, noticeWeeks, hrComment, null, null, null, null, null, null, null, null, rating);

    private Candidate NewCandidate(params string[] tags)
    {
        var candidate = Candidate.Create(Profile(tags: tags), _hrUserId, despiteDuplicate: false, _clock);
        candidate.ClearDomainEvents();
        return candidate;
    }

    [Fact]
    public void Create_WithNameAndOneContact_Succeeds()
    {
        var candidate = Candidate.Create(
            new CandidateProfile(" Ali ", " Veli ", null, "0532 123 45 67", null, null, null, null, [], null),
            _hrUserId, despiteDuplicate: false, _clock);

        candidate.FullName.ShouldBe("Ali Veli");
        candidate.Email.ShouldBeNull();
        candidate.NormalizedPhone.ShouldBe("5321234567");
        // Yeni aday her zaman havuza girer.
        candidate.InTalentPool.ShouldBeTrue();
        candidate.DomainEvents.ShouldHaveSingleItem()
            .ShouldBe(new CandidateCreated(candidate.Id, _hrUserId, _clock.GetUtcNow(), DespiteDuplicate: false));
    }

    [Theory]
    [InlineData("", "Veli", "a@b.com", null)]
    [InlineData("Ali", " ", "a@b.com", null)]
    [InlineData("Ali", "Veli", null, null)]
    [InlineData("Ali", "Veli", " ", "")]
    public void Create_WithoutNameOrAnyContact_Throws(string firstName, string lastName, string? email, string? phone)
    {
        Should.Throw<DomainException>(() =>
            Candidate.Create(Profile(firstName, lastName, email, phone), _hrUserId, false, _clock));
    }

    [Fact]
    public void Create_NormalizesContactsAndDeduplicatesTags()
    {
        var candidate = NewCandidate(" React ", "react", "TypeScript", " ");

        candidate.Email.ShouldBe("Isil.Caglayan@Ornek.com");
        candidate.NormalizedEmail.ShouldBe("isil.caglayan@ornek.com");
        candidate.NormalizedPhone.ShouldBe("5321234567");
        candidate.Tags.ShouldBe(["React", "TypeScript"]);
    }

    [Fact]
    public void SearchText_ContainsNameContactCityAndTags_ButNotNotes()
    {
        var words = NewCandidate("React").SearchText.Split(' ');

        words.ShouldContain("işil");
        words.ShouldContain("çağlayan");
        words.ShouldContain("ornek");
        words.ShouldContain("5321234567");
        words.ShouldContain("izmir");
        words.ShouldContain("react");
        words.ShouldNotContain("referansla");
    }

    [Fact]
    public void Create_DespiteDuplicate_RecordsTheDecision()
    {
        var candidate = Candidate.Create(Profile(), _hrUserId, despiteDuplicate: true, _clock);

        candidate.DuplicateAcknowledged.ShouldBeTrue();
        candidate.DomainEvents.OfType<CandidateCreated>().Single().DespiteDuplicate.ShouldBeTrue();
    }

    [Fact]
    public void UpdateProfile_ChangesFieldsAndKeepsEarlierDuplicateAcknowledgement()
    {
        var candidate = Candidate.Create(Profile(), _hrUserId, despiteDuplicate: true, _clock);

        candidate.UpdateProfile(Profile(lastName: "Demir", email: null), despiteDuplicate: false);

        candidate.LastName.ShouldBe("Demir");
        candidate.NormalizedEmail.ShouldBeNull();
        candidate.DuplicateAcknowledged.ShouldBeTrue();
        candidate.SearchText.ShouldContain("demir");
        candidate.SearchText.ShouldNotContain("ornek");
    }

    [Fact]
    public void SetTalentPool_WhenChanged_RaisesEventOnce()
    {
        var candidate = NewCandidate();

        // Yeni aday zaten havuzdadır; ancak değişiklik varsa olay üretilir.
        candidate.SetTalentPool(true, _hrUserId, _clock);
        candidate.SetTalentPool(false, _hrUserId, _clock);
        candidate.SetTalentPool(false, _hrUserId, _clock);

        candidate.InTalentPool.ShouldBeFalse();
        candidate.DomainEvents.ShouldHaveSingleItem()
            .ShouldBe(new CandidateTalentPoolChanged(candidate.Id, false, _hrUserId, _clock.GetUtcNow()));
    }

    [Fact]
    public void AddDocument_AddsItAndRaisesEvent()
    {
        var candidate = NewCandidate();

        var document = candidate.AddDocument(" cv.pdf ", "application/pdf", 1024, "2026/10/abc.pdf", null, _hrUserId, _clock);

        candidate.Documents.ShouldHaveSingleItem().ShouldBe(document);
        document.FileName.ShouldBe("cv.pdf");
        document.UploadedAt.ShouldBe(_clock.GetUtcNow());
        document.TypeId.ShouldBeNull();
        candidate.DomainEvents.ShouldHaveSingleItem()
            .ShouldBe(new CandidateDocumentAdded(candidate.Id, document.Id, "cv.pdf", _hrUserId, _clock.GetUtcNow()));
    }

    [Theory]
    [InlineData("", 10)]
    [InlineData("cv.pdf", 0)]
    public void AddDocument_WithBlankNameOrEmptyFile_Throws(string fileName, long size)
    {
        Should.Throw<DomainException>(() =>
            NewCandidate().AddDocument(fileName, "application/pdf", size, "key", null, _hrUserId, _clock));
    }

    [Fact]
    public void SetDocumentText_MakesContentSearchable_AndRemoveDocumentTakesItBack()
    {
        var candidate = NewCandidate();
        var document = candidate.AddDocument("cv.pdf", "application/pdf", 1024, "key", null, _hrUserId, _clock);

        candidate.SetDocumentText(document.Id, "Kubernetes ve Terraform deneyimi");
        candidate.SearchText.ShouldContain("kubernetes");

        candidate.RemoveDocument(document.Id);
        candidate.Documents.ShouldBeEmpty();
        candidate.SearchText.ShouldNotContain("kubernetes");
    }

    [Fact]
    public void SetDocumentText_WithVeryLongText_KeepsOnlyTheLimit()
    {
        var candidate = NewCandidate();
        var document = candidate.AddDocument("cv.pdf", "application/pdf", 1024, "key", null, _hrUserId, _clock);

        candidate.SetDocumentText(document.Id, new string('a', CandidateDocument.ExtractedTextMaxLength + 500));

        document.ExtractedText!.Length.ShouldBe(CandidateDocument.ExtractedTextMaxLength);
    }

    [Fact]
    public void RemoveDocument_WhenUnknown_Throws()
    {
        Should.Throw<DomainException>(() => NewCandidate().RemoveDocument(Guid.CreateVersion7()));
    }

    [Fact]
    public void StartScreening_CreatesEmptyRecordForInterviewer()
    {
        var candidate = NewCandidate();

        var screening = candidate.StartScreening(_hrUserId, _clock);

        candidate.Screenings.ShouldHaveSingleItem().ShouldBe(screening);
        screening.InterviewerUserId.ShouldBe(_hrUserId);
        screening.OverallRating.ShouldBeNull();
        candidate.DomainEvents.ShouldHaveSingleItem()
            .ShouldBe(new ScreeningStarted(candidate.Id, screening.Id, _hrUserId, _clock.GetUtcNow()));
    }

    [Fact]
    public void UpdateScreening_WithAllFieldsEmpty_IsValid()
    {
        var candidate = NewCandidate();
        var screening = candidate.StartScreening(_hrUserId, _clock);

        Should.NotThrow(() => candidate.UpdateScreening(screening.Id, Details()));
    }

    [Fact]
    public void UpdateScreening_StoresValuesAndDropsBlankRows()
    {
        var candidate = NewCandidate();
        var screening = candidate.StartScreening(_hrUserId, _clock);

        candidate.UpdateScreening(screening.Id, Details(
            rating: 4,
            totalMonths: 90,
            noticeWeeks: 4,
            educations: [new Education("ODTÜ", "Bilgisayar Mühendisliği", "Lisans", 2018), new Education(" ", null, null, null)],
            languages: [new LanguageSkill("İngilizce", LanguageLevel.Advanced), new LanguageSkill("", LanguageLevel.Beginner)],
            technicalTags: ["Go", "go", " Kafka "]));

        screening.OverallRating.ShouldBe(4);
        screening.TotalExperienceMonths.ShouldBe(90);
        screening.NoticePeriodWeeks.ShouldBe(4);
        screening.Educations.ShouldHaveSingleItem().School.ShouldBe("ODTÜ");
        screening.Languages.ShouldHaveSingleItem().Language.ShouldBe("İngilizce");
        screening.TechnicalSkillTags.ShouldBe(["Go", "Kafka"]);
    }

    [Fact]
    public void UpdateScreening_MakesSkillsSearchable_ButNotHrComment()
    {
        var candidate = NewCandidate();
        var screening = candidate.StartScreening(_hrUserId, _clock);

        candidate.UpdateScreening(screening.Id, Details(
            hrComment: "Ücret beklentisi yüksek, pazarlık gerekebilir",
            technicalSkills: "Mikroservis mimarisi",
            technicalTags: ["Kafka"]));

        candidate.SearchText.ShouldContain("kafka");
        candidate.SearchText.ShouldContain("mikroservis");
        candidate.SearchText.ShouldNotContain("pazarlik");
    }

    [Theory]
    [InlineData(0, null, null)]
    [InlineData(6, null, null)]
    [InlineData(null, -1, null)]
    [InlineData(null, 841, null)]
    [InlineData(null, null, -1)]
    public void UpdateScreening_WithOutOfRangeValues_Throws(int? rating, int? totalMonths, int? noticeWeeks)
    {
        var candidate = NewCandidate();
        var screening = candidate.StartScreening(_hrUserId, _clock);

        Should.Throw<DomainException>(() => candidate.UpdateScreening(
            screening.Id, Details(rating, totalMonths, noticeWeeks)));
    }

    [Fact]
    public void UpdateScreening_StoresSalaryRange_AndRejectsInvalidRanges()
    {
        var candidate = NewCandidate();
        var screening = candidate.StartScreening(_hrUserId, _clock);

        candidate.UpdateScreening(screening.Id, Details() with { ExpectedSalary = new Money(100_000), ExpectedSalaryMax = 130_000 });
        screening.ExpectedSalary!.Amount.ShouldBe(100_000);
        screening.ExpectedSalaryMax.ShouldBe(130_000);

        // Tek değer de geçerlidir.
        candidate.UpdateScreening(screening.Id, Details() with { ExpectedSalary = new Money(100_000) });
        screening.ExpectedSalaryMax.ShouldBeNull();

        Should.Throw<DomainException>(() => candidate.UpdateScreening(
            screening.Id, Details() with { ExpectedSalary = new Money(100_000), ExpectedSalaryMax = 90_000 }));
        Should.Throw<DomainException>(() => candidate.UpdateScreening(
            screening.Id, Details() with { ExpectedSalaryMax = 90_000 }));
    }

    [Fact]
    public void UpdateScreening_StoresLocationAndNotes_AndDistrictNeedsProvince()
    {
        var candidate = NewCandidate();
        var screening = candidate.StartScreening(_hrUserId, _clock);

        candidate.UpdateScreening(screening.Id, Details() with
        {
            LocationProvince = " İstanbul ", LocationDistrict = "Kadıköy", LocationNote = "Anadolu yakasında",
            HasTravelRestriction = false, EducationNote = "Yüksek lisans devam ediyor", LanguagesNote = "Teknik İngilizce çok iyi",
        });

        screening.LocationProvince.ShouldBe("İstanbul");
        screening.LocationDistrict.ShouldBe("Kadıköy");
        screening.HasTravelRestriction.ShouldBe(false);
        candidate.SearchText.ShouldContain("kadiköy");
        candidate.SearchText.ShouldContain("yüksek lisans");

        Should.Throw<DomainException>(() => candidate.UpdateScreening(
            screening.Id, Details() with { LocationProvince = null, LocationDistrict = "Kadıköy" }));
    }

    [Fact]
    public void RemoveScreening_RemovesRecord_RaisesEventAndDropsItsSearchText()
    {
        var candidate = NewCandidate();
        var screening = candidate.StartScreening(_hrUserId, _clock);
        candidate.UpdateScreening(screening.Id, Details(technicalTags: ["Kafka"]));
        candidate.ClearDomainEvents();

        candidate.RemoveScreening(screening.Id, _hrUserId, _clock);

        candidate.Screenings.ShouldBeEmpty();
        candidate.SearchText.ShouldNotContain("kafka");
        candidate.DomainEvents.ShouldHaveSingleItem()
            .ShouldBe(new ScreeningDeleted(candidate.Id, screening.Id, _hrUserId, _clock.GetUtcNow()));
        Should.Throw<DomainException>(() => candidate.RemoveScreening(screening.Id, _hrUserId, _clock));
    }

    [Fact]
    public void UpdateScreening_WhenUnknown_Throws()
    {
        Should.Throw<DomainException>(() => NewCandidate().UpdateScreening(Guid.CreateVersion7(), Details()));
    }
}
