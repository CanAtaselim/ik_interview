namespace Recruitment.Domain.Users;

/// <summary>
/// Kullanıcının kişisel arayüz tercihi (ör. dashboard kart sırası). Anahtar başına tek kayıt tutulur;
/// değer JSON metnidir ve yapısını ilgili özellik belirler.
/// </summary>
public sealed class UserPreference
{
    public const int KeyMaxLength = 100;
    public const int ValueMaxLength = 20_000;

    private UserPreference() { }

    public Guid UserId { get; private set; }
    public string Key { get; private set; } = default!;
    public string Value { get; private set; } = default!;
    public DateTimeOffset UpdatedAt { get; private set; }

    public static UserPreference Create(Guid userId, string key, string value, DateTimeOffset now) =>
        new() { UserId = userId, Key = key, Value = value, UpdatedAt = now };

    public void Update(string value, DateTimeOffset now)
    {
        Value = value;
        UpdatedAt = now;
    }
}
