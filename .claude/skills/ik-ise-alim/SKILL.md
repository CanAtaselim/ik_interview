---
name: ik-ise-alim
description: Şirket içi IK işe alım uygulaması (kadro talebi, aday havuzu, IK ön görüşme formu, kara liste, CV önerme, mülakat planlama, soru bankası, puanlama, teklif, referans programı, onboarding) üzerinde .NET 10 + PostgreSQL backend veya React (JavaScript, TypeScript YOK) + PrimeReact frontend kodu yazarken, düzeltirken, test ederken veya tasarlarken MUTLAKA kullan. Mimari kurallar, klasör yapısı, domain kuralları, LDAP/JWT yetkilendirme ve UI ilkelerinin tek doğru kaynağı budur. Kullanıcı aday, mülakat, talep, pozisyon, kara liste, onboarding, referans çağrısı gibi bir özellikten bahsettiğinde veya bu repoda herhangi bir dosyaya dokunduğunda bu skill'i yükle.
---

# IK İşe Alım Uygulaması

Kadro talebinden işe başlayan çalışanın oryantasyonuna kadar tüm işe alım sürecini yöneten **şirket içi** uygulama. Kullanıcılar şirket çalışanlarıdır; adaylar sisteme giriş yapmaz (aday portalı kapsam dışı).

Bu dosya her işte geçerli olan kuralları ve çalışma akışını verir. Ayrıntılar `references/` altında; yalnızca o anki işe gereken dosyayı oku:

