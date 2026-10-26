using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Recruitment.Application.Abstractions;
using Recruitment.Application.Common;
using Recruitment.Domain.Users;

namespace Recruitment.Application.Features.Settings.RoleMappings;

public sealed record RoleMappingDto(Guid Id, string GroupName, Role Role);

/// <summary>AD grubu → rol eşlemeleri. Rol, kullanıcının bir sonraki girişinde güncellenir.</summary>
public sealed record GetRoleMappingsQuery : IQuery<IReadOnlyList<RoleMappingDto>>;

internal sealed class GetRoleMappingsQueryHandler(IAppDbContext db) : IQueryHandler<GetRoleMappingsQuery, IReadOnlyList<RoleMappingDto>>
{
    public async Task<Result<IReadOnlyList<RoleMappingDto>>> Handle(GetRoleMappingsQuery query, CancellationToken ct) =>
        await db.RoleGroupMappings.AsNoTracking()
            .OrderBy(m => m.Role).ThenBy(m => m.GroupName)
            .Select(m => new RoleMappingDto(m.Id, m.GroupName, m.Role))
            .ToListAsync(ct);
}

public sealed record CreateRoleMappingCommand(string GroupName, Role Role) : ICommand<Guid>;

internal sealed class CreateRoleMappingCommandValidator : AbstractValidator<CreateRoleMappingCommand>
{
    public CreateRoleMappingCommandValidator()
    {
        RuleFor(c => c.GroupName)
            .NotEmpty().WithMessage("AD grubunun adını yazmalısınız.")
            .MaximumLength(512).WithMessage("Grup adı en fazla 512 karakter olabilir.");
        RuleFor(c => c.Role).IsInEnum().WithMessage("Bir rol seçmelisiniz.");
    }
}

internal sealed class CreateRoleMappingCommandHandler(IAppDbContext db) : ICommandHandler<CreateRoleMappingCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateRoleMappingCommand command, CancellationToken ct)
    {
        var name = command.GroupName.Trim();
        // Kültürden bağımsız: Türkçe kültürde "I" küçük harfe "ı" döner ve karşılaştırma tutmazdı.
        var lowered = name.ToLowerInvariant();
        if (await db.RoleGroupMappings.AnyAsync(m => m.Role == command.Role && m.GroupName.ToLower() == lowered, ct))
            return SettingsErrors.RoleMappingDuplicate;

        var mapping = RoleGroupMapping.Create(name, command.Role);
        db.RoleGroupMappings.Add(mapping);
        await db.SaveChangesAsync(ct);
        return mapping.Id;
    }
}

/// <summary>Eşlemeyi siler. Sistem Admin rolüne eşlenmiş son grup silinemez (kilitlenmeyi önler).</summary>
public sealed record DeleteRoleMappingCommand(Guid Id) : ICommand<Unit>;

internal sealed class DeleteRoleMappingCommandHandler(IAppDbContext db) : ICommandHandler<DeleteRoleMappingCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteRoleMappingCommand command, CancellationToken ct)
    {
        var mapping = await db.RoleGroupMappings.FirstOrDefaultAsync(m => m.Id == command.Id, ct);
        if (mapping is null)
            return SettingsErrors.RoleMappingNotFound;

        if (mapping.Role == Role.SystemAdmin &&
            !await db.RoleGroupMappings.AnyAsync(m => m.Role == Role.SystemAdmin && m.Id != mapping.Id, ct))
        {
            return SettingsErrors.LastAdminMapping;
        }

        db.RoleGroupMappings.Remove(mapping);
        await db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
