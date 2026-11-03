namespace Recruitment.Application.Common;

/// <summary>İşlem yapan kişinin ekranda "Ad Soyad (Unvan)" biçiminde gösterilmesi.</summary>
public static class UserLabel
{
    public static string Format(string displayName, string? title) =>
        string.IsNullOrWhiteSpace(title) ? displayName : $"{displayName} ({title})";
}
