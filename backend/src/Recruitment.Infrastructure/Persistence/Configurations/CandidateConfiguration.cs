using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;
using Recruitment.Domain.Audit;
using Recruitment.Domain.Candidates;
using Recruitment.Domain.Common;
using Recruitment.Domain.Dictionaries;
using Recruitment.Domain.Users;

namespace Recruitment.Infrastructure.Persistence.Configurations;

internal sealed class CandidateConfiguration : IEntityTypeConfiguration<Candidate>
{
    /// <summary>Tam metin arama kolonu; yalnızca veritabanında vardır (gölge özellik).</summary>
    public const string SearchVector = "SearchVector";

    public void Configure(EntityTypeBuilder<Candidate> builder)
    {
        builder.Property(c => c.FirstName).HasMaxLength(Candidate.NameMaxLength);
        builder.Property(c => c.LastName).HasMaxLength(Candidate.NameMaxLength);
        builder.Property(c => c.Email).HasMaxLength(320);
        builder.Property(c => c.Phone).HasMaxLength(30);
        builder.Property(c => c.NormalizedEmail).HasMaxLength(320);
        builder.Property(c => c.NormalizedPhone).HasMaxLength(10);
        builder.Property(c => c.NormalizedName).HasMaxLength(Candidate.NameMaxLength * 2 + 1);
        builder.Property(c => c.LinkedInUrl).HasMaxLength(300);
        builder.Property(c => c.GitHubUrl).HasMaxLength(300);
        builder.Property(c => c.City).HasMaxLength(100);

        builder.Ignore(c => c.FullName);
        builder.Ignore(c => c.Tags);
        builder.Property<List<string>>("_tags").HasColumnName("tags");

        // search_text'i uygulama üretir (sadeleştirilmiş, boşlukla ayrılmış sözcükler). Vektör ondan
        // türetilir; 'simple' yapılandırması kök bulma yapmaz, sözcük başı eşleşmesiyle aranır.
        builder.Property<NpgsqlTsVector>(SearchVector)
            .HasComputedColumnSql("to_tsvector('simple', search_text)", stored: true);
        builder.HasIndex(SearchVector).HasMethod("GIN");

        builder.HasMany(c => c.Documents).WithOne().HasForeignKey("CandidateId").IsRequired();
        builder.HasMany(c => c.Screenings).WithOne().HasForeignKey("CandidateId").IsRequired();
        builder.HasOne<DictionaryItem>().WithMany().HasForeignKey(c => c.SourceId).OnDelete(DeleteBehavior.Restrict);

        // Mükerrer aday araması bu iki kolondan yapılır.
        builder.HasIndex(c => c.NormalizedEmail);
        builder.HasIndex(c => c.NormalizedPhone);
        builder.HasIndex(c => c.CreatedAt);
    }
}

internal sealed class CandidateDocumentConfiguration : IEntityTypeConfiguration<CandidateDocument>
{
    public void Configure(EntityTypeBuilder<CandidateDocument> builder)
    {
        builder.ToTable("candidate_documents");

        builder.Property(d => d.FileName).HasMaxLength(260);
        builder.Property(d => d.ContentType).HasMaxLength(150);
        builder.Property(d => d.StorageKey).HasMaxLength(300);

        builder.HasOne<User>().WithMany().HasForeignKey(d => d.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<DictionaryItem>().WithMany().HasForeignKey(d => d.TypeId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ScreeningConfiguration : IEntityTypeConfiguration<Screening>
{
    public void Configure(EntityTypeBuilder<Screening> builder)
    {
        builder.ToTable("screenings");

        builder.Property(s => s.Channel).HasConversion<string>().HasMaxLength(30);
        builder.Property(s => s.WorkModel).HasConversion<string>().HasMaxLength(30);
        builder.Property(s => s.MilitaryStatus).HasConversion<string>().HasMaxLength(30);
        builder.Property(s => s.LocationProvince).HasMaxLength(100);
        builder.Property(s => s.LocationDistrict).HasMaxLength(100);
        builder.Property(s => s.ExpectedSalaryMax).HasPrecision(14, 2);

        builder.ComplexProperty(s => s.ExpectedSalary!, ConfigureMoney);
        builder.ComplexProperty(s => s.CurrentSalary!, ConfigureMoney);

        builder.Ignore(s => s.TechnicalSkillTags);
        builder.Property<List<string>>("_technicalSkillTags").HasColumnName("technical_skill_tags");

        // Eğitim ve dil listeleri kaydın parçasıdır, ayrı sorgulanmaz: JSON kolonunda saklanır.
        builder.OwnsMany(s => s.Educations, educations => educations.ToJson("educations"));
        builder.OwnsMany(s => s.Languages, languages =>
        {
            languages.ToJson("languages");
            languages.Property(l => l.Level).HasConversion<string>();
        });

        builder.HasOne<User>().WithMany().HasForeignKey(s => s.InterviewerUserId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureMoney(ComplexPropertyBuilder<Money> money)
    {
        money.Property(m => m.Amount).HasPrecision(14, 2);
        money.Property(m => m.Currency).HasMaxLength(3);
    }
}

internal sealed class TimelineEntryConfiguration : IEntityTypeConfiguration<TimelineEntry>
{
    public void Configure(EntityTypeBuilder<TimelineEntry> builder)
    {
        builder.Property(t => t.Type).HasConversion<string>().HasMaxLength(50);
        builder.Property(t => t.Summary).HasMaxLength(500);

        builder.HasOne<Candidate>().WithMany().HasForeignKey(t => t.CandidateId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(t => new { t.CandidateId, t.OccurredAt });
    }
}

internal sealed class AccessLogConfiguration : IEntityTypeConfiguration<AccessLog>
{
    public void Configure(EntityTypeBuilder<AccessLog> builder)
    {
        builder.Property(a => a.Action).HasConversion<string>().HasMaxLength(50);

        builder.HasIndex(a => new { a.CandidateId, a.OccurredAt });
        builder.HasIndex(a => a.UserId);
    }
}
