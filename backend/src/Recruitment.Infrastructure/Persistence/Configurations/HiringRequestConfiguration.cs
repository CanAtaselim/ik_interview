using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recruitment.Domain.Dictionaries;
using Recruitment.Domain.HiringRequests;
using Recruitment.Domain.Users;

namespace Recruitment.Infrastructure.Persistence.Configurations;

internal sealed class HiringRequestConfiguration : IEntityTypeConfiguration<HiringRequest>
{
    public void Configure(EntityTypeBuilder<HiringRequest> builder)
    {
        builder.Property(r => r.Title).HasMaxLength(HiringRequest.TitleMaxLength);
        builder.Property(r => r.ReplacedEmployeeName).HasMaxLength(200);
        builder.Property(r => r.Reason).HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.Urgency).HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(r => r.AwaitingRole).HasConversion<string>().HasMaxLength(50);

        // Etiketler PostgreSQL dizisi (text[]) olarak saklanır; dışarıya salt okunur liste açılır.
        builder.Ignore(r => r.Competencies);
        builder.Property<List<string>>("_competencies").HasColumnName("competencies");

        builder.HasMany(r => r.ApprovalSteps).WithOne().HasForeignKey("HiringRequestId").IsRequired();

        builder.HasOne<DictionaryItem>().WithMany().HasForeignKey(r => r.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DictionaryItem>().WithMany().HasForeignKey(r => r.LevelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(r => r.Status);
    }
}

internal sealed class ApprovalStepConfiguration : IEntityTypeConfiguration<ApprovalStep>
{
    public void Configure(EntityTypeBuilder<ApprovalStep> builder)
    {
        builder.ToTable("hiring_request_approval_steps");

        builder.Property(s => s.Role).HasConversion<string>().HasMaxLength(50);
        builder.Property(s => s.Decision).HasConversion<string>().HasMaxLength(30);
        builder.Property(s => s.Note).HasMaxLength(2000);

        builder.HasOne<User>().WithMany().HasForeignKey(s => s.DecidedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
