using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Candidates;

namespace Recruitment.Application.Features.Candidates.UpdateScreening;

internal sealed class UpdateScreeningCommandHandler(IAppDbContext db) : ICommandHandler<UpdateScreeningCommand, Unit>
{
    public async Task<Result<Unit>> Handle(UpdateScreeningCommand command, CancellationToken ct)
    {
        var candidate = await db.FindCandidate(command.CandidateId, ct);
        if (candidate is null)
            return CandidateErrors.NotFound;
        if (candidate.Screenings.All(s => s.Id != command.ScreeningId))
            return CandidateErrors.ScreeningNotFound;

        candidate.UpdateScreening(command.ScreeningId, new ScreeningDetails(
            command.Channel,
            command.InterviewedAt,
            [.. (command.Educations ?? []).Select(e => new Education(e.School, e.Department, e.Degree, e.GraduationYear))],
            command.EducationNote,
            [.. (command.Languages ?? []).Select(l => new LanguageSkill(l.Language, l.Level))],
            command.LanguagesNote,
            command.SoftSkills,
            command.TechnicalSkillTags ?? [],
            command.TechnicalSkills,
            command.ExperienceSummary,
            command.Motivation,
            command.TotalExperienceMonths,
            command.FieldExperienceMonths,
            command.ExpectedSalary.ToMoney(),
            command.ExpectedSalaryMax,
            command.NoticePeriodWeeks,
            command.HrComment,
            command.CurrentSalary.ToMoney(),
            command.WorkModel,
            command.LocationProvince,
            command.LocationDistrict,
            command.LocationNote,
            command.OpenToRelocation,
            command.HasTravelRestriction,
            command.MilitaryStatus,
            command.OverallRating));

        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
