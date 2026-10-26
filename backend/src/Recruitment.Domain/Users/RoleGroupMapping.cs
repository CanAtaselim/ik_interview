using Recruitment.Domain.Common;

namespace Recruitment.Domain.Users;

/// <summary>Bir AD grubunun uygulamadaki hangi role karşılık geldiği.</summary>
public sealed class RoleGroupMapping : Entity
{
    private RoleGroupMapping() { }

    /// <summary>Grubun adı (CN) veya tam DN'i; karşılaştırma büyük/küçük harf duyarsızdır.</summary>
    public string GroupName { get; private set; } = default!;
    public Role Role { get; private set; }

    public static RoleGroupMapping Create(string groupName, Role role)
    {
        if (string.IsNullOrWhiteSpace(groupName))
            throw new DomainException("AD grup adı boş olamaz.");

        return new RoleGroupMapping { GroupName = groupName.Trim(), Role = role };
    }
}
