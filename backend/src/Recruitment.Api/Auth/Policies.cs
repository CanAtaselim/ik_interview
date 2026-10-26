using Microsoft.AspNetCore.Authorization;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.Users;

namespace Recruitment.Api.Auth;

/// <summary>
/// Rol bazlı yetki politikaları. Kaynak bazlı kurallar (bu CV bana mı önerildi, bu mülakata
/// atanmış mıyım) burada değil, handler'da <see cref="ICurrentUser"/> ile kontrol edilir.
/// </summary>
public static class Policies
{
    public const string FullAccess = nameof(FullAccess);
    public const string HrOnly = nameof(HrOnly);
    public const string HrManagerOnly = nameof(HrManagerOnly);
    public const string CanRequestHeadcount = nameof(CanRequestHeadcount);
    public const string CanApproveRequest = nameof(CanApproveRequest);
    public const string CanReviewCv = nameof(CanReviewCv);
    public const string CanInterview = nameof(CanInterview);
    public const string CanManageQuestions = nameof(CanManageQuestions);
    public const string CanViewSalary = nameof(CanViewSalary);
    public const string CanViewBlacklist = nameof(CanViewBlacklist);
    public const string CanViewHiringRequests = nameof(CanViewHiringRequests);
    public const string CanViewPositions = nameof(CanViewPositions);
    public const string CanManageDictionaries = nameof(CanManageDictionaries);
    public const string CanManageSettings = nameof(CanManageSettings);
    public const string SystemAdmin = nameof(SystemAdmin);

    public static void Configure(AuthorizationOptions options)
    {
        var fullAccess = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .RequireClaim(AuthClaims.Scope, AuthClaims.FullScope)
            .Build();

        options.AddPolicy(FullAccess, fullAccess);

        // Politika belirtilmeyen her endpoint tam erişim ister; kısıtlı (referans) token'lı biri
        // yeni eklenen bir endpoint'e yanlışlıkla erişemez.
        options.DefaultPolicy = fullAccess;
        options.FallbackPolicy = fullAccess;

        options.AddRolePolicy(HrOnly, Role.HrSpecialist, Role.HrManager);
        options.AddRolePolicy(HrManagerOnly, Role.HrManager);
        options.AddRolePolicy(CanRequestHeadcount, Role.TeamLead, Role.DepartmentManager, Role.HrManager);
        options.AddRolePolicy(CanApproveRequest, Role.DepartmentManager, Role.HrManager);
        options.AddRolePolicy(CanReviewCv, Role.TeamLead, Role.HrSpecialist, Role.HrManager);
        // Mülakatçı olabilecek herkes; hangi mülakatı göreceği handler'da atanmaya göre süzülür.
        options.AddRolePolicy(
            CanInterview, Role.TeamLead, Role.DepartmentManager, Role.HrSpecialist, Role.HrManager, Role.Executive);
        options.AddRolePolicy(CanManageQuestions, Role.TeamLead, Role.HrSpecialist, Role.HrManager);
        options.AddRolePolicy(CanViewSalary, Role.HrSpecialist, Role.HrManager, Role.DepartmentManager);
        options.AddRolePolicy(CanViewBlacklist, Role.HrSpecialist, Role.HrManager, Role.SystemAdmin);
        options.AddRolePolicy(
            CanViewHiringRequests, Role.TeamLead, Role.DepartmentManager, Role.HrSpecialist, Role.HrManager);
        options.AddRolePolicy(
            CanViewPositions, Role.TeamLead, Role.DepartmentManager, Role.HrSpecialist, Role.HrManager);
        options.AddRolePolicy(CanManageDictionaries, Role.SystemAdmin, Role.HrManager);
        // E-posta şablonları, onay akışı, süreç aşamaları ve resmi tatiller; rol eşlemesi yalnızca SystemAdmin'dedir.
        options.AddRolePolicy(CanManageSettings, Role.SystemAdmin, Role.HrManager);
        options.AddRolePolicy(SystemAdmin, Role.SystemAdmin);
    }

    private static void AddRolePolicy(this AuthorizationOptions options, string name, params Role[] roles) =>
        options.AddPolicy(name, policy => policy
            .RequireAuthenticatedUser()
            .RequireClaim(AuthClaims.Scope, AuthClaims.FullScope)
            .RequireRole(roles.Select(role => role.ToString())));
}
