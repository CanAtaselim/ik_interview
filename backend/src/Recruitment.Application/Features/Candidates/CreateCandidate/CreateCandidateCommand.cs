using Recruitment.Application.Abstractions;

namespace Recruitment.Application.Features.Candidates.CreateCandidate;

/// <summary>Adayı havuza ekler. Pozisyon gerekmez; ad, soyad ve bir iletişim bilgisi yeterlidir.</summary>
public sealed record CreateCandidateCommand(
    string FirstName,
    string LastName,
    string? Email,
    string? Phone,
    string? LinkedInUrl,
    string? GitHubUrl,
    string? City,
    Guid? SourceId,
    IReadOnlyCollection<string>? Tags,
    string? Notes,
    bool ConfirmDuplicate,
    bool ConfirmBlacklist = false) : ICommand<Guid>, ICandidateFields;

internal sealed class CreateCandidateCommandValidator : CandidateFieldsValidator<CreateCandidateCommand>;
