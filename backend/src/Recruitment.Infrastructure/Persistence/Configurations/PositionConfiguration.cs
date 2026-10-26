using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recruitment.Domain.Dictionaries;
using Recruitment.Domain.HiringRequests;
using Recruitment.Domain.Positions;
using Recruitment.Domain.Users;

namespace Recruitment.Infrastructure.Persistence.Configurations;

internal sealed class PositionConfiguration : IEntityTypeConfiguration<Position>
{
    public void Configure(EntityTypeBuilder<Position> builder)
    {
        builder.Property(p => p.Title).HasMaxLength(Position.TitleMaxLength);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(30);
        builder.Ignore(p => p.InterviewTemplateIds);
        builder.Property<List<Guid>>("_interviewTemplateIds").HasColumnName("interview_template_ids");

        builder.HasOne<DictionaryItem>().WithMany().HasForeignKey(p => p.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DictionaryItem>().WithMany().HasForeignKey(p => p.LevelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.ResponsibleHrUserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne<HiringRequest>().WithMany().HasForeignKey(p => p.HiringRequestId).OnDelete(DeleteBehavior.Restrict);

        // Bir kadro talebi için en fazla bir pozisyon açılır (outbox mesajı tekrar işlense bile).
        builder.HasIndex(p => p.HiringRequestId).IsUnique();
        builder.HasIndex(p => p.Status);
    }
}
