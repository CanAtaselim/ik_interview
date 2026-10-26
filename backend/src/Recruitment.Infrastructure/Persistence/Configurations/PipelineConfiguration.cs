using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recruitment.Domain.Candidates;
using Recruitment.Domain.Dictionaries;
using Recruitment.Domain.Pipeline;
using Recruitment.Domain.Positions;
using Recruitment.Domain.Users;

namespace Recruitment.Infrastructure.Persistence.Configurations;

internal sealed class PipelineTemplateConfiguration : IEntityTypeConfiguration<PipelineTemplate>
{
    public void Configure(EntityTypeBuilder<PipelineTemplate> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(200);
        builder.Ignore(t => t.OrderedStages);
        builder.Ignore(t => t.FirstStage);

        builder.HasMany(t => t.Stages).WithOne().HasForeignKey("PipelineTemplateId").IsRequired();

        // Aynı anda tek bir varsayılan şablon olabilir.
        builder.HasIndex(t => t.IsDefault).IsUnique().HasFilter("is_default = true and is_deleted = false");
    }
}

internal sealed class PipelineStageDefinitionConfiguration : IEntityTypeConfiguration<PipelineStageDefinition>
{
    public void Configure(EntityTypeBuilder<PipelineStageDefinition> builder)
    {
        builder.ToTable("pipeline_stage_definitions");

        builder.Property(s => s.Name).HasMaxLength(100);
        builder.Property(s => s.Kind).HasConversion<string>().HasMaxLength(40);
    }
}

internal sealed class JobApplicationConfiguration : IEntityTypeConfiguration<JobApplication>
{
    public void Configure(EntityTypeBuilder<JobApplication> builder)
    {
        builder.ToTable("applications");

        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(a => a.ClosingNote).HasMaxLength(2000);

        builder.HasMany(a => a.History).WithOne().HasForeignKey("ApplicationId").IsRequired();

        builder.HasOne<Candidate>().WithMany().HasForeignKey(a => a.CandidateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Position>().WithMany().HasForeignKey(a => a.PositionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PipelineStageDefinition>().WithMany().HasForeignKey(a => a.StageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DictionaryItem>().WithMany().HasForeignKey(a => a.RejectionReasonId).OnDelete(DeleteBehavior.Restrict);

        // Bir adayın aynı pozisyonda yalnızca bir aktif süreci olabilir.
        builder.HasIndex(a => new { a.CandidateId, a.PositionId })
            .IsUnique()
            .HasFilter("status = 'Active' and is_deleted = false");
        builder.HasIndex(a => a.PositionId);
    }
}

internal sealed class ApplicationStageChangeConfiguration : IEntityTypeConfiguration<ApplicationStageChange>
{
    public void Configure(EntityTypeBuilder<ApplicationStageChange> builder)
    {
        builder.Property(c => c.BypassReason).HasMaxLength(2000);
        builder.ToTable("application_stage_changes");

        builder.Property(c => c.FromStatus).HasConversion<string>().HasMaxLength(30);
        builder.Property(c => c.ToStatus).HasConversion<string>().HasMaxLength(30);

        builder.HasOne<User>().WithMany().HasForeignKey(c => c.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
