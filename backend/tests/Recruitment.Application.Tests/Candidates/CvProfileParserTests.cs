using Recruitment.Application.Features.Candidates.ExtractCvProfile;
using Shouldly;

namespace Recruitment.Application.Tests.Candidates;

public sealed class CvProfileParserTests
{
    [Fact]
    public void Parse_TypicalTurkishCv_ReadsNameContactAndLinks()
    {
        string[] lines =
        [
            "Ayşe Nur Yılmaz",
            "Kıdemli Yazılım Geliştirici",
            "E-posta: Ayse.Yilmaz@Ornek.com",
            "Telefon: +90 (532) 123 45 67",
            "linkedin.com/in/ayse-yilmaz-123/",
            "https://github.com/ayseyilmaz/proje-adi",
            "Deneyim",
        ];

        var profile = CvProfileParser.Parse(lines);

        profile.FirstName.ShouldBe("Ayşe Nur");
        profile.LastName.ShouldBe("Yılmaz");
        profile.Email.ShouldBe("ayse.yilmaz@ornek.com");
        profile.Phone.ShouldBe("0532 123 45 67");
        profile.LinkedInUrl.ShouldBe("https://www.linkedin.com/in/ayse-yilmaz-123");
        profile.GitHubUrl.ShouldBe("https://github.com/ayseyilmaz");
    }

    [Fact]
    public void Parse_AllCapsName_IsConvertedWithTurkishRules()
    {
        var profile = CvProfileParser.Parse(["ÖZGEÇMİŞ", "IŞIL ÇAĞLAYAN", "isil@ornek.com"]);

        profile.FirstName.ShouldBe("Işıl");
        profile.LastName.ShouldBe("Çağlayan");
    }

    [Theory]
    [InlineData("0532 123 45 67", "0532 123 45 67")]
    [InlineData("05321234567", "0532 123 45 67")]
    [InlineData("+90 532 123 4567", "0532 123 45 67")]
    [InlineData("(0532) 123-45-67", "0532 123 45 67")]
    [InlineData("90 532 123 45 67", "0532 123 45 67")]
    [InlineData("Tel: 0 212 555 11 22", "0212 555 11 22")]
    public void Parse_Phone_IsNormalized(string line, string expected) =>
        CvProfileParser.Parse([line]).Phone.ShouldBe(expected);

    [Theory]
    [InlineData("2018 - 2021 İstanbul")]
    [InlineData("TC 12345678901 numaralı")]
    [InlineData("No: 1234567")]
    public void Parse_NumbersThatAreNotPhones_AreIgnored(string line) =>
        CvProfileParser.Parse([line]).Phone.ShouldBeNull();

    [Theory]
    [InlineData("Özgeçmiş")]
    [InlineData("Kişisel Bilgiler")]
    [InlineData("Yazılım Geliştirme Uzmanı")]
    [InlineData("curriculum vitae")]
    [InlineData("Sayfa 1 / 2")]
    [InlineData("ali veli")]
    [InlineData("Ali")]
    public void Parse_HeadingsAndTitles_AreNotTakenAsName(string line)
    {
        var profile = CvProfileParser.Parse([line]);

        profile.FirstName.ShouldBeNull();
        profile.LastName.ShouldBeNull();
    }

    [Fact]
    public void Parse_NameDetectionOff_StillReadsContactDetails()
    {
        var profile = CvProfileParser.Parse(["Ali Veli", "ali@ornek.com"], detectName: false);

        profile.FirstName.ShouldBeNull();
        profile.Email.ShouldBe("ali@ornek.com");
    }

    [Fact]
    public void Parse_NothingReadable_ReturnsEmpty()
    {
        CvProfileParser.Parse([]).ShouldBe(CvProfile.Empty);
        CvProfileParser.Parse(["lorem ipsum dolor sit amet"]).ShouldBe(CvProfile.Empty);
    }

