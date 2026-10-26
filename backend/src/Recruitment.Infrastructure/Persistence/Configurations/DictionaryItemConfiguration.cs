using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Recruitment.Domain.Dictionaries;

namespace Recruitment.Infrastructure.Persistence.Configurations;

internal sealed class DictionaryItemConfiguration : IEntityTypeConfiguration<DictionaryItem>
{
    public void Configure(EntityTypeBuilder<DictionaryItem> builder)
    {
        builder.Property(d => d.Type).HasConversion<string>().HasMaxLength(40);
        builder.Property(d => d.Name).HasMaxLength(DictionaryItem.NameMaxLength);

        builder.HasIndex(d => new { d.Type, d.Name }).IsUnique().HasFilter("is_deleted = false");
    }
}
