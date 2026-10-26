using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recruitment.Domain.Candidates;
using Recruitment.Domain.Dictionaries;
using Recruitment.Domain.Interviews;
using Recruitment.Domain.Pipeline;
using Recruitment.Domain.Positions;
using Recruitment.Domain.Questions;
using Recruitment.Domain.Users;

namespace Recruitment.Infrastructure.Persistence.Configurations;

internal sealed class InterviewConfiguration : IEntityTypeConfiguration<Interview>
{
    public void Configure(EntityTypeBuilder<Interview> builder)
    {
        builder.Property(i => i.Mode).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.MeetingUrl).HasMaxLength(Interview.MeetingUrlMaxLength);
        builder.Property(i => i.Note).HasMaxLength(Interview.TextMaxLength);
        builder.Property(i => i.CancellationReason).HasMaxLength(Interview.TextMaxLength);
        builder.Property(i => i.DebriefDecision).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.DebriefReason).HasMaxLength(Interview.TextMaxLength);
        builder.Ignore(i => i.InterviewerUserIds);

        builder.HasOne<JobApplication>().WithMany().HasForeignKey(i => i.ApplicationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Candidate>().WithMany().HasForeignKey(i => i.CandidateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Position>().WithMany().HasForeignKey(i => i.PositionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DictionaryItem>().WithMany().HasForeignKey(i => i.TypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DictionaryItem>().WithMany().HasForeignKey(i => i.RoomId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(i => i.ScheduledByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<InterviewTemplate>().WithMany().HasForeignKey(i => i.TemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(i => i.DebriefByUserId).OnDelete(DeleteBehavior.Restrict);

        builder.OwnsMany(i => i.Interviewers, interviewers =>
        {
            interviewers.ToTable("interview_interviewers");
            interviewers.WithOwner().HasForeignKey("InterviewId");
            interviewers.HasKey("InterviewId", nameof(InterviewInterviewer.UserId));
            interviewers.Property(x => x.UserId).ValueGeneratedNever();
            interviewers.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            interviewers.HasIndex(x => x.UserId);
        });
        builder.Navigation(i => i.Interviewers).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Takvim ve çakışma sorguları zaman aralığına göre okur.
        builder.HasIndex(i => new { i.Status, i.StartsAt });
        builder.HasIndex(i => i.ApplicationId);
        builder.HasIndex(i => i.CandidateId);
        builder.HasIndex(i => new { i.RoomId, i.StartsAt });
    }
}

internal sealed class InterviewerAvailabilityConfiguration : IEntityTypeConfiguration<InterviewerAvailability>
{
    public void Configure(EntityTypeBuilder<InterviewerAvailability> builder)
    {
        builder.ToTable("interviewer_availabilities");
        builder.HasOne<User>().WithMany().HasForeignKey(a => a.UserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(a => new { a.UserId, a.StartsAt });
    }
}
