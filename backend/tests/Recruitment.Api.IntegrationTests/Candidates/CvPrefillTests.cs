using System.Net;
using System.Text;
using System.Text.Json;
using Recruitment.Api.IntegrationTests.Infrastructure;
using Recruitment.Domain.Users;
using Shouldly;

namespace Recruitment.Api.IntegrationTests.Candidates;

/// <summary>CV'den aday ekleme formunu önceden doldurma: bulunanı alır, bulunamayanı sessizce boş bırakır.</summary>
public sealed class CvPrefillTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string Url = "/api/v1/candidates/cv-prefill";

    private Task<ApiUser> Hr() => factory.LoginAs(Role.HrSpecialist);

    private static string? Value(JsonElement profile, string name) =>
        profile.GetProperty(name).ValueKind == JsonValueKind.Null ? null : profile.GetProperty(name).GetString();

    [Fact]
    public async Task Docx_ReadsNameEmailPhoneAndLinks_AndStoresNothing()
    {
        var hr = await Hr();
        var docx = TestFiles.Docx(
            "Zeynep Arslan",
            "Backend Geliştirici",
            "zeynep.arslan@ornek.com | 0533 111 22 33",
            "https://www.linkedin.com/in/zeynep-arslan",
            "github.com/zeynepa");

        var response = await hr.Client.PostAsync(Url, TestFiles.AsUpload(docx, "cv.docx"));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var profile = await response.Json();
        Value(profile, "firstName").ShouldBe("Zeynep");
        Value(profile, "lastName").ShouldBe("Arslan");
        Value(profile, "email").ShouldBe("zeynep.arslan@ornek.com");
        Value(profile, "phone").ShouldBe("0533 111 22 33");
        Value(profile, "linkedInUrl").ShouldBe("https://www.linkedin.com/in/zeynep-arslan");
        Value(profile, "gitHubUrl").ShouldBe("https://github.com/zeynepa");
    }

    [Fact]
    public async Task Docx_ReadsLinksBehindHyperlinkTexts_AndTheCity()
    {
        var hr = await Hr();
        using var buffer = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(buffer, System.IO.Compression.ZipArchiveMode.Create, leaveOpen: true))
        {
            const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
            Write(archive, "word/document.xml",
                $"<w:document xmlns:w=\"{W}\"><w:body><w:p><w:r><w:t>Can Öztürk</w:t></w:r></w:p>" +
                "<w:p><w:r><w:t>Bursa · can@ornek.com</w:t></w:r></w:p>" +
                "<w:p><w:r><w:t>LinkedIn | GitHub</w:t></w:r></w:p></w:body></w:document>");
            Write(archive, "word/_rels/document.xml.rels",
                "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                "<Relationship Id=\"r1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink\" Target=\"https://www.linkedin.com/in/can-ozturk\" TargetMode=\"External\"/>" +
                "<Relationship Id=\"r2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink\" Target=\"https://github.com/canoz\" TargetMode=\"External\"/>" +
                "<Relationship Id=\"r3\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
                "</Relationships>");
        }

        var profile = await (await hr.Client.PostAsync(Url, TestFiles.AsUpload(buffer.ToArray(), "cv.docx"))).Json();

        Value(profile, "linkedInUrl").ShouldBe("https://www.linkedin.com/in/can-ozturk");
        Value(profile, "gitHubUrl").ShouldBe("https://github.com/canoz");
        Value(profile, "city").ShouldBe("Bursa");
        Value(profile, "firstName").ShouldBe("Can");
    }

    [Fact]
    public async Task Pdf_ReadsLinkAnnotationsBehindVisibleLabels()
    {
        var hr = await Hr();
        var pdf = PdfWithLinks(
            ["Deniz Kaya", "Izmir", "LinkedIn", "GitHub"],
            ["https://www.linkedin.com/in/deniz-kaya", "https://github.com/denizk"]);

        var profile = await (await hr.Client.PostAsync(Url, TestFiles.AsUpload(pdf, "cv.pdf"))).Json();

        Value(profile, "linkedInUrl").ShouldBe("https://www.linkedin.com/in/deniz-kaya");
        Value(profile, "gitHubUrl").ShouldBe("https://github.com/denizk");
        Value(profile, "city").ShouldBe("İzmir");
        Value(profile, "firstName").ShouldBe("Deniz");
    }

    private static void Write(System.IO.Compression.ZipArchive archive, string name, string content)
    {
        using var entry = archive.CreateEntry(name).Open();
        entry.Write(Encoding.UTF8.GetBytes("<?xml version=\"1.0\" encoding=\"UTF-8\"?>" + content));
    }

    /// <summary>Her satırı ayrı yazan, son satırlarına bağlantı açıklaması (link annotation) koyan PDF.</summary>
    private static byte[] PdfWithLinks(string[] lines, string[] uris)
    {
        var content = "BT /F1 14 Tf 72 740 Td 20 TL " + string.Join(" ", lines.Select(l => $"({l}) Tj T*")) + " ET";
        var firstLinkY = 740 - 20 * (lines.Length - uris.Length) - 4;
        var annotationRefs = string.Join(" ", uris.Select((_, i) => $"{6 + i} 0 R"));
        var objects = new List<string>
        {
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> /Annots [{annotationRefs}] >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        };
        objects.AddRange(uris.Select((uri, i) =>
            $"<< /Type /Annot /Subtype /Link /Rect [72 {firstLinkY - 20 * i} 200 {firstLinkY - 20 * i + 16}] /Border [0 0 0] /A << /S /URI /URI ({uri}) >> >>"));

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Count; i++)
        {
            offsets.Add(pdf.Length);
            pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xref = pdf.Length;
        pdf.Append($"xref\n0 {objects.Count + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            pdf.Append($"{offset:D10} 00000 n \n");
        pdf.Append($"trailer\n<< /Size {objects.Count + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF");
        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    [Fact]
    public async Task Pdf_ReadsTheContactDetailsItCanFind()
    {
        var hr = await Hr();
        var pdf = TestFiles.Pdf("Iletisim ali.veli@ornek.com 0532 987 65 43");

        var profile = await (await hr.Client.PostAsync(Url, TestFiles.AsUpload(pdf, "cv.pdf"))).Json();

        Value(profile, "email").ShouldBe("ali.veli@ornek.com");
        Value(profile, "phone").ShouldBe("0532 987 65 43");
    }

    [Fact]
    public async Task LegacyDoc_ReadsEmailAndPhone_ButDoesNotGuessTheName()
    {
        var hr = await Hr();
        byte[] header = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0x00, 0x00];
        var body = Encoding.Latin1.GetBytes("\0\0Mehmet Kaya\0\0mehmet.kaya@ornek.com\0\0Tel 0544 222 33 44\0\0");
        var doc = header.Concat(body).ToArray();

        var profile = await (await hr.Client.PostAsync(Url, TestFiles.AsUpload(doc, "cv.doc"))).Json();

        Value(profile, "email").ShouldBe("mehmet.kaya@ornek.com");
        Value(profile, "phone").ShouldBe("0544 222 33 44");
        Value(profile, "firstName").ShouldBeNull();
    }

    [Theory]
    [InlineData("bozuk.pdf", "%PDF-1.7 bozuk icerik")]
    [InlineData("virus.exe", "MZ zararli")]
    [InlineData("sahte.docx", "docx degil")]
    public async Task UnreadableOrInvalidFile_ReturnsEmptyProfileWithoutError(string fileName, string content)
    {
        var hr = await Hr();

        var response = await hr.Client.PostAsync(Url, TestFiles.AsUpload(Encoding.ASCII.GetBytes(content), fileName));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var profile = await response.Json();
        foreach (var field in new[] { "firstName", "lastName", "email", "phone", "linkedInUrl", "gitHubUrl" })
            Value(profile, field).ShouldBeNull(field);
    }

    [Fact]
    public async Task Anonymous_IsRejected()
    {
        var anonymous = await factory.CreateApiClient().PostAsync(Url, TestFiles.AsUpload(TestFiles.Pdf("x"), "cv.pdf"));

        anonymous.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
