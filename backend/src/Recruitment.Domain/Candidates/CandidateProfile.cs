namespace Recruitment.Domain.Candidates;

/// <summary>Adayın kimlik ve iletişim bilgileri. Ad, soyad ve en az bir iletişim dışında her şey opsiyoneldir.</summary>
public sealed record CandidateProfile(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? LinkedInUrl,
    string? GitHubUrl,
    string? City,
    Guid? SourceId,
    IReadOnlyCollection<string> Tags,
    string? Notes);
