# Backend Kuralları (.NET 10 + PostgreSQL)

## İçindekiler
1. Paketler
2. Katman sorumlulukları ve klasör yapısı
3. Domain kalıpları
4. CQRS: kendi dispatcher'ımız
5. Result ve hata yönetimi
6. Validasyon
7. Veri erişimi: EF Core, Repository, Unit of Work
8. Domain event'leri ve Outbox
9. Arka plan job'ları (Hangfire)
10. Dış servis arayüzleri (e-posta, takvim, dosya, AI)
11. API katmanı
12. Konfigürasyon (appsettings.json)
13. Loglama
14. Test
15. Kodlama standartları

---

## 1. Paketler

| Amaç | Paket | Proje |
| --- | --- | --- |
| ORM | `Microsoft.EntityFrameworkCore`, `Npgsql.EntityFrameworkCore.PostgreSQL` | Infrastructure |
| snake_case isimlendirme | `EFCore.NamingConventions` | Infrastructure |
| Validasyon | `FluentValidation`, `FluentValidation.DependencyInjectionExtensions` | Application |
| Loglama | `Serilog.AspNetCore`, `Serilog.Sinks.Console`, `Serilog.Sinks.File` | Api |
| Job | `Hangfire.AspNetCore`, `Hangfire.PostgreSql` | Infrastructure / Api |
| LDAP | `System.DirectoryServices.Protocols` | Infrastructure |
| JWT | `Microsoft.AspNetCore.Authentication.JwtBearer` | Api |
| E-posta | `MailKit` | Infrastructure |
| PDF (Faz 2, teklif mektubu) | `QuestPDF` (Community lisansı koşullarını kontrol et) | Infrastructure |
| Excel export | `ClosedXML` | Infrastructure |
| Test | `xunit`, `NSubstitute`, `Shouldly`, `Testcontainers.PostgreSql`, `Microsoft.AspNetCore.Mvc.Testing` | tests |

**Kullanma:** MediatR, AutoMapper, FluentAssertions v8+ (ticari lisans), Moq. Yeni paket eklemeden önce lisansını kontrol et.

## 2. Katman sorumlulukları ve klasör yapısı

```
Recruitment.Domain/
  Common/            Entity.cs, AggregateRoot.cs, IDomainEvent.cs, DomainException.cs, Money.cs
  Candidates/        Candidate.cs, CandidateDocument.cs, Screening.cs, Events/...
  Blacklist/         BlacklistEntry.cs, BlacklistStatus.cs
  HiringRequests/    ...
  Positions/  Applications/  CvReviews/  Interviews/  Questions/  Scorecards/
  Offers/  Onboarding/  Referrals/  Users/  Dictionaries/

Recruitment.Application/
  Abstractions/      ICommand, IQuery, handler arayüzleri, IDispatcher,
                     IAppDbContext, ICurrentUser, IEmailSender, ICalendarService,
                     IFileStorage, ILdapService, ITokenService, IAiService
  Common/            Result.cs, Error.cs, PagedResult.cs, Behaviors (validation, logging, transaction)
  Features/
    Candidates/
      CreateCandidate/   CreateCandidateCommand.cs, ...Handler.cs, ...Validator.cs
      GetCandidateDetail/ GetCandidateDetailQuery.cs, ...Handler.cs, CandidateDetailDto.cs
      SearchCandidates/
    Blacklist/  HiringRequests/  Interviews/ ...
  DependencyInjection.cs

Recruitment.Infrastructure/
  Persistence/       AppDbContext.cs, Configurations/, Migrations/, Interceptors/, Seed/
  Identity/          LdapService.cs, TokenService.cs, RefreshTokenStore.cs
  Email/             SmtpEmailSender.cs, EmailTemplateRenderer.cs
  Calendar/          InternalAvailabilityCalendarService.cs (Faz 1), Graph/ veya Ews/ (Faz 2)
  Files/             LocalFileStorage.cs
  Outbox/            OutboxMessage.cs, OutboxProcessorJob.cs
  Jobs/              CvReviewSlaJob.cs, InterviewReminderJob.cs, BlacklistExpiryJob.cs ...
  DependencyInjection.cs

Recruitment.Api/
  Controllers/       CandidatesController.cs ...
  Auth/              Policies.cs, CurrentUser.cs
  Hubs/              NotificationHub.cs
  Middleware/        ExceptionHandler (IExceptionHandler)
  Program.cs
```

Kural: bir özellik dosyaları **özellik klasöründe birlikte** durur (vertical slice). `Services/` gibi her şeyin toplandığı klasörler açma.

