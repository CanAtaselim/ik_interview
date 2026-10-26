using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Positions;
using Recruitment.Domain.Positions.Specifications;
using Recruitment.Domain.Users;

namespace Recruitment.Application.Features.Positions;

internal static class PositionChecks
{
    public static PositionVisibleTo VisibleTo(ICurrentUser user) => new(user.IsHr, user.DepartmentId);

    /// <summary>Sorumlu olarak seçilen kullanıcı aktif bir İK çalışanı değilse alan hatası döner.</summary>
    public static async Task<Error?> FindInvalidResponsibleHr(
        this IAppDbContext db, Guid? userId, CancellationToken ct)
    {
        if (userId is null)
            return null;

        var isHr = await db.Users.AnyAsync(
            u => u.Id == userId && u.IsActive &&
                 u.Roles.Any(r => r.Role == Role.HrSpecialist || r.Role == Role.HrManager),
            ct);

        return isHr
            ? null
            : Error.Validation(new Dictionary<string, string[]>
            {
                ["responsibleHrUserId"] = ["Sorumlu olarak bir İK çalışanı seçmelisiniz."],
            });
    }

    public static IRuleBuilderOptions<T, string> PositionTitle<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Pozisyon adını yazmalısınız.")
            .MaximumLength(Position.TitleMaxLength)
            .WithMessage($"Pozisyon adı en fazla {Position.TitleMaxLength} karakter olabilir.");

    public static IRuleBuilderOptions<T, int> PositionHeadcount<T>(this IRuleBuilder<T, int> rule) =>
        rule.InclusiveBetween(1, 1000).WithMessage("Kişi sayısı 1 ile 1000 arasında olmalı.");

    public static IRuleBuilderOptions<T, string?> PositionJobDescription<T>(this IRuleBuilder<T, string?> rule) =>
        rule.MaximumLength(10000).WithMessage("İş tanımı en fazla 10.000 karakter olabilir.");
}
