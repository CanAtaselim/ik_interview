using Recruitment.Domain.Common;

namespace Recruitment.Domain.Dictionaries;

/// <summary>
/// Yönetim ekranından düzenlenen seçenek listelerinin bir öğesi (birim, seviye, red nedeni…).
/// Kullanılan öğe silinmez, pasifleştirilir; böylece eski kayıtlar adını göstermeye devam eder.
/// </summary>
public sealed class DictionaryItem : Entity
{
    public const int NameMaxLength = 200;

    private DictionaryItem() { }

    public DictionaryType Type { get; private set; }
    public string Name { get; private set; } = default!;
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; }

    public static DictionaryItem Create(DictionaryType type, string name, int sortOrder = 0) =>
        new() { Type = type, Name = Normalize(name), SortOrder = sortOrder, IsActive = true };

    public void Update(string name, int sortOrder, bool isActive)
    {
        Name = Normalize(name);
        SortOrder = sortOrder;
        IsActive = isActive;
    }

    private static string Normalize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Sözlük öğesinin adı boş olamaz.");

        var trimmed = name.Trim();
        if (trimmed.Length > NameMaxLength)
            throw new DomainException($"Sözlük öğesinin adı en fazla {NameMaxLength} karakter olabilir.");

        return trimmed;
    }
}