## 3. Domain kalıpları

```csharp
public abstract class Entity
{
    public Guid Id { get; protected set; } = Guid.CreateVersion7();
    public DateTimeOffset CreatedAt { get; private set; }
    public string CreatedBy { get; private set; } = default!;
    public DateTimeOffset? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }
    public bool IsDeleted { get; private set; }

    private readonly List<IDomainEvent> _events = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _events;
    protected void Raise(IDomainEvent e) => _events.Add(e);
    public void ClearDomainEvents() => _events.Clear();
}
```

- Entity oluşturma `static Create(...)` fabrika metoduyla; geçersiz durumda `DomainException`.
- Koleksiyonlar `private readonly List<T>` + `IReadOnlyCollection<T>` dışa açılır.
- İş kuralı entity'de:

```csharp
public sealed class BlacklistEntry : Entity
{
    public void Approve(string approvedBy, TimeProvider clock)
    {
        if (Status != BlacklistStatus.PendingApproval)
            throw new DomainException("Yalnızca onay bekleyen kara liste kaydı onaylanabilir.");
        Status = BlacklistStatus.Active;
        ApprovedBy = approvedBy;
        ApprovedAt = clock.GetUtcNow();
        Raise(new BlacklistEntryApproved(Id, CandidateId));
    }
}
```

- Pipeline aşamaları **State pattern**: `IStageState.CanMoveTo(next, context)`.
- Skor hesabı **Strategy pattern**: `IScoreCalculator`.
- Arama filtreleri **Specification pattern**: `Specification<T>` → `Expression<Func<T,bool>>`, query handler'larda birleştirilir.

## 4. CQRS: kendi dispatcher'ımız

```csharp
public interface ICommand<TResponse> { }
public interface IQuery<TResponse> { }
public interface ICommandHandler<in TCommand, TResponse> where TCommand : ICommand<TResponse>
{ Task<Result<TResponse>> Handle(TCommand command, CancellationToken ct); }
public interface IQueryHandler<in TQuery, TResponse> where TQuery : IQuery<TResponse>
{ Task<Result<TResponse>> Handle(TQuery query, CancellationToken ct); }

public interface IDispatcher
{
    Task<Result<T>> Send<T>(ICommand<T> command, CancellationToken ct = default);
    Task<Result<T>> Query<T>(IQuery<T> query, CancellationToken ct = default);
}
```

- Handler'lar assembly taramasıyla DI'a kaydedilir (`DependencyInjection.cs` içinde küçük bir reflection döngüsü).
- Kesişen işler (validasyon, loglama, transaction) dispatcher içinde dekoratör zinciriyle: `ValidationBehavior → LoggingBehavior → TransactionBehavior (sadece command) → Handler`.
- Command'lar veri değiştirir, `Result<Guid>` veya `Result<Unit>` döner. Query'ler veri değiştirmez, `AsNoTracking()` ve doğrudan DTO projeksiyonu (`Select`) kullanır.

## 5. Result ve hata yönetimi

- Beklenen hatalar (bulunamadı, yetkisiz, iş kuralı, validasyon) `Result.Failure(Error)` ile döner; exception akış kontrolü için kullanılmaz.
- `Error`: `Code` ("Candidate.NotFound"), `Message` (Türkçe), `Type` (`Validation` / `NotFound` / `Conflict` / `Forbidden` / `BusinessRule`).
- `DomainException` beklenmedik değil; dispatcher yakalayıp `BusinessRule` hatasına çevirir.
- API'de `Result` → RFC 7807 `ProblemDetails`: Validation 400 (alan hataları `errors` içinde), NotFound 404, Forbidden 403, Conflict 409, BusinessRule 422. Beklenmeyen exception 500 + log + kullanıcıya genel Türkçe mesaj (iç detay sızdırma).

## 6. Validasyon

- Her command için `AbstractValidator<TCommand>`; mesajlar Türkçe: `.WithMessage("E-posta veya telefon alanlarından en az biri dolu olmalı.")`.
- Validator yalnızca girdi biçimini kontrol eder; veritabanı gerektiren iş kuralları handler'da veya domain'de.
- Ön görüşme formunda **hiçbir alan zorunlu değildir**; sadece biçim (puan 1–5, yıl ≥ 0, URL formatı) doğrulanır.

## 7. Veri erişimi

