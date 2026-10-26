# Kimlik Doğrulama ve Yetkilendirme (LDAP + JWT)

## Giriş akışı

```
POST /api/v1/auth/login { username, password }
  1. Kullanıcı adını normalize et (küçük harf, "DOMAIN\" veya "@domain" kısmını at).
  2. LdapService.Authenticate: kullanıcının kendi kimliğiyle bind → başarısızsa 401
     "Kullanıcı adı veya şifre hatalı." (hangisinin hatalı olduğunu söyleme)
  3. LdapService.GetUser: servis hesabıyla (BindUser) kullanıcıyı ara; displayName, mail,
     department, title, manager, memberOf (iç içe gruplar dahil) oku.
  4. User kaydını oluştur/güncelle (upsert), LastLoginAt yaz.
  5. Rolleri çöz: memberOf ∩ RoleGroupMapping.
  6. Rol yoksa referans kuralı (domain.md §2): aktif kampanya → Referrer;
     önerisi var → Referrer (read-only); hiçbiri → 403 "Şu anda aktif bir referans çağrısı bulunmuyor."
  7. Access token + refresh token üret, dön.
```

### LDAP uygulama notları

- `System.DirectoryServices.Protocols` (`LdapConnection`) kullan; Linux/Docker'da çalışır. `System.DirectoryServices` (Windows-only) kullanma.
- `UseSsl` true ise LDAPS (636); değilse StartTLS dene. Kimlik bilgisi düz metin gönderilmemeli; üretimde LDAPS/StartTLS zorunlu.
- Kullanıcı adını filtreye koymadan önce LDAP filtre karakterlerini kaçır (`*`, `(`, `)`, `\`, NUL) — LDAP injection.
- İç içe grup üyeliği için `memberOf:1.2.840.113556.1.4.1941:=` (LDAP_MATCHING_RULE_IN_CHAIN) kullan.
- Boş şifreyle bind'i reddet (anonim bind başarılı sayılabilir).
- Başarısız girişleri logla (kullanıcı adı + IP, şifre asla). 5 dakikada 5 başarısız denemeden sonra o kullanıcı adı için 5 dk bekleme.
- Geliştirme ortamı için `FakeLdapService` (appsettings `Ldap:UseFake=true`, sadece `Development`'ta izinli) — sabit test kullanıcıları ve rolleri.

## Token'lar

**Access token (JWT, 15 dk):**

| Claim | Değer |
| --- | --- |
| `sub` | User.Id |
| `name` | Username |
| `display_name` | DisplayName |
| `email` | Email |
| `role` | her rol için ayrı claim |
| `dept` | DepartmentId (varsa) |
| `scope` | `full` veya `referral` veya `referral-readonly` |

İmza HS256, anahtar `Jwt:SigningKey` (en az 32 byte, user-secrets/ortam değişkeni). `ClockSkew` 30 sn.

**Refresh token (7 gün):**
- Rastgele 64 byte, veritabanında **hash'i** saklanır (`refresh_tokens`: user_id, token_hash, expires_at, created_by_ip, revoked_at, replaced_by).
- Frontend'e `HttpOnly; Secure; SameSite=Strict` cookie olarak verilir (path `/api/v1/auth`).
- `POST /api/v1/auth/refresh`: rotasyon — eskisi iptal, yenisi verilir. İptal edilmiş token tekrar kullanılırsa o kullanıcının tüm refresh token'ları iptal edilir (çalınma tespiti).
- Refresh sırasında roller ve referans kampanyası durumu **yeniden** hesaplanır (kampanya bittiyse scope `referral-readonly`'e düşer, rol AD'den kaldırıldıysa düşer).
- `POST /api/v1/auth/logout`: refresh token iptal, cookie silinir.

## Yetki politikaları

`Recruitment.Api/Auth/Policies.cs` içinde sabitler; `AddAuthorization` ile tanımlanır.

| Politika | Koşul |
| --- | --- |
| `FullAccess` | `scope == full` (tüm iç endpoint'lerin varsayılanı, `FallbackPolicy` olarak ayarla) |
| `HrOnly` | HrSpecialist veya HrManager |
| `HrManagerOnly` | HrManager |
| `CanRequestHeadcount` | TeamLead, DepartmentManager, HrManager |
| `CanApproveRequest` | DepartmentManager, HrManager |
| `CanReviewCv` | TeamLead, HrSpecialist, HrManager |
| `CanScoreInterview` | Mülakata atanmış kullanıcı (resource-based, handler'da) |
| `CanManageQuestions` | TeamLead (kendi birimi), HrSpecialist, HrManager |
| `CanViewSalary` | HrSpecialist, HrManager, DepartmentManager (sadece teklif onayında) |
| `CanViewBlacklist` | HrSpecialist, HrManager, SystemAdmin |
| `CanViewReports` | HrManager, Executive, DepartmentManager (kendi birimi) |
| `SystemAdmin` | SystemAdmin |
| `ReferralSubmit` | `scope == referral` |
| `ReferralRead` | `scope in (referral, referral-readonly)` |

Kurallar:
- `FallbackPolicy = FullAccess` → `referral` token'lı biri yeni eklenen bir endpoint'e yanlışlıkla erişemez. Referans endpoint'leri (`/api/v1/referrals/...`) açıkça `ReferralSubmit`/`ReferralRead` ile işaretlenir.
- Kaynak bazlı kurallar (bu mülakata atanmış mı, bu CV bana mı önerildi, bu birim benim mi) handler'da `ICurrentUser` ile kontrol edilir; ihlal `Error.Forbidden`.
- Bağımsız değerlendirme: diğer mülakatçıların scorecard'ları, istek yapanın scorecard'ı `Submitted` değilse ve istek yapan IK değilse **sorgu seviyesinde** filtrelenir.

## ICurrentUser

```csharp
public interface ICurrentUser
{
    Guid UserId { get; }
    string Username { get; }
    IReadOnlySet<Role> Roles { get; }
    Guid? DepartmentId { get; }
    AccessScope Scope { get; }
    bool IsInRole(Role role);
    bool IsHr => IsInRole(Role.HrSpecialist) || IsInRole(Role.HrManager);
}
```

Api'de `HttpContext.User` claim'lerinden okunur; Application yalnızca bu arayüzü bilir.

## Frontend tarafı

- Access token **bellekte** (Zustand store) tutulur, `localStorage`'a yazılmaz.
- Sayfa yenilenince uygulama açılışında `/auth/refresh` çağrılır (cookie ile) → yeni access token.
- Axios interceptor: 401 gelirse tek bir refresh isteği (eşzamanlı istekler kuyruğa alınır), başarısızsa login'e yönlendir.
- `scope` `referral` / `referral-readonly` ise router yalnızca `/referral` rotalarını yükler; menü hiç render edilmez.
- Rol bazlı gizleme sadece kullanıcı deneyimi içindir; gerçek kontrol her zaman API'dedir.
