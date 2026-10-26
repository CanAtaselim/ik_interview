# Domain Kuralları

Her modülün entity'leri, durumları ve iş kuralları. Kodda isimler İngilizce (bkz. `glossary.md`), açıklamalar Türkçe.

## İçindekiler
1. Ortak kurallar
2. Kullanıcı ve erişim modeli
3. Kadro talebi (HiringRequest)
4. Pozisyon (Position)
5. Aday (Candidate) ve IK ön görüşme formu (Screening)
6. Kara liste (BlacklistEntry)
7. Süreç hattı (Application + PipelineStage)
8. CV önerme ve değerlendirme (CvReview)
9. Mülakat planlama (Interview)
10. Soru bankası (Question, InterviewTemplate)
11. Değerlendirme ve puanlama (Scorecard)
12. Teklif (Offer) — Faz 2
13. Onboarding — Faz 2
14. Çalışan referans programı (ReferralCampaign) — Faz 3
15. Referans kontrolü (ReferenceCheck) — Faz 3
16. Case / ödev — Faz 3
17. Bildirimler, sözlükler, audit

---

## 1. Ortak kurallar

- Tüm entity'ler `Entity<Guid>`'dan türer: `Id`, `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` (audit interceptor doldurur).
- Silme **soft delete**'tir (`IsDeleted`, `DeletedAt`, `DeletedBy`); global query filter ile gizlenir.
- Zamanlar veritabanında UTC (`timestamptz`). Arayüzde `Europe/Istanbul` gösterilir. Saat için `TimeProvider` enjekte edilir, `DateTime.Now` kullanılmaz.
- Para: `Money` value object (`Amount` decimal, `Currency` "TRY" varsayılan, `IsNet` bool).
- Durum değişiklikleri entity metoduyla yapılır; geçersiz geçiş `DomainException` fırlatır (Türkçe mesaj). Setter'lar `private`.
- Her önemli durum değişikliği bir domain event üretir (bildirim, e-posta, zaman çizelgesi bu event'lerden beslenir).
- "Sözlük" değerleri (red nedeni, kaynak, seviye vb.) ayrı tabloda tutulur, admin ekranından yönetilir; koda enum olarak gömülmez. Akış mantığını belirleyen durumlar (status) ise enum'dur.

## 2. Kullanıcı ve erişim modeli

`User`: `Id`, `Username` (sAMAccountName), `DisplayName`, `Email`, `Department`, `Title`, `ManagerUsername`, `Roles` (çoka-çok), `IsActive`, `LastLoginAt`. İlk LDAP girişinde otomatik oluşur, her girişte LDAP'tan güncellenir.

Roller (`Role` enum + AD grup eşlemesi `RoleGroupMapping` tablosunda):

| Rol | Kod | Kapsam |
| --- | --- | --- |
| Sistem Admin | `SystemAdmin` | Ayarlar, sözlükler, akış tanımları, grup-rol eşlemesi |
| IK Yöneticisi | `HrManager` | Her şey; talep/teklif onayı, referans çağrısı, kara liste onay/çıkarma |
| IK Uzmanı | `HrSpecialist` | Aday, ön görüşme, CV önerme, mülakat planlama, teklif hazırlama, onboarding başlatma |
| Takım Lideri / Mülakatçı | `TeamLead` | Talep açma, kendine önerilen CV'ler, mülakat puanlama, kendi biriminin soru bankası |
| Departman Yöneticisi | `DepartmentManager` | Kendi biriminin talep/teklif onayı, birim raporları |
| Yönetim | `Executive` | Salt okunur dashboard ve raporlar |
| Birim Sorumlusu | `TaskOwner` | Kendisine atanan onboarding görevleri (Faz 2) |
| Çalışan (referans) | `Referrer` | Sadece Aday Öner sayfası (Faz 3) |

**Giriş kuralı (sırayla):**
1. Kullanıcının AD gruplarından en az bir tam rol çıkıyorsa → tam uygulama, rolüne göre menü.
2. Değilse ve aktif bir referans çağrısı varsa → yalnızca `Referrer` rolüyle kısıtlı token.
3. Değilse ve kullanıcının daha önce yaptığı öneriler varsa → `Referrer` rolüyle salt okunur token (sadece "Önerilerim").
4. Hiçbiri değilse → giriş reddedilir: "Şu anda aktif bir referans çağrısı bulunmuyor."

**Veri bazlı yetki:**
- `TeamLead` yalnızca kendisine veya birimine önerilmiş adayları/CV'leri görür.
- Maaş beklentisi, mevcut ücret, teklif tutarı, kara liste bilgisi ve IK yorumu `TeamLead` ve `Executive`'den gizlenir (DTO'da hiç gönderilmez, frontend'de gizlemek yetmez).
- Bir kullanıcı birden çok role sahip olabilir; yetkiler birleşir.

## 3. Kadro talebi (HiringRequest)

Alanlar: `Title` (pozisyon adı), `DepartmentId`, `LevelId` (L1/L2/L3/L4 sözlüğü; kıdem pozisyon adında, ör. "Kıdemli Yazılım Geliştirme Uzmanı" + L2), `Headcount` (≥1), `Reason` (`NewHeadcount` | `Replacement`), `ReplacedEmployeeName` (Replacement ise zorunlu), `Urgency` (`Low` | `Normal` | `High`), `ExpectedStartDate`, `Competencies` (etiketler), `Description`, `RequestedBy`.

Durumlar: `Draft → PendingApproval → Approved | Rejected`, her durumdan `Cancelled`.

Kurallar:
- Onay akışı `ApprovalFlowDefinition`'dan gelir (varsayılan: Departman Yöneticisi → IK Yöneticisi). Her adım `ApprovalStep` kaydı: onaylayan, karar, not, tarih.
- Ret gerekçesi zorunlu; talep sahibine bildirim gider.
- Son onayda `HiringRequestApproved` event'i → otomatik `Position` oluşur.
- Talep sahibi yalnızca `Draft`'ta düzenleyebilir.

## 4. Pozisyon (Position)

Alanlar: `HiringRequestId` (opsiyonel; IK talepsiz pozisyon açabilir), `Title`, `DepartmentId`, `LevelId`, `Headcount`, `FilledCount`, `ResponsibleHrUserId`, `InterviewPanel` (kullanıcılar), `PipelineTemplateId`, `InterviewTemplateIds`, `JobDescription`.

Durumlar: `Open`, `OnHold`, `Closed`, `Cancelled`.

Kurallar:
- `FilledCount` teklif kabulünde artar; `FilledCount == Headcount` olunca pozisyon `Closed` önerilir (otomatik kapatma değil, IK'ya bildirim).
- Kapalı pozisyona aday bağlanamaz.

## 5. Aday (Candidate) ve IK ön görüşme formu (Screening)

**Candidate** — kişinin kendisi, tek kayıt:
- Zorunlu olan **yalnızca** `FirstName`, `LastName` ve en az bir iletişim (`Email` veya `Phone`). Geri kalan her şey opsiyonel.
- Opsiyonel: `LinkedInUrl`, `GitHubUrl`, `City`, `SourceId` (sözlük), `ReferredByUserId`, `Tags`, `InTalentPool` (bool), `Notes`.
- `CvFiles`: birden çok `CandidateDocument` (dosya adı, içerik tipi, boyut, depolama anahtarı, yükleyen, tarih, `ExtractedText` arama için). İzin verilen: PDF, DOC, DOCX; en fazla 10 MB.
- Aday **pozisyonsuz** eklenebilir (genel havuz). Pozisyona bağlanınca bir `Application` oluşur.

**Mükerrer kontrolü:** Kayıt ve güncellemede e-posta (küçük harf, trim) veya telefon (sadece rakam, son 10 hane) eşleşirse uyarı + mevcut aday kartına link. Engellemez; IK "yine de ekle" diyebilir (audit'e yazılır).

**Arama:** `search_vector` (tsvector, `turkish` config) ad, e-posta, etiketler, ön görüşme metinleri ve `ExtractedText` üzerinden; GIN index.

**Screening (IK Ön Görüşme Formu)** — bir adayın birden çok kaydı olabilir. **Tüm alanlar opsiyoneldir**; IK adayı hiç görüşmeden ekleyebilir, formu istediği zaman, istediği kadar doldurur. Form taslak olarak otomatik kaydedilir.

| Alan | Kod | Tip |
| --- | --- | --- |
| Görüşme şekli | `Channel` | enum `Phone` / `InPerson` / `Teams` |
| Görüşme tarihi | `InterviewedAt` | datetime |
| Görüşmeyi yapan | `InterviewerUserId` | varsayılan: giriş yapan IK |
| Eğitim | `Educations` | liste: okul, bölüm, derece, mezuniyet yılı |
| Yabancı Dil | `Languages` | liste: dil, seviye (`Beginner`/`Intermediate`/`Advanced`/`Native`) |
| Beşeri Yetkinlik | `SoftSkills` | metin |
| Teknik Yetkinlik | `TechnicalSkills` | etiket listesi + metin |
| Deneyim Özeti | `ExperienceSummary` | metin |
| Motivasyonu | `Motivation` | metin |
| Toplam İş Deneyimi | `TotalExperienceYears` | decimal(4,1) |
| Toplam Alan Deneyimi | `FieldExperienceYears` | decimal(4,1) |
| Ücret Beklentisi | `ExpectedSalary` | Money |
| İhbar Süresi | `NoticePeriodWeeks` | int |
| İK Yorumu | `HrComment` | metin |
| Mevcut ücret ve yan haklar | `CurrentSalary`, `CurrentBenefits` | Money, metin |
| Çalışma şekli tercihi | `WorkModel` | `Office` / `Hybrid` / `Remote` |
| Lokasyon / taşınma | `Location`, `OpenToRelocation` | metin, bool? |
| Devam eden diğer süreçler | `OtherProcesses` | metin |
| Askerlik durumu | `MilitaryStatus` | `Done` / `Postponed` / `Exempt` / `NotApplicable` |
| Genel puan | `OverallRating` | 1–5, opsiyonel |

Görüşme sonrası IK'nın tek buton aksiyonları (aday kartında):
- **Süreci sonlandır** → red nedeni (sözlük) zorunlu, not opsiyonel. İsteğe bağlı "kara liste talebi aç".
- **Havuzda beklet** → `InTalentPool = true`, etiket önerilir.
- **Takım liderine öner** → pozisyon seçerek veya pozisyonsuz birime; ön görüşme formu öneriyle birlikte gider (maaş alanları hariç).

**Zaman çizelgesi (`TimelineEntry`)**: aday üzerindeki her olay (eklendi, görüşme, öneri, karar, mülakat, puan, aşama değişikliği, e-posta) kim/ne zaman/özet olarak. Domain event handler'ları yazar; elle yazılmaz.

## 6. Kara liste (BlacklistEntry)

Alanlar: `CandidateId`, `ReasonId` (sözlük, zorunlu), `Description` (zorunlu), `ExpiresAt` (null = kalıcı), `Status` (`PendingApproval` / `Active` / `Expired` / `Removed`), `RequestedBy`, `ApprovedBy`, `RemovedBy`, `RemovalReason`.

Varsayılan nedenler (sözlük seed): mülakata haber vermeden gelmedi, teklifi kabul edip işe başlamadı, yanlış/yanıltıcı bilgi verdi, etik olmayan davranış, disiplin nedeniyle ayrılmış eski çalışan, diğer.

Kurallar:
- `HrSpecialist` talep eder (`PendingApproval`), `HrManager` onaylar (`Active`). `HrManager` doğrudan `Active` ekleyebilir.
- Çıkarma yalnızca `HrManager`, gerekçe zorunlu.
- `ExpiresAt` geçince günlük job `Expired` yapar.
- Eşleştirme: e-posta, telefon (mükerrer kuralıyla aynı normalizasyon) ve ad-soyad benzerliği (`pg_trgm`, eşik 0.6 → sadece "olası eşleşme" uyarısı).
- Uyarı noktaları: aday ekleme, CV önerme, mülakat planlama, referans önerisi. Uyarı engellemez ama IK'nın onaylaması gerekir ve audit'e yazılır.
- Görünürlük: kayıt ve neden yalnızca `HrSpecialist`, `HrManager`, `SystemAdmin`. Öneren çalışana sadece "öneriniz değerlendirmeye alınmadı" gösterilir.
- Ekleme, çıkarma ve **görüntüleme** audit'e yazılır.

## 7. Süreç hattı (Application + PipelineStage)

`Application` = bir adayın bir pozisyondaki süreci. Bir aday aynı anda birden çok pozisyonda süreçte olabilir; aynı pozisyonda yalnızca bir aktif `Application`.

`PipelineTemplate` → sıralı `PipelineStageDefinition` listesi (admin'den konfigüre). Varsayılan şablon:

`Applied → HrScreening → CvReview → TechnicalInterview → CaseStudy (ops.) → FinalInterview → ReferenceCheck (ops.) → Offer → Hired`

Terminal durumlar: `Hired`, `Rejected`, `Withdrawn`. `Rejected`/`Withdrawn` her aşamadan seçilebilir; red nedeni zorunlu.

Kurallar (State pattern — her aşama `IStageState` ile kendi geçiş koşulunu bilir):
- İleri geçiş yalnızca sıradaki aşamaya veya opsiyonel aşamaları atlayarak yapılır.
- `CvReview`'dan çıkış için en az bir `Positive` CvReview kararı gerekir.
- Mülakat aşamalarından çıkış için o aşamadaki tüm atanmış mülakatçıların değerlendirmesi gönderilmiş olmalı (IK Yöneticisi gerekçeyle bypass edebilir, audit'e yazılır).
- Geri alma: son aşama değişikliği 10 dakika içinde "geri al" ile iptal edilebilir.
- Her geçiş `ApplicationStageChanged` event'i üretir.

## 8. CV önerme ve değerlendirme (CvReview)

Alanlar: `CandidateId`, `ApplicationId` (ops.), `DepartmentId` (ops.), `ReviewerUserId`, `SuggestedBy`, `SuggestionNote`, `Decision` (`Pending` / `Positive` / `Negative` / `OnHold`), `DecisionReason`, `RejectionReasonId`, `DueAt`, `DecidedAt`.

Kurallar:
- Bir aday aynı anda birden çok lidere önerilebilir.
- Karar verirken gerekçe zorunlu; `Negative` ise red nedeni sözlükten zorunlu.
- `DueAt` = öneri + SLA (varsayılan 3 iş günü, ayardan). Geçince lidere hatırlatma, 2 gün daha geçince IK'ya gecikme bildirimi (Hangfire job).
- Karar verildikten sonra değiştirilemez; IK yeniden öneri açabilir.
- `Positive` kararda IK'ya "mülakat planla" kısayolu bildirimi gider.

## 9. Mülakat planlama (Interview)

Alanlar: `ApplicationId`, `TypeId` (sözlük: Telefon, Teknik, Case sunumu, Yönetici, Kültür uyumu), `Mode` (`InPerson` / `Online`), `StartsAt`, `DurationMinutes`, `RoomId` (yüz yüze), `MeetingUrl` (online), `Interviewers` (çoka-çok, en az 1), `InterviewTemplateId`, `Status` (`Scheduled` / `Completed` / `Cancelled` / `NoShow`), `CalendarEventId` (Faz 2).

Kurallar:
- Mülakatçı çakışması ve oda çakışması kontrol edilir (uyarı + IK onayı ile geçilebilir).
- Faz 1: müsaitlik `InterviewerAvailability` tablosundan. Faz 2: `ICalendarService` Outlook'tan okur ve Outlook toplantısı (Teams linkiyle) oluşturur.
- Planlama, yeniden planlama ve iptalde adaya ve mülakatçılara `.ics` ekli e-posta; 24 saat ve 1 saat önce mülakatçılara hatırlatma.
- `NoShow` işaretlenince IK'ya "kara liste talebi aç" önerisi gösterilir.
- Planlanınca pozisyonun mülakat şablonu otomatik bağlanır, her mülakatçı için boş `Scorecard` oluşur.

## 10. Soru bankası (Question, InterviewTemplate)

`Question`: `DepartmentId`, `PositionTitle` (ops.), `LevelId` (ops.), `CompetencyId` (sözlük), `Type` (`Technical` / `Behavioral` / `Case`), `Difficulty` (1–3), `Weight` (varsayılan 1), `Text`, `Guidance` (beklenen cevap/ipuçları), `IsActive`, `Version`.

Kurallar:
- **Versiyonlama:** Soru düzenlenince yeni `QuestionVersion` oluşur; mülakatlar soru metninin o anki halini (`ScorecardItem.QuestionTextSnapshot`) saklar.
- `TeamLead` yalnızca kendi biriminin sorularını yönetir; IK hepsini görür.
- Kullanılan soru silinmez, `IsActive = false` yapılır.

`InterviewTemplate`: ad, pozisyon/seviye/mülakat tipi, sıralı soru listesi. Mülakatçı mülakat sırasında şablona soru ekleyip çıkarabilir (yalnızca o scorecard'ı etkiler).

## 11. Değerlendirme ve puanlama (Scorecard)

`Scorecard`: `InterviewId`, `InterviewerUserId`, `Items` (`ScorecardItem`: soru snapshot, ağırlık, `Score` 1–5 ops., `Note`), `GeneralNote`, `Recommendation` (`StrongHire` / `Hire` / `Undecided` / `NoHire` / `StrongNoHire`), `Status` (`Draft` / `Submitted`), `SubmittedAt`.

Rubrik (sabit, arayüzde her puanın yanında gösterilir): 1 Yetersiz, 2 Beklentinin altında, 3 Beklentiyi karşılıyor, 4 Beklentinin üstünde, 5 Mükemmel.

Kurallar:
- **Bağımsız değerlendirme:** Mülakatçı kendi scorecard'ını `Submitted` yapmadan aynı mülakatın diğer scorecard'larını göremez (API seviyesinde uygulanır). IK hepsini görür.
- `Submit` için `Recommendation` zorunlu ve en az bir puanlı madde gerekli.
- `Submitted` kilitlidir. Düzeltme yalnızca IK Yöneticisi tarafından gerekçeyle açılır, audit'e yazılır.
- Skor hesabı **Strategy pattern** ile (`IScoreCalculator`): varsayılan `WeightedAverageCalculator` = Σ(puan × ağırlık) / Σ(ağırlık), puansız maddeler hariç. Yetkinlik bazlı ortalama da hesaplanır.
- **Debrief:** Tüm scorecard'lar gönderilince IK ortak kararı (`DebriefDecision` + gerekçe) kaydeder.
- Karşılaştırma ekranı: aynı pozisyondaki adaylar yetkinlik skorlarıyla yan yana.

## 12. Teklif (Offer) — Faz 2

Alanlar: `ApplicationId`, `Salary` (Money), `Benefits`, `JobTitle`, `StartDate`, `Status` (`Draft` / `PendingApproval` / `Approved` / `Sent` / `Accepted` / `Rejected` / `CounterOffered` / `Cancelled`), `ApprovalSteps`, `CounterOfferNote`.

Kurallar:
- Tutar `OfferApprovalThreshold` ayarını aşarsa ek onay adımı eklenir.
- Teklif mektubu şablondan PDF üretilir.
- `Accepted` → `Position.FilledCount++`, `Application` → `Hired`, `OfferAccepted` event → onboarding açılır.

## 13. Onboarding — Faz 2

`OnboardingProcess`: `ApplicationId`, `EmployeeName`, `StartDate`, `TeamLeadUserId`, `Status`.
`OnboardingTask`: `Title`, `ResponsibleUnitId` (IT, İdari İşler, IK, Takım Lideri…), `AssigneeUserId` (ops.), `DueAt` (başlama tarihine göre göreli, iş günü), `Status` (`Pending` / `Done` / `Overdue`), `ResultNote` (ör. açılan e-posta adresi), `CompletedBy`.

Kurallar:
- `OfferAccepted` event'inde pozisyona/birime uygun `OnboardingTemplate` seçilir, görevler oluşur, her sorumlu birime **e-posta** gider (birimin e-posta adresi `Unit` sözlüğünde). Jira entegrasyonu Faz 3.
- Termine 1 gün kala ve gecikince birime ve IK'ya hatırlatma.
- Varsayılan şablon görevleri: e-posta + AD hesabı (IT, -3 iş günü), Jira/Confluence hesabı (IT, -2), bilgisayar/ekipman (IT, -2), VPN ve erişimler (IT, -1), giriş kartı/masa (İdari İşler, -1), özlük evrakı/SGK (IK, 0), buddy ataması (Takım Lideri, -5), ilk hafta oryantasyonu (IK + Takım Lideri, +5), ilk ay görüşmesi (Takım Lideri, +30 gün).

## 14. Çalışan referans programı (ReferralCampaign) — Faz 3

`ReferralCampaign`: `Title`, `PositionIds` (boş = tüm açık pozisyonlar), `BonusAmount` (Money), `BonusCondition` (metin, ör. "deneme süresini tamamlarsa"), `StartsAt`, `EndsAt` (zorunlu), `Status` (`Draft` / `Active` / `Ended` / `Cancelled`).

`Referral`: `CampaignId`, `ReferrerUserId`, `CandidateId`, `PositionId`, `Note`, `IsDuplicate`, `BonusStatus` (`NotEligible` / `Pending` / `Eligible` / `Paid`).

Kurallar:
- Yalnızca `HrManager` yayınlar; yayınlanınca tüm çalışanlara giriş linkli duyuru e-postası.
- `StartsAt ≤ şimdi < EndsAt` ise kampanya aktiftir; IK uzatabilir veya erken kapatabilir; aynı anda birden çok aktif kampanya olabilir.
- Öneri formu: ad, soyad, e-posta/telefon, CV, pozisyon, not. Aday `Source = EmployeeReferral`, `ReferredByUserId` ile oluşur. Mükerrer ve kara liste kontrolü çalışır.
- Aday zaten havuzdaysa `IsDuplicate = true`, `BonusStatus = NotEligible`; çalışana "aday zaten havuzda" gösterilir.
- Çalışan yalnızca genel durumu görür: `Değerlendiriliyor` / `Süreç devam ediyor` / `Sonuçlandı` + prim durumu. Mülakat notu, puan, red nedeni, kara liste görünmez.
- Kampanya bitince yeni öneri yapılamaz; önerileri salt okunur görünmeye devam eder.
- İşe alınan önerilen adayın deneme süresi bitince IK'ya prim hatırlatması.

## 15. Referans kontrolü (ReferenceCheck) — Faz 3

`ReferenceContact`: `CandidateId`, ad soyad, şirket, unvan, ilişki (`Manager` / `Colleague` / `Other`), birlikte çalışma süresi, iletişim, `CandidateConsentToContactCurrentEmployer` (bool).
`ReferenceCheck`: şablon soruları ve cevapları, `Result` (`Positive` / `Reserved` / `Negative` / `Unreachable`), görüşmeyi yapan IK, tarih.

Kurallar: pozisyon bazında zorunlu/opsiyonel; mevcut işveren onay olmadan aranmaz; notları yalnızca IK görür; sonuç teklif ekranında gösterilir.

## 16. Case / ödev — Faz 3

`CaseTemplate` (pozisyona bağlı açıklama + ekler), `CaseAssignment` (gönderim, son teslim, teslim dosyası/linki, rubrikle scorecard). Teslim tarihi geçerse IK'ya bildirim.

## 17. Bildirimler, sözlükler, audit

- **Bildirim:** `Notification` (alıcı, tip, başlık, link, okundu). Domain event handler'ları oluşturur, SignalR ile anlık iter. Kullanıcı tercihleri: hangi tipler e-postayla da gelsin.
- **E-posta şablonları:** `EmailTemplate` (kod, konu, gövde HTML, değişkenler `{AdayAdi}`, `{Pozisyon}`, `{MulakatTarihi}`…). Gönderim outbox üzerinden; adaya giden her e-posta zaman çizelgesine yazılır.
- **Sözlükler:** Department, Level, Competency, RejectionReason, BlacklistReason, CandidateSource, InterviewType, LanguageLevel, Unit (onboarding birimleri + e-posta), Room.
- **Audit:** `AuditLog` (kullanıcı, zaman, entity tipi, id, işlem, eski değer JSON, yeni değer JSON). EF interceptor otomatik yazar. Ayrıca `AccessLog`: CV görüntüleme/indirme ve kara liste görüntüleme.
- **Dosyalar:** yalnızca yetkili kullanıcıya, kısa süreli (5 dk) imzalı indirme linkiyle.