- `AppDbContext : DbContext, IAppDbContext`. Application `IAppDbContext` üzerinden `DbSet`'lere erişir (EF Core'un kendisi zaten Repository + Unit of Work'tür). Karmaşık aggregate yükleme gereken yerde `ICandidateRepository` gibi özel repository yazılabilir; jenerik `IRepository<T>` yazma.
- `UseNpgsql(...).UseSnakeCaseNamingConvention()`.
- Her entity için ayrı `IEntityTypeConfiguration<T>`; string alanlara `HasMaxLength`, enum'lar `HasConversion<string>()`.
- Global query filter: `IsDeleted == false`.
- Interceptor'lar: `AuditableEntityInterceptor` (Created/Updated alanları), `AuditLogInterceptor` (eski/yeni değer JSON), `DomainEventsToOutboxInterceptor`.
- Tam metin arama: `candidates.search_vector` generated column, `to_tsvector('turkish', ...)`, GIN index. Benzerlik için `pg_trgm` extension.
- Sayfalama her liste query'sinde zorunlu: `PagedResult<T>` (`Items`, `TotalCount`, `Page`, `PageSize`), varsayılan 20, en fazla 100.
- Migration isimleri PascalCase ve açıklayıcı: `AddBlacklist`, `AddScreeningFields`.
- Seed: sözlükler ve varsayılan şablonlar `Persistence/Seed/` altında, idempotent.

## 8. Domain event'leri ve Outbox

