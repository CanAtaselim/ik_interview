using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recruitment.Domain.Calendar;
using Recruitment.Domain.Candidates;
using Recruitment.Domain.CvReviews;
using Recruitment.Domain.Dictionaries;
using Recruitment.Domain.Pipeline;
using Recruitment.Domain.Users;

namespace Recruitment.Infrastructure.Persistence.Configurations;

internal sealed class CvReviewConfiguration : IEntityTypeConfiguration<CvReview>
{
    public void Configure(EntityTypeBuilder<CvReview> builder)
    {
        builder.Property(r => r.SuggestionNote).HasMaxLength(CvReview.TextMaxLength);
        builder.Property(r => r.DecisionReason).HasMaxLength(CvReview.TextMaxLength);
        builder.Property(r => r.Decision).HasConversion<string>().HasMaxLength(20);

        builder.HasOne<Candidate>().WithMany().HasForeignKey(r => r.CandidateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<JobApplication>().WithMany().HasForeignKey(r => r.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DictionaryItem>().WithMany().HasForeignKey(r => r.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DictionaryItem>().WithMany().HasForeignKey(r => r.RejectionReasonId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.ReviewerUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(r => r.SuggestedByUserId).OnDelete(DeleteBehavior.Restrict);

        // "Bekleyen CV'lerim" ve gecikme işi karar durumuna göre okur.
        builder.HasIndex(r => new { r.ReviewerUserId, r.Decision });
        builder.HasIndex(r => new { r.Decision, r.DueAt });
        builder.HasIndex(r => r.CandidateId);
    }
}

internal sealed class HolidayConfiguration : IEntityTypeConfiguration<Holiday>
{
    public void Configure(EntityTypeBuilder<Holiday> builder)
    {
        builder.Property(h => h.Name).HasMaxLength(100);
        builder.HasIndex(h => new { h.Month, h.Day, h.Year });
    }
}
