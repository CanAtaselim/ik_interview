using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recruitment.Domain.Users;

namespace Recruitment.Infrastructure.Persistence.Configurations;

internal sealed class UserPreferenceConfiguration : IEntityTypeConfiguration<UserPreference>
{
    public void Configure(EntityTypeBuilder<UserPreference> builder)
    {
        builder.HasKey(p => new { p.UserId, p.Key });
        builder.Property(p => p.Key).HasMaxLength(UserPreference.KeyMaxLength);
        builder.Property(p => p.Value).HasColumnType("jsonb");
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
