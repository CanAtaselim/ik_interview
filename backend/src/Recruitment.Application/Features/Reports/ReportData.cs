using Recruitment.Application.Common;
using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Features.Dictionaries;

namespace Recruitment.Application.Features.Reports;

/// <summary>Rapor satırlarında kimlik yerine ad yazmak için toplu çözümleme; bulunamayan "—" olur.</summary>
internal sealed class ReportLookups
{
    private readonly IReadOnlyDictionary<Guid, string> _users;
    private readonly IReadOnlyDictionary<Guid, string> _items;
    private readonly IReadOnlyDictionary<Guid, string> _stages;
    private readonly IReadOnlyDictionary<Guid, string> _candidates;
    private readonly IReadOnlyDictionary<Guid, (string Title, Guid DepartmentId, Guid? LevelId)> _positions;

    private ReportLookups(
        IReadOnlyDictionary<Guid, string> users,
        IReadOnlyDictionary<Guid, string> items,
        IReadOnlyDictionary<Guid, string> stages,
        IReadOnlyDictionary<Guid, string> candidates,
        IReadOnlyDictionary<Guid, (string Title, Guid DepartmentId, Guid? LevelId)> positions)
    {
        _candidates = candidates;
        _users = users;
        _items = items;
        _stages = stages;
        _positions = positions;
    }

    public string User(Guid? id) => id is { } key ? _users.GetValueOrDefault(key, ReportFormatter.Empty) : ReportFormatter.Empty;

    public string Item(Guid? id) => id is { } key ? _items.GetValueOrDefault(key, ReportFormatter.Empty) : ReportFormatter.Empty;

    public string Candidate(Guid id) => _candidates.GetValueOrDefault(id, ReportFormatter.Empty);

    public string Stage(Guid id) => _stages.GetValueOrDefault(id, ReportFormatter.Empty);

    public string PositionTitle(Guid id) => _positions.TryGetValue(id, out var p) ? p.Title : ReportFormatter.Empty;

    public string PositionDepartment(Guid id) => _positions.TryGetValue(id, out var p) ? Item(p.DepartmentId) : ReportFormatter.Empty;

    public string PositionLevel(Guid id) => _positions.TryGetValue(id, out var p) ? Item(p.LevelId) : ReportFormatter.Empty;

    /// <summary>Verilen kimliklerin kullanıcı, sözlük, pozisyon ve aşama adlarını tek seferde yükler.</summary>
    public static async Task<ReportLookups> Load(
        IAppDbContext db,
        IEnumerable<Guid?> userIds,
        IEnumerable<Guid?> itemIds,
        IEnumerable<Guid> positionIds,
        CancellationToken ct,
        IEnumerable<Guid>? candidateIds = null)
    {
        var users = userIds.OfType<Guid>().Distinct().ToList();
        var userNames = (await db.Users.AsNoTracking()
            .Where(u => users.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.Title })
            .ToListAsync(ct))
            .ToDictionary(u => u.Id, u => UserLabel.Format(u.DisplayName, u.Title));

        var positionKeys = positionIds.Distinct().ToList();
        var positions = await db.Positions.AsNoTracking()
            .Where(p => positionKeys.Contains(p.Id))
            .Select(p => new { p.Id, p.Title, p.DepartmentId, p.LevelId })
            .ToDictionaryAsync(p => p.Id, p => (p.Title, p.DepartmentId, p.LevelId), ct);

        var allItems = itemIds
            .Concat(positions.Values.SelectMany(p => new Guid?[] { p.DepartmentId, p.LevelId }))
            .OfType<Guid>()
            .Distinct()
            .ToList();
        var itemNames = await db.ItemNames(allItems, ct);

        var stages = (await db.PipelineTemplates.AsNoTracking().Include(t => t.Stages).ToListAsync(ct))
            .SelectMany(t => t.Stages)
            .ToDictionary(s => s.Id, s => s.Name);

        var candidateKeys = (candidateIds ?? []).Distinct().ToList();
        var candidateNames = candidateKeys.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.Candidates.AsNoTracking()
                .Where(c => candidateKeys.Contains(c.Id))
                .Select(c => new { c.Id, c.FirstName, c.LastName })
                .ToDictionaryAsync(c => c.Id, c => $"{c.FirstName} {c.LastName}", ct);

        return new ReportLookups(userNames, itemNames, stages, candidateNames, positions);
    }
}
