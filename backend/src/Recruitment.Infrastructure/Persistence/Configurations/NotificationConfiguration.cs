using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recruitment.Domain.Candidates;
using Recruitment.Domain.Notifications;
using Recruitment.Domain.Users;

namespace Recruitment.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).ValueGeneratedNever();
        builder.Property(n => n.Type).HasConversion<string>().HasMaxLength(50);
        builder.Property(n => n.Title).HasMaxLength(Notification.TitleMaxLength);
        builder.Property(n => n.Body).HasMaxLength(Notification.BodyMaxLength);
        builder.Property(n => n.Link).HasMaxLength(500);
        builder.Property(n => n.DedupeKey).HasMaxLength(300);
        builder.Ignore(n => n.IsRead);

        builder.HasOne<User>().WithMany().HasForeignKey(n => n.RecipientUserId).OnDelete(DeleteBehavior.Cascade);

        // Aynı olay iki kez işlense de kullanıcıya tek bildirim düşer.
        builder.HasIndex(n => new { n.RecipientUserId, n.DedupeKey }).IsUnique();
        builder.HasIndex(n => new { n.RecipientUserId, n.CreatedAt });
    }
}

internal sealed class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.HasKey(p => new { p.UserId, p.Type });
        builder.Property(p => p.Type).HasConversion<string>().HasMaxLength(50);
        builder.HasOne<User>().WithMany().HasForeignKey(p => p.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class OutgoingEmailConfiguration : IEntityTypeConfiguration<OutgoingEmail>
{
    public void Configure(EntityTypeBuilder<OutgoingEmail> builder)
    {
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();
        builder.Property(e => e.ToAddress).HasMaxLength(320);
        builder.Property(e => e.ToName).HasMaxLength(200);
        builder.Property(e => e.Subject).HasMaxLength(EmailTemplate.SubjectMaxLength);
        builder.Property(e => e.DedupeKey).HasMaxLength(300);
        builder.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.CalendarMethod).HasConversion<string>().HasMaxLength(20);
        builder.Property(e => e.LastError).HasMaxLength(2000);

        builder.HasOne<Candidate>().WithMany().HasForeignKey(e => e.CandidateId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.DedupeKey).IsUnique();
        builder.HasIndex(e => new { e.Status, e.CreatedAt });
    }
}

internal sealed class EmailTemplateConfiguration : IEntityTypeConfiguration<EmailTemplate>
{
    public void Configure(EntityTypeBuilder<EmailTemplate> builder)
    {
        builder.Property(t => t.Code).HasMaxLength(100);
        builder.Property(t => t.Name).HasMaxLength(200);
        builder.Property(t => t.Subject).HasMaxLength(EmailTemplate.SubjectMaxLength);
        builder.Property(t => t.Variables).HasMaxLength(500);
        builder.HasIndex(t => t.Code).IsUnique();
    }
}
