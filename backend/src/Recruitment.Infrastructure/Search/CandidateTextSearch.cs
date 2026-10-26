using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using Recruitment.Application.Abstractions;
using Recruitment.Domain.Candidates;
using Recruitment.Domain.Common;
using Recruitment.Infrastructure.Persistence.Configurations;

namespace Recruitment.Infrastructure.Search;

/// <summary>PostgreSQL tam metin araması (GIN indeksli tsvector) ile aday arar.</summary>
internal sealed class CandidateTextSearch : ICandidateTextSearch
{
    public IQueryable<Candidate> Apply(IQueryable<Candidate> candidates, string? term)
    {
        // Aranan metin, indekslenen metinle aynı kuralla sözcüklere bölünür. Sözcükler yalnızca
        // harf ve rakam içerdiği için tsquery sözdizimine güvenle yerleştirilebilir.
        var words = SearchText.Tokenize(term);
        if (words.Count == 0)
            return candidates;

        // "ahm yil" → "ahm:* & yil:*": her sözcük, sözcük başı eşleşmesiyle ve hepsi birlikte aranır.
        var query = string.Join(" & ", words.Select(word => $"{word}:*"));

        return candidates.Where(c =>
            EF.Property<NpgsqlTsVector>(c, CandidateConfiguration.SearchVector)
                .Matches(EF.Functions.ToTsQuery("simple", query)));
    }
}
