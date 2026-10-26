using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recruitment.Domain.Blacklist;
using Recruitment.Domain.Candidates;
using Recruitment.Domain.Dictionaries;
using Recruitment.Domain.Users;

namespace Recruitment.Infrastructure.Persistence.Configurations;

internal sealed class BlacklistEntryConfiguration : IEntityTypeConfiguration<BlacklistEntry>
{
    public void Configure(EntityTypeBuilder<BlacklistEntry> builder)
    {
        builder.Property(e => e.Description).HasMaxLength(BlacklistEntry.DescriptionMaxLength);
        builder.Property(e => e.RemovalReason).HasMaxLength(2000);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(30);

        builder.HasOne<Candidate>().WithMany().HasForeignKey(e => e.CandidateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DictionaryItem>().WithMany().HasForeignKey(e => e.ReasonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(e => e.RemovedByUserId).OnDelete(DeleteBehavior.Restrict);

        // Bir adayın aynı anda tek bir açık (onay bekleyen veya yürürlükteki) kaydı olabilir.
        builder.HasIndex(e => e.CandidateId)
            .IsUnique()
            .HasFilter("status in ('PendingApproval', 'Active') and is_deleted = false");
        builder.HasIndex(e => e.Status);
    }
}
