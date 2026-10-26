using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recruitment.Domain.Dictionaries;
using Recruitment.Domain.Interviews;
using Recruitment.Domain.Questions;
using Recruitment.Domain.Scorecards;
using Recruitment.Domain.Users;

namespace Recruitment.Infrastructure.Persistence.Configurations;

internal sealed class ScorecardConfiguration : IEntityTypeConfiguration<Scorecard>
{
    public void Configure(EntityTypeBuilder<Scorecard> builder)
    {
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.Recommendation).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.GeneralNote).HasMaxLength(Scorecard.NoteMaxLength);
        builder.Property(s => s.ReopenReason).HasMaxLength(2000);
        builder.Ignore(s => s.OrderedItems);
        builder.Ignore(s => s.IsSubmitted);

        builder.HasOne<Interview>().WithMany().HasForeignKey(s => s.InterviewId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<User>().WithMany().HasForeignKey(s => s.InterviewerUserId).OnDelete(DeleteBehavior.Restrict);

        builder.OwnsMany(s => s.Items, items =>
        {
            items.ToTable("scorecard_items");
            items.WithOwner().HasForeignKey("ScorecardId");
            items.HasKey(i => i.Id);
            // Kimliği domain verir; EF yeni maddeyi mevcut sanıp güncellemeye çalışmasın.
            items.Property(i => i.Id).ValueGeneratedNever();
            items.Property(i => i.Text).HasMaxLength(Scorecard.TextMaxLength);
            items.Property(i => i.Guidance).HasMaxLength(Question.GuidanceMaxLength);
            items.Property(i => i.Note).HasMaxLength(Scorecard.NoteMaxLength);
            items.Property(i => i.Weight).HasPrecision(4, 2);
            items.Ignore(i => i.IsCustom);
            items.HasOne<Question>().WithMany().HasForeignKey(i => i.QuestionId).OnDelete(DeleteBehavior.Restrict);
            items.HasOne<DictionaryItem>().WithMany().HasForeignKey(i => i.CompetencyId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Navigation(s => s.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Her mülakatçının bir mülakat için tek değerlendirmesi olur.
        builder.HasIndex(s => new { s.InterviewId, s.InterviewerUserId }).IsUnique().HasFilter("is_deleted = false");
    }
}
