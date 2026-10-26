using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recruitment.Domain.Dictionaries;
using Recruitment.Domain.Users;

namespace Recruitment.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.Property(u => u.Username).HasMaxLength(256);
        builder.Property(u => u.DisplayName).HasMaxLength(256);
        builder.Property(u => u.Email).HasMaxLength(320);
        builder.Property(u => u.Department).HasMaxLength(256);
        builder.Property(u => u.Title).HasMaxLength(256);
        builder.Property(u => u.ManagerUsername).HasMaxLength(256);

        builder.HasIndex(u => u.Username).IsUnique();

        builder.HasOne<DictionaryItem>().WithMany().HasForeignKey(u => u.DepartmentId).OnDelete(DeleteBehavior.SetNull);

        builder.OwnsMany(u => u.Roles, roles =>
        {
            roles.ToTable("user_roles");
            roles.WithOwner().HasForeignKey("UserId");
            roles.Property(r => r.Role).HasConversion<string>().HasMaxLength(50);
            roles.HasKey("UserId", nameof(UserRole.Role));
        });
    }
}
