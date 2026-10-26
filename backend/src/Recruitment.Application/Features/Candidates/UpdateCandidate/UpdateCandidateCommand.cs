using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;

namespace Recruitment.Application.Features.Candidates.UpdateCandidate;

public sealed record UpdateCandidateCommand(
    Guid Id,
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
    bool ConfirmBlacklist = false) : ICommand<Unit>, ICandidateFields;

internal sealed class UpdateCandidateCommandValidator : CandidateFieldsValidator<UpdateCandidateCommand>;
