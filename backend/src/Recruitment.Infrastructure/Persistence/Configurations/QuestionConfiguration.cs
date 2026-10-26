using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recruitment.Domain.Dictionaries;
using Recruitment.Domain.Questions;
using Recruitment.Domain.Users;

namespace Recruitment.Infrastructure.Persistence.Configurations;

internal sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.Property(q => q.Text).HasMaxLength(Question.TextMaxLength);
        builder.Property(q => q.Guidance).HasMaxLength(Question.GuidanceMaxLength);
        builder.Property(q => q.PositionTitle).HasMaxLength(Question.PositionTitleMaxLength);
        builder.Property(q => q.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(q => q.Weight).HasPrecision(4, 2);

        builder.HasOne<DictionaryItem>().WithMany().HasForeignKey(q => q.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DictionaryItem>().WithMany().HasForeignKey(q => q.LevelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DictionaryItem>().WithMany().HasForeignKey(q => q.CompetencyId).OnDelete(DeleteBehavior.Restrict);

        builder.OwnsMany(q => q.Versions, versions =>
        {
            versions.ToTable("question_versions");
            versions.WithOwner().HasForeignKey("QuestionId");
            versions.HasKey("QuestionId", nameof(QuestionVersion.Number));
            // Sahiplenilmiş koleksiyonda EF anahtarın sayı kısmını varsayılan olarak üretilen sayar ve
            // yeni sürümü mevcut kayıt gibi günceller; numarayı domain verir.
            versions.Property(v => v.Number).ValueGeneratedNever();
            versions.Property(v => v.Text).HasMaxLength(Question.TextMaxLength);
            versions.Property(v => v.Guidance).HasMaxLength(Question.GuidanceMaxLength);
            versions.Property(v => v.Type).HasConversion<string>().HasMaxLength(20);
            versions.Property(v => v.Weight).HasPrecision(4, 2);
            versions.HasOne<User>().WithMany().HasForeignKey(v => v.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });
        builder.Navigation(q => q.Versions).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(q => new { q.DepartmentId, q.IsActive });
        builder.HasIndex(q => q.CompetencyId);
    }
}

internal sealed class InterviewTemplateConfiguration : IEntityTypeConfiguration<InterviewTemplate>
{
    public void Configure(EntityTypeBuilder<InterviewTemplate> builder)
    {
        builder.Property(t => t.Name).HasMaxLength(InterviewTemplate.NameMaxLength);
        builder.Ignore(t => t.OrderedQuestionIds);

        builder.HasOne<DictionaryItem>().WithMany().HasForeignKey(t => t.DepartmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DictionaryItem>().WithMany().HasForeignKey(t => t.LevelId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DictionaryItem>().WithMany().HasForeignKey(t => t.InterviewTypeId).OnDelete(DeleteBehavior.Restrict);

        builder.OwnsMany(t => t.Questions, questions =>
        {
            questions.ToTable("interview_template_questions");
            questions.WithOwner().HasForeignKey("InterviewTemplateId");
            questions.HasKey("InterviewTemplateId", nameof(InterviewTemplateQuestion.QuestionId));
            questions.Property(q => q.QuestionId).ValueGeneratedNever();
            questions.HasOne<Question>().WithMany().HasForeignKey(q => q.QuestionId).OnDelete(DeleteBehavior.Restrict);
            questions.HasIndex(q => q.QuestionId);
        });
        builder.Navigation(t => t.Questions).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(t => new { t.DepartmentId, t.IsActive });
    }
}
