using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.Dictionaries;

namespace Recruitment.Application.Features.Dictionaries;

/// <summary>Bir sözlük öğesinin hangi kayıt türünde kaç kez kullanıldığı.</summary>
public sealed record DictionaryUsageDto(string Area, int Count);

/// <summary>
/// Sözlük öğelerinin kullanım sayıları. Kullanılan öğenin adı değiştirilemez ve öğe silinemez:
/// kullanıcı önce o kayıtları siler veya başka değere çevirir. Kimlikle bağlananlar kimlikten,
/// adı kayıtta metin olarak saklananlar (pozisyon adı, eğitim seviyesi) addan sayılır.
/// </summary>
internal static class DictionaryUsage
{
    /// <summary>Varsayılan CV türü: aday listesindeki CV simgesi adına bakar, bu yüzden değiştirilemez.</summary>
    public static bool IsSystemItem(DictionaryItem item) =>
        item is { Type: DictionaryType.DocumentType } && string.Equals(item.Name, "CV", StringComparison.OrdinalIgnoreCase);

    public static Task<Dictionary<Guid, List<DictionaryUsageDto>>> Of(
        this IAppDbContext db, DictionaryItem item, CancellationToken ct) =>
        db.UsagesOf(item.Type, [new ItemKey(item.Id, item.Name)], ct);

    public static async Task<Dictionary<Guid, List<DictionaryUsageDto>>> UsagesOf(
        this IAppDbContext db, DictionaryType type, IReadOnlyCollection<ItemKey> items, CancellationToken ct)
    {
        var result = items.ToDictionary(i => i.Id, _ => new List<DictionaryUsageDto>());
        if (items.Count == 0)
            return result;

        foreach (var (area, ids) in IdSources(db, type))
        {
            var counts = await ids.Where(id => id != null)
                .GroupBy(id => id!.Value)
                .Select(g => new { Id = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            foreach (var count in counts.Where(c => result.ContainsKey(c.Id)))
                result[count.Id].Add(new DictionaryUsageDto(area, count.Count));
        }

        foreach (var (area, names) in NameSources(db, type))
        {
            var counts = (await names.Where(n => n != null)
                    .GroupBy(n => n!)
                    .Select(g => new { Name = g.Key, Count = g.Count() })
                    .ToListAsync(ct))
                .GroupBy(c => c.Name.Trim().ToLowerInvariant())
                .ToDictionary(g => g.Key, g => g.Sum(c => c.Count));
            foreach (var item in items)
            {
                if (counts.TryGetValue(item.Name.Trim().ToLowerInvariant(), out var count))
                    result[item.Id].Add(new DictionaryUsageDto(area, count));
            }
        }

        return result;
    }

    private static IEnumerable<(string Area, IQueryable<Guid?> Ids)> IdSources(IAppDbContext db, DictionaryType type) =>
        type switch
        {
            DictionaryType.Department =>
            [
                ("kadro talebinde", db.HiringRequests.Select(r => (Guid?)r.DepartmentId)),
                ("pozisyonda", db.Positions.Select(p => (Guid?)p.DepartmentId)),
                ("soruda", db.Questions.Select(q => (Guid?)q.DepartmentId)),
                ("mülakat şablonunda", db.InterviewTemplates.Select(t => (Guid?)t.DepartmentId)),
                ("CV değerlendirmesinde", db.CvReviews.Select(r => r.DepartmentId)),
            ],
            DictionaryType.Level =>
            [
                ("kadro talebinde", db.HiringRequests.Select(r => (Guid?)r.LevelId)),
                ("pozisyonda", db.Positions.Select(p => (Guid?)p.LevelId)),
                ("soruda", db.Questions.Select(q => q.LevelId)),
                ("mülakat şablonunda", db.InterviewTemplates.Select(t => t.LevelId)),
            ],
            DictionaryType.Competency =>
            [
                ("soruda", db.Questions.Select(q => (Guid?)q.CompetencyId)),
                ("değerlendirme puanında", db.Scorecards.SelectMany(s => s.Items).Select(i => i.CompetencyId)),
            ],
            DictionaryType.RejectionReason =>
            [
                ("sonlanan süreçte", db.JobApplications.Select(a => a.RejectionReasonId)),
                ("CV değerlendirmesinde", db.CvReviews.Select(r => r.RejectionReasonId)),
            ],
            DictionaryType.BlacklistReason => [("kara liste kaydında", db.BlacklistEntries.Select(e => (Guid?)e.ReasonId))],
            DictionaryType.CandidateSource => [("adayda", db.Candidates.Select(c => c.SourceId))],
            DictionaryType.InterviewType =>
            [
                ("mülakatta", db.Interviews.Select(i => (Guid?)i.TypeId)),
                ("mülakat şablonunda", db.InterviewTemplates.Select(t => t.InterviewTypeId)),
            ],
            DictionaryType.Room => [("mülakatta", db.Interviews.Select(i => i.RoomId))],
            DictionaryType.DocumentType =>
                [("aday dosyasında", db.Candidates.SelectMany(c => c.Documents).Select(d => d.TypeId))],
            _ => [],
        };

    private static IEnumerable<(string Area, IQueryable<string?> Names)> NameSources(IAppDbContext db, DictionaryType type) =>
        type switch
        {
            DictionaryType.PositionTitle =>
            [
                ("kadro talebinde", db.HiringRequests.Select(r => (string?)r.Title)),
                ("pozisyonda", db.Positions.Select(p => (string?)p.Title)),
            ],
            DictionaryType.EducationLevel =>
            [
                (
                    "ön görüşme kaydında",
                    db.Candidates.SelectMany(c => c.Screenings).SelectMany(s => s.Educations).Select(e => e.Degree)
                ),
            ],
            _ => [],
        };

    public sealed record ItemKey(Guid Id, string Name);
}
