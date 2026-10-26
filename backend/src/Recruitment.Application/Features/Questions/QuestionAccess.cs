using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Questions;

namespace Recruitment.Application.Features.Questions;

/// <summary>
/// Soru bankası ve şablonlarda birim kısıtı: İK tüm birimleri, takım lideri yalnızca kendi birimini
/// görür ve yönetir.
/// </summary>
internal static class QuestionAccess
{
    public static readonly Error NoDepartment = Error.Forbidden(
        "Question.NoDepartment", "Biriminiz tanımlı olmadığı için soru bankasını kullanamazsınız. Sistem yöneticinize haber verin.");

    public static readonly Error OtherDepartment = Error.Forbidden(
        "Question.OtherDepartment", "Yalnızca kendi biriminizin soru ve şablonlarını yönetebilirsiniz.");

    public static bool CanManageDepartment(this ICurrentUser user, Guid departmentId) =>
        user.IsHr || (user.DepartmentId is { } own && own == departmentId);

    /// <summary>Verilen birim için yönetim yetkisi yoksa hata; varsa <c>null</c>.</summary>
    public static Error? DepartmentError(this ICurrentUser user, Guid departmentId) =>
        user.CanManageDepartment(departmentId) ? null
        : user.DepartmentId is null ? NoDepartment
        : OtherDepartment;

    public static IQueryable<Question> VisibleQuestions(this IAppDbContext db, ICurrentUser user)
    {
        if (user.IsHr)
            return db.Questions;

        var departmentId = user.DepartmentId;
        return db.Questions.Where(q => departmentId != null && q.DepartmentId == departmentId);
    }

    public static IQueryable<InterviewTemplate> VisibleTemplates(this IAppDbContext db, ICurrentUser user)
    {
        if (user.IsHr)
            return db.InterviewTemplates;

        var departmentId = user.DepartmentId;
        return db.InterviewTemplates.Where(t => departmentId != null && t.DepartmentId == departmentId);
    }
}
