using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recruitment.Domain.Approvals;

namespace Recruitment.Infrastructure.Persistence.Configurations;

internal sealed class ApprovalFlowDefinitionConfiguration : IEntityTypeConfiguration<ApprovalFlowDefinition>
{
    public void Configure(EntityTypeBuilder<ApprovalFlowDefinition> builder)
    {
        builder.Property(f => f.Kind).HasConversion<string>().HasMaxLength(40);
        builder.HasIndex(f => f.Kind).IsUnique().HasFilter("is_deleted = false");
        builder.Ignore(f => f.ApproverRoles);

        builder.OwnsMany(f => f.Steps, steps =>
        {
            steps.ToTable("approval_flow_steps");
            steps.WithOwner().HasForeignKey("ApprovalFlowDefinitionId");
            steps.Property(s => s.Role).HasConversion<string>().HasMaxLength(50);
            steps.HasKey("ApprovalFlowDefinitionId", nameof(ApprovalFlowStep.Order));
            // Sırayı domain verir; EF yeni adımı mevcut kayıt sanıp güncellemeye çalışmasın.
            steps.Property(s => s.Order).ValueGeneratedNever();
        });
    }
}
