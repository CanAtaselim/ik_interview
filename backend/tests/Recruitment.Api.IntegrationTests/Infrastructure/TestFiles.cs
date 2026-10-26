using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;

namespace Recruitment.Api.IntegrationTests.Infrastructure;

/// <summary>Testler için bellekte üretilen gerçek (geçerli) PDF ve DOCX dosyaları.</summary>
internal static class TestFiles
{
    /// <summary>İçinde verilen metin (yalnızca ASCII) yazan tek sayfalık geçerli bir PDF.</summary>
    public static byte[] Pdf(string text)
    {
        var content = $"BT /F1 12 Tf 72 720 Td ({text}) Tj ET";
        string[] objects =
        [
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
            $"<< /Length {content.Length} >>\nstream\n{content}\nendstream",
            "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
        ];

        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int>();
        for (var i = 0; i < objects.Length; i++)
        {
            offsets.Add(pdf.Length);
            pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }

        var xrefOffset = pdf.Length;
        pdf.Append($"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
        foreach (var offset in offsets)
            pdf.Append($"{offset:D10} 00000 n \n");
        pdf.Append($"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xrefOffset}\n%%EOF");

        return Encoding.ASCII.GetBytes(pdf.ToString());
    }

    /// <summary>Her paragrafında verilen metinlerden biri olan DOCX.</summary>
    public static byte[] Docx(params string[] paragraphs)
    {
        var body = string.Concat(paragraphs.Select(p => $"<w:p><w:r><w:t>{p}</w:t></w:r></w:p>"));
        var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                  "<w:document xmlns:w=\"http://schemas.openxmlformats.org/wordprocessingml/2006/main\">" +
                  $"<w:body>{body}</w:body></w:document>";

        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var entry = archive.CreateEntry("word/document.xml").Open();
            entry.Write(Encoding.UTF8.GetBytes(xml));
        }

        return buffer.ToArray();
    }

    public static MultipartFormDataContent AsUpload(byte[] content, string fileName)
    {
        var file = new ByteArrayContent(content);
        file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");

        return new MultipartFormDataContent { { file, "file", fileName } };
    }
}
