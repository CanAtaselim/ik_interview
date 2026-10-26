using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recruitment.Domain.Users;

namespace Recruitment.Infrastructure.Persistence.Configurations;

internal sealed class RoleGroupMappingConfiguration : IEntityTypeConfiguration<RoleGroupMapping>
{
    public void Configure(EntityTypeBuilder<RoleGroupMapping> builder)
    {
        builder.Property(m => m.GroupName).HasMaxLength(512);
        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(50);

        // Silinen bir eşleme yeniden eklenebilsin diye yalnızca silinmemiş kayıtlar tekildir.
        builder.HasIndex(m => new { m.GroupName, m.Role }).IsUnique().HasFilter("is_deleted = false");
    }
}