| Dosya | Ne zaman oku |
| --- | --- |
| `references/domain.md` | Herhangi bir modülün iş kuralına, entity'sine, durumuna dokunmadan önce |
| `references/backend.md` | .NET tarafında kod yazarken (katmanlar, CQRS, EF Core, outbox, job'lar, config) |
| `references/auth.md` | Giriş, LDAP, JWT, roller, yetki politikaları, referans çağrısı erişimi |
| `references/frontend.md` | React tarafında kod yazarken (yapı, PrimeReact tema, state, formlar, UX) |
| `references/glossary.md` | Türkçe iş terimini koddaki İngilizce isme çevirirken |

## Değişmez Kurallar

Bunlar kullanıcıyla netleşmiş kararlardır. Kullanıcı açıkça değiştirmeden esneme.

1. **Backend:** .NET 10, C#, Clean Architecture (Domain / Application / Infrastructure / Api), SOLID.
2. **Veritabanı:** PostgreSQL, EF Core + Npgsql, tablo ve kolonlar `snake_case`.
3. **Frontend:** React + **saf JavaScript**. `.ts`/`.tsx` dosyası, TypeScript bağımlılığı, `tsconfig` ekleme. Tip belgelemek gerekirse JSDoc kullan.
4. **UI kütüphanesi:** PrimeReact, özel tema token'larıyla modern ve tutarlı bir görünüm (varsayılan "kurumsal klasik" görünümde bırakma).
5. **Kimlik doğrulama:** LDAP (`System.DirectoryServices.Protocols`) + JWT access token + refresh token. Rol AD grubundan gelir.
6. **Lisans dikkati:** MediatR, AutoMapper ve FluentAssertions (v8+) ticari lisanslı. Kullanma. Yerine: kendi küçük dispatcher'ımız, elle mapping (extension metot), Shouldly.
7. **Kod dili İngilizce, arayüz dili Türkçe.** Sınıf/metot/tablo isimleri İngilizce (`Candidate`, `Interview`); kullanıcının gördüğü her metin, hata mesajı ve e-posta Türkçe.
8. **Kullanım kolaylığı birinci kabul kriteri.** Bir ekran karmaşıksa, işlem 3 adımdan fazlaysa sadeleştir. Ayrıntı: `references/frontend.md` → UX İlkeleri.

## Çözüm Yapısı

```
/                              (repo kökü)
├── backend/
│   ├── Recruitment.sln
│   ├── src/
│   │   ├── Recruitment.Domain/          Entity, value object, enum, domain event, iş kuralı
│   │   ├── Recruitment.Application/     Command/Query + handler, validator, DTO, arayüzler
│   │   ├── Recruitment.Infrastructure/  EF Core, LDAP, SMTP, dosya, takvim, Hangfire, outbox
│   │   └── Recruitment.Api/             Controller, auth, policy, SignalR hub, Program.cs
│   └── tests/
│       ├── Recruitment.Domain.Tests/
│       ├── Recruitment.Application.Tests/
│       └── Recruitment.Api.IntegrationTests/   (Testcontainers + PostgreSQL)
└── frontend/                     Vite + React (JS) + PrimeReact
```

Bağımlılık yönü: `Api → Infrastructure → Application → Domain`. Domain hiçbir projeye ve dış pakete bağımlı olmaz. Application sadece Domain'e bağlıdır; dış dünya (DB, LDAP, SMTP, takvim, dosya, saat) Application'da arayüzle tanımlanır, Infrastructure'da uygulanır.

## Yeni Özellik Ekleme Akışı (dikey dilim)

Bir özelliği her zaman uçtan uca, bu sırayla ekle. Her adımda ilgili referans dosyasına bak.

1. **Kuralı netleştir:** `references/domain.md`'deki modül bölümünü oku. Belirsiz bir iş kuralı varsa tahmin etme, kullanıcıya sor.
2. **Domain:** Entity / enum / durum geçişi / domain event. İş kuralı entity metodunda yaşar (`candidate.Blacklist(...)`), handler'da değil.
3. **Application:** `Features/<Modül>/<İşlem>/` altında command veya query + handler + validator + DTO.
4. **Infrastructure:** EF configuration (`IEntityTypeConfiguration<T>`), gerekiyorsa migration: `dotnet ef migrations add <Isim> -p src/Recruitment.Infrastructure -s src/Recruitment.Api`.
5. **Api:** İnce controller action + yetki politikası. İş mantığı controller'a girmez.
6. **Test:** Domain kuralları için unit test, handler için unit test, kritik akış için integration test.
7. **Frontend:** `src/features/<modül>/` altında `api.js` → TanStack Query hook'u → bileşen/sayfa → route + menü (rol kontrolüyle).
8. **Kontrol:** `dotnet build`, `dotnet test`, `npm run lint`, `npm run build` hatasız geçmeli. Arayüzü değiştirdiysen ekranı açıp bak.

## Fazlar

Kullanıcı aksini söylemedikçe yalnızca aktif fazın modüllerini geliştir; ileri faz için şimdiden kod yazma, sadece genişlemeye açık arayüz bırak.

- **Faz 1 (MVP):** Roller + LDAP/JWT, kadro talebi + onay akışı, pozisyon, aday havuzu + IK ön görüşme formu + kara liste, süreç hattı (kanban), CV önerme ve değerlendirme, mülakat planlama (sistem içi müsaitlik), soru bankası, değerlendirme ve puanlama, bildirimler (uygulama içi + SMTP), audit log, kullanılabilirlik ilkeleri.
- **Faz 2:** Teklif yönetimi, onboarding (birimlere e-posta ile görev), Outlook/Exchange takvim entegrasyonu, raporlama ve dashboard.
- **Faz 3 (en son):** Çalışan referans programı, referans kontrolü, case/ödev, Jira otomatik ticket, AI destekleri (sağlayıcı belli değil; şu an aksiyon yok).
- **Bekleyen:** Kariyer.net / LinkedIn entegrasyonu (IK kararı bekleniyor).
- **Kapsam dışı:** Aday portalı.

## Açık Kararlar (karşılaşırsan kullanıcıya sor, tahmin etme)

- KVKK: saklama süresi, aydınlatma, kara liste kayıtları → hukuk birimiyle konuşulacak. O gelene kadar silme/anonimleştirme job'ı yazma, ama veri modelini buna engel olmayacak şekilde kur.
- Exchange Online (Graph API) mı, şirket içi Exchange Server (EWS) mı?
- Onboarding görevini tamamlayacak, yönetici olmayan IT / İdari İşler personeli hangi AD grubuyla girecek?
- AI sağlayıcısı (Faz 3).

## Çalışma Tarzı

- Kod yazmadan önce mevcut dosyalara bak; var olan kalıbı tekrar et, paralel ikinci bir kalıp icat etme.
- Bir değişiklik birden fazla katmana dokunuyorsa önce kısa planı söyle, sonra uygula.
- Yeni NuGet/npm paketi eklemeden önce gerçekten gerekli mi düşün; eklersen nedenini söyle ve lisansını kontrol et.
- Gizli bilgileri (LDAP şifresi, JWT anahtarı, SMTP şifresi) koda veya repoya yazma; `appsettings.json`'da boş alan bırak, geliştirmede `dotnet user-secrets` kullan.
- Bu skill'deki bir kural kullanıcının yeni kararıyla çelişirse kullanıcının kararını uygula ve skill dosyasını güncellemeyi öner.
