using Recruitment.Domain.Candidates.Events;
using Recruitment.Domain.Common;

namespace Recruitment.Domain.Candidates;

/// <summary>
/// Aday: kişinin kendisi, tek kayıt. Pozisyona bağlı olmadan (genel havuz) eklenebilir.
/// Dosyaları ve ön görüşme kayıtları aday üzerinden yönetilir; aranabilir metni aday kendisi üretir.
/// </summary>
public sealed class Candidate : AggregateRoot
{
    public const int NameMaxLength = 100;
    public const int SearchTextMaxLength = 400_000;

    private readonly List<string> _tags = [];
    private readonly List<CandidateDocument> _documents = [];
    private readonly List<Screening> _screenings = [];

    private Candidate() { }

    public string FirstName { get; private set; } = default!;
    public string LastName { get; private set; } = default!;
    public string? Email { get; private set; }
    public string? Phone { get; private set; }

    /// <summary>Mükerrer eşleştirmesi için sadeleştirilmiş e-posta ve telefon.</summary>
    public string? NormalizedEmail { get; private set; }
    public string? NormalizedPhone { get; private set; }

    /// <summary>Sadeleştirilmiş ad soyad; kara listede ad benzerliği aramasında kullanılır.</summary>
    public string NormalizedName { get; private set; } = string.Empty;

    public string? LinkedInUrl { get; private set; }
    public string? GitHubUrl { get; private set; }
    public string? City { get; private set; }
    public Guid? SourceId { get; private set; }
    public string? Notes { get; private set; }
    public bool InTalentPool { get; private set; }

    /// <summary>İK mükerrer uyarısını görüp "yine de ekle/kaydet" dediyse true.</summary>
    public bool DuplicateAcknowledged { get; private set; }

    /// <summary>Tam metin aramanın kaynağı; her değişiklikte yeniden üretilir.</summary>
    public string SearchText { get; private set; } = string.Empty;

    public string FullName => $"{FirstName} {LastName}";
    public IReadOnlyList<string> Tags => _tags;
    public IReadOnlyCollection<CandidateDocument> Documents => _documents;
    public IReadOnlyCollection<Screening> Screenings => _screenings;

    public static Candidate Create(
        CandidateProfile profile, Guid createdByUserId, bool despiteDuplicate, TimeProvider clock)
    {
        // Yeni aday her zaman havuza girer; pozisyona alınması havuzdan çıkarmaz.
        var candidate = new Candidate { DuplicateAcknowledged = despiteDuplicate, InTalentPool = true };
        candidate.Apply(profile);
        candidate.Raise(new CandidateCreated(candidate.Id, createdByUserId, clock.GetUtcNow(), despiteDuplicate));
        return candidate;
    }

    public void UpdateProfile(CandidateProfile profile, bool despiteDuplicate)
    {
        Apply(profile);
        DuplicateAcknowledged |= despiteDuplicate;
    }

    public void SetTalentPool(bool inTalentPool, Guid changedByUserId, TimeProvider clock)
    {
        if (InTalentPool == inTalentPool)
            return;

        InTalentPool = inTalentPool;
        Raise(new CandidateTalentPoolChanged(Id, inTalentPool, changedByUserId, clock.GetUtcNow()));
    }

    public CandidateDocument AddDocument(
        string fileName, string contentType, long sizeBytes, string storageKey, Guid? typeId, Guid uploadedByUserId, TimeProvider clock)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new DomainException("Dosya adı boş olamaz.");
        if (sizeBytes <= 0)
            throw new DomainException("Boş dosya yüklenemez.");

        var now = clock.GetUtcNow();
        var document = new CandidateDocument(fileName.Trim(), contentType, sizeBytes, storageKey, typeId, uploadedByUserId, now);
        _documents.Add(document);
        Raise(new CandidateDocumentAdded(Id, document.Id, document.FileName, uploadedByUserId, now));
        return document;
    }

    public void RemoveDocument(Guid documentId)
    {
        _documents.Remove(FindDocument(documentId));
        RebuildSearchText();
    }

    public void SetDocumentText(Guid documentId, string? text)
    {
        FindDocument(documentId).SetExtractedText(text);
        RebuildSearchText();
    }

    public Screening StartScreening(Guid interviewerUserId, TimeProvider clock)
    {
        var screening = new Screening(interviewerUserId);
        _screenings.Add(screening);
        Raise(new ScreeningStarted(Id, screening.Id, interviewerUserId, clock.GetUtcNow()));
        return screening;
    }

    /// <summary>
    /// Ön görüşme kaydını kaldırır (soft delete; kayıt veritabanında kalır). Zaman çizelgesine yazılır.
    /// </summary>
    public void RemoveScreening(Guid screeningId, Guid actorUserId, TimeProvider clock)
    {
        var screening = _screenings.FirstOrDefault(s => s.Id == screeningId)
            ?? throw new DomainException("Ön görüşme kaydı bulunamadı.");

        _screenings.Remove(screening);
        RebuildSearchText();
        Raise(new ScreeningDeleted(Id, screeningId, actorUserId, clock.GetUtcNow()));
    }

    public void UpdateScreening(Guid screeningId, ScreeningDetails details)
    {
        var screening = _screenings.FirstOrDefault(s => s.Id == screeningId)
            ?? throw new DomainException("Ön görüşme kaydı bulunamadı.");

        screening.Update(details);
        RebuildSearchText();
    }

    private CandidateDocument FindDocument(Guid documentId) =>
        _documents.FirstOrDefault(d => d.Id == documentId) ?? throw new DomainException("Dosya bulunamadı.");

    private void Apply(CandidateProfile profile)
    {
        if (string.IsNullOrWhiteSpace(profile.FirstName))
            throw new DomainException("Adayın adını yazmalısınız.");
        if (string.IsNullOrWhiteSpace(profile.LastName))
            throw new DomainException("Adayın soyadını yazmalısınız.");

        var email = Clean(profile.Email);
        var phone = Clean(profile.Phone);
        if (email is null && phone is null)
            throw new DomainException("E-posta veya telefon alanlarından en az biri dolu olmalı.");

        FirstName = profile.FirstName.Trim();
        LastName = profile.LastName.Trim();
        Email = email;
        Phone = phone;
        NormalizedEmail = ContactNormalizer.NormalizeEmail(email);
        NormalizedPhone = ContactNormalizer.NormalizePhone(phone);
        NormalizedName = ContactNormalizer.NormalizeName(FirstName, LastName);
        LinkedInUrl = Clean(profile.LinkedInUrl);
        GitHubUrl = Clean(profile.GitHubUrl);
        City = Clean(profile.City);
        SourceId = profile.SourceId;
        Notes = Clean(profile.Notes);

        _tags.Clear();
        _tags.AddRange(profile.Tags.Select(Clean).OfType<string>().Distinct(StringComparer.OrdinalIgnoreCase));

        RebuildSearchText();
    }

    // Notlar bilerek aranmaz: İK'nın serbest notu, aday araması sonuçlarını kirletmesin.
    private void RebuildSearchText() =>
        SearchText = Common.SearchText.Build(
            [
                FirstName, LastName, Email, NormalizedPhone, City,
                .. _tags,
                .. _screenings.SelectMany(s => s.SearchableTexts()),
                .. _documents.Select(d => d.ExtractedText),
            ],
            SearchTextMaxLength);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
