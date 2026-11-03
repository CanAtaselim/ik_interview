namespace Recruitment.Domain.Candidates;

/// <summary>Mükerrer aday eşleştirmesinde kullanılan iletişim bilgisi biçimleri.</summary>
public static class ContactNormalizer
{
    private const int PhoneMatchDigits = 10;

    /// <summary>Küçük harf, kırpılmış. Boşsa <c>null</c>.</summary>
    public static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    /// <summary>Ad ve soyadı tek biçime getirir: "IŞIL  Çağlayan" → "işil çağlayan".</summary>
    public static string NormalizeName(string? firstName, string? lastName) =>
        string.Join(' ', Common.SearchText.Tokenize($"{firstName} {lastName}"));

    /// <summary>
    /// Telefon tam 10 haneli (alan kodu + numara) olmalı. Başındaki "0" veya "+90" sayılmaz:
    /// "0532 123 45 67" ve "+90 532 123 45 67" geçerli, "532 123 45" geçersizdir.
    /// </summary>
    public static bool IsValidPhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return false;

        var digits = new string(phone.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 12 && digits.StartsWith("90", StringComparison.Ordinal))
            digits = digits[2..];
        else if (digits.Length == 11 && digits[0] == '0')
            digits = digits[1..];

        return digits.Length == PhoneMatchDigits;
    }

    /// <summary>
    /// Yalnızca rakamlar, son 10 hane: "+90 (532) 123 45 67" ve "0532 123 4567" aynı numaradır.
    /// Rakam yoksa <c>null</c>.
    /// </summary>
    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        var digits = new string(phone.Where(char.IsAsciiDigit).ToArray());
        if (digits.Length == 0)
            return null;

        return digits.Length <= PhoneMatchDigits ? digits : digits[^PhoneMatchDigits..];
    }
}
