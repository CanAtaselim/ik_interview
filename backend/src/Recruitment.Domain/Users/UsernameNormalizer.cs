namespace Recruitment.Domain.Users;

public static class UsernameNormalizer
{
    /// <summary>"DOMAIN\Ad.Soyad" ve "ad.soyad@domain" biçimlerini "ad.soyad" haline getirir.</summary>
    public static string Normalize(string? input)
    {
        var value = (input ?? string.Empty).Trim();

        var slash = value.LastIndexOf('\\');
        if (slash >= 0)
            value = value[(slash + 1)..];

        var at = value.IndexOf('@');
        if (at >= 0)
            value = value[..at];

        return value.ToLowerInvariant();
    }
}