    [Theory]
    [InlineData("Adres: Moda Mah. Kadıköy / İstanbul", "İstanbul")]
    [InlineData("Lokasyon: Eskisehir, Turkiye", "Eskişehir")]
    [InlineData("Şehir: IZMIR", "İzmir")]
    [InlineData("Ankara, Türkiye", "Ankara")]
    [InlineData("Diyarbakır", "Diyarbakır")]
    public void Parse_City_IsReadFromAddressOrHeaderLines(string line, string expected) =>
        CvProfileParser.Parse(["Ali Veli", line, "ali@ornek.com"]).City.ShouldBe(expected);

    [Fact]
    public void Parse_City_IgnoresSchoolsEmployersAndBirthplaces()
    {
        string[] lines =
        [
            "Ali Veli",
            "ali@ornek.com",
            "Doğum yeri: Konya",
            "Ankara Üniversitesi Bilgisayar Mühendisliği",
            "Bursa Teknoloji A.Ş. Yazılım Geliştirici",
            "Samsung Electronics",
        ];

        CvProfileParser.Parse(lines).City.ShouldBeNull();
    }

    [Fact]
    public void Parse_City_PrefersTheAddressLabelOverTheHeader()
    {
        string[] lines = ["Ali Veli", "Bursa", "...", "Adres: Çankaya, Ankara"];

        CvProfileParser.Parse(lines).City.ShouldBe("Ankara");
    }

    [Fact]
    public void Parse_LinksGivenAsHyperlinkTargets_AreRead()
    {
        string[] lines = ["Ali Veli", "LinkedIn | GitHub", "https://tr.linkedin.com/in/ali-veli/", "https://github.com/aliveli/", "mailto:ali@ornek.com"];

        var profile = CvProfileParser.Parse(lines);

        profile.LinkedInUrl.ShouldBe("https://www.linkedin.com/in/ali-veli");
        profile.GitHubUrl.ShouldBe("https://github.com/aliveli");
        profile.Email.ShouldBe("ali@ornek.com");
    }

    [Fact]
    public void Parse_KnownSkillsInText_AreFoundWithoutFalseMatches()
    {
        string[] lines =
        [
            "Ayşe Yılmaz",
            "ASP.NET Core ve PostgreSQL ile API geliştirdim. JavaScript, React ve Docker kullandım.",
            "Takım içinde Scrum uyguladım; C# ve Git deneyimim var.",
        ];

        var skills = CvProfileParser.Parse(lines).Skills;

        skills.ShouldBe(["ASP.NET Core", "PostgreSQL", "JavaScript", "React", "Docker", "Scrum", "C#", "Git"], ignoreOrder: true);
        skills.ShouldNotContain("Java");
        skills.ShouldNotContain("SQL");
        skills.ShouldNotContain(".NET");
    }

    [Fact]
    public void Parse_SkillsSection_ReadsItemsAndStopsAtNextHeader()
    {
        string[] lines =
        [
            "Ali Veli",
            "Yetenekler",
            "• Postgres, Redis",
            "- Oracle Forms | Raporlama",
            "Deneyim",
            "Şirket A.Ş. Kıdemli Geliştirici",
        ];

        var skills = CvProfileParser.Parse(lines).Skills;

        skills.ShouldContain("PostgreSQL");
        skills.ShouldContain("Redis");
        skills.ShouldContain("Oracle Forms");
        skills.ShouldContain("Raporlama");
        skills.ShouldNotContain("Şirket A.Ş. Kıdemli Geliştirici");
    }

    [Fact]
    public void Parse_InlineSkillsHeader_ReadsItems()
    {
        var skills = CvProfileParser.Parse(["Skills: Kafka, gRPC; Kubernetes"]).Skills;

        skills.ShouldBe(["Kafka", "gRPC", "Kubernetes"], ignoreOrder: true);
    }

    [Fact]
    public void Parse_MoreThanLimit_KeepsFirstOnes()
    {
        var skills = CvProfileParser.Parse(["Yetenekler: C#, Java, Python, PHP, Ruby, Rust, Scala, Dart, Flutter, React, Angular, Docker, Git, SQL, Redis, Kafka, Linux"]).Skills;

        skills.Count.ShouldBe(15);
        skills[0].ShouldBe("C#");
    }
}