1. Entity `Raise(...)` ile event üretir.
2. `SaveChanges` sırasında interceptor event'leri aynı transaction içinde `outbox_messages` tablosuna JSON olarak yazar.
3. `OutboxProcessorJob` (Hangfire, 15 sn'de bir) bekleyen mesajları okur, `IDomainEventHandler<TEvent>`'leri çalıştırır, işlenince `ProcessedAt` doldurur; hata olursa `Error` + deneme sayısı, en fazla 5 deneme.
4. Event handler'ları idempotent olmalı (aynı mesaj iki kez işlenebilir).

E-posta, bildirim, zaman çizelgesi kaydı ve takvim işlemleri **her zaman** event handler'ından yapılır, command handler'ından doğrudan yapılmaz.

## 9. Arka plan job'ları (Hangfire)

Hangfire PostgreSQL storage, dashboard `/hangfire` (yalnızca `SystemAdmin`).

| Job | Sıklık | İş |
| --- | --- | --- |
| `OutboxProcessorJob` | 15 sn | Outbox mesajlarını işle |
| `CvReviewSlaJob` | saatlik | Süresi geçen CV değerlendirmeleri için hatırlatma/gecikme |
| `InterviewReminderJob` | 15 dk | 24 saat ve 1 saat kala hatırlatma |
| `BlacklistExpiryJob` | günlük 02:00 | Süresi dolan kayıtları `Expired` yap |
| `OnboardingTaskReminderJob` (Faz 2) | günlük 08:00 | Yaklaşan/geciken görevler |
| `ReferralCampaignJob` (Faz 3) | günlük 00:05 | Kampanya başlat/bitir |

İş günü hesabı `IBusinessCalendar` arayüzüyle (hafta sonu + resmi tatil tablosu).

## 10. Dış servis arayüzleri

Hepsi Application'da arayüz, Infrastructure'da uygulama. Uygulamayı değiştirmek başka hiçbir kodu etkilememeli.

- `IEmailSender.SendAsync(EmailMessage msg, ct)` → `SmtpEmailSender` (MailKit). Ayarlar `Smtp` bölümünden; değerleri kullanıcı manuel girer.
- `IEmailTemplateRenderer.Render(code, model)` → `EmailTemplate` tablosundan, `{Degisken}` yer değiştirme.
- `ICalendarService` → Faz 1 `InternalAvailabilityCalendarService`; Faz 2 Exchange Online için Microsoft Graph, şirket içi Exchange için EWS (hangisi olduğu açık karar).
- `IIcsGenerator` → mülakat davetleri için `.ics`.
- `IFileStorage.Save/Open/Delete/GetSignedUrl` → `LocalFileStorage` (kök yol `FileStorage:RootPath`); ileride MinIO/S3.
- `ITextExtractor` → PDF/DOCX'ten arama metni çıkarma.
- `IAiService` → Faz 3; şimdi yalnızca arayüz bile yazma, ihtiyaç olunca eklenir.

## 11. API katmanı

- Controller'lar ince: girdi al → `dispatcher.Send/Query` → `result.ToActionResult()`.
- Rota: `/api/v1/<kaynak>` çoğul, kebab-case: `/api/v1/candidates/{id}/screenings`, `/api/v1/blacklist-entries`.
- Her action'da `[Authorize(Policy = Policies.X)]`; `[AllowAnonymous]` sadece `/api/v1/auth/login` ve `/refresh`.
- Veri bazlı yetki (TeamLead'in sadece kendine önerilenleri görmesi) handler'da `ICurrentUser` ile uygulanır, controller'da değil.
- Hassas alanlar (maaş, kara liste, IK yorumu) rol kontrolüyle DTO'ya **hiç konmaz**.
- Dosya yükleme `multipart/form-data`, boyut ve uzantı kontrolü + içerik tipi (magic bytes) kontrolü.
- OpenAPI: .NET 10'un yerleşik `Microsoft.AspNetCore.OpenApi` + geliştirmede Scalar arayüzü.
- SignalR hub `/hubs/notifications`, JWT query string `access_token` ile.
- CORS sadece frontend origin'ine.

## 12. Konfigürasyon (appsettings.json)

Gizli değerler boş bırakılır; geliştirmede `dotnet user-secrets`, sunucuda ortam değişkeni.

```json
{
  "ConnectionStrings": {
    "Default": "Host=localhost;Port=5432;Database=recruitment;Username=recruitment;Password="
  },
  "Ldap": {
    "Host": "",
    "Port": 389,
    "UseSsl": false,
    "Domain": "",
    "BaseDn": "",
    "BindUser": "",
    "BindPassword": "",
    "UserSearchFilter": "(sAMAccountName={0})",
    "TimeoutSeconds": 10
  },
  "Jwt": {
    "Issuer": "recruitment-api",
    "Audience": "recruitment-web",
    "SigningKey": "",
    "AccessTokenMinutes": 15,
    "RefreshTokenDays": 7
  },
  "Smtp": {
    "Host": "",
    "Port": 587,
    "UseSsl": true,
    "Username": "",
    "Password": "",
    "FromAddress": "",
    "FromName": "IK İşe Alım"
  },
  "FileStorage": {
    "RootPath": "storage",
    "MaxFileSizeMb": 10,
    "AllowedExtensions": [ ".pdf", ".doc", ".docx" ],
    "SignedUrlMinutes": 5
  },
  "Recruitment": {
    "CvReviewSlaBusinessDays": 3,
    "CvReviewEscalationBusinessDays": 2,
    "OfferApprovalThreshold": 0,
    "FrontendBaseUrl": "http://localhost:5173"
  },
  "Cors": { "AllowedOrigins": [ "http://localhost:5173" ] }
}
```

Ayarlar `IOptions<T>` ile, başlangıçta `ValidateDataAnnotations().ValidateOnStart()` yapılır (Smtp hariç: SMTP boşsa e-postalar loglanır ama gönderilmez, uygulama açılır).

## 13. Loglama

- Serilog, yapılandırılmış log: `logger.LogInformation("Aday {CandidateId} kara listeye alındı", id)`.
- Her istekte `CorrelationId` ve kullanıcı adı log context'ine eklenir.
- Kişisel veri (CV içeriği, telefon, maaş) ve şifre/token **loglanmaz**.

## 14. Test

- **Domain.Tests:** durum geçişleri ve iş kuralları (her `DomainException` dalı).
- **Application.Tests:** handler'lar; dış servisler NSubstitute ile, DB için gerekirse EF InMemory yerine SQLite değil, integration test tercih et.
- **Api.IntegrationTests:** `WebApplicationFactory` + `Testcontainers.PostgreSql`; LDAP sahte `ILdapService` ile değiştirilir; yetki politikaları (özellikle TeamLead veri kısıtı, bağımsız değerlendirme, Referrer token kısıtı) mutlaka test edilir.
- Test isimleri: `Metot_Durum_BeklenenSonuc` (`Approve_WhenNotPending_Throws`).

## 15. Kodlama standartları

- `Nullable` ve `ImplicitUsings` açık, `TreatWarningsAsErrors` açık, `.editorconfig` repoda.
- File-scoped namespace, primary constructor (DI için), `sealed` sınıflar varsayılan.
- Async metotlar `Async` son eki almaz (handler `Handle`), ama `CancellationToken` her zaman son parametredir ve aşağı iletilir.
- Magic string yok: politika adları `Policies`, hata kodları ilgili `Errors` sınıfında sabit.
- Bir sınıf tek iş yapar (SRP); bir handler 100 satırı geçiyorsa domain'e veya yardımcı servise taşı.
