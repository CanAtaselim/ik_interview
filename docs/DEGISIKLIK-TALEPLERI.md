# Geliştirme ve Değişiklik Talepleri

İK biriminden gelen 21 talep, 10 modül altında. Durumlar: ⬜ bekliyor · 🔄 devam ediyor · ✅ tamamlandı.
Talep tipleri: **Hata** (çalışmıyor) · **Değişiklik** (mevcut davranış güncellenir) · **Yeni** (yeni özellik) · **Kaldırma** (ekran/menü kalkar).

Bir talep tamamlandığında bu dosyadaki durumu ve `.claude/skills/ik-ise-alim/references/domain.md` içindeki ilgili "Bekleyen değişiklik" notunu güncelle (not kalıcı kurala dönüştürülür). Veritabanı şeması değişen taleplerde `docs/sql/` altına idempotent script de eklenir.

| No | Modül | Tip | Talep | Durum |
| --- | --- | --- | --- | --- |
| CR-01 | Genel | Değişiklik | Telefon numarası alanları 10 hane olacak şekilde doğrulanacak. | ✅ |
| CR-02 | Yönetim | Hata | Dosya Türü başta olmak üzere tüm yönetim (sözlük) ekranlarında "ekle" butonu pasif görünüyor ve tıklanmıyor. | ✅ |
| CR-03 | Yönetim | Yeni | Yabancı dil tanımı: yönetim panelinden yeni dil eklenir; dil seçimi çoktan seçmeli olur. | ✅ |
| CR-04 | Yönetim | Kaldırma | Resmi Tatiller ekranı tamamen kaldırılır. | ✅ |
| CR-05 | Yönetim | Kaldırma | Kara Liste menüsü ve ekranı kaldırılır. Adayı kara listeye ekleme devam eder; kara listedeki adaylar Aday listesinden görülür. | ✅ |
| CR-06 | Kadro talebi | Değişiklik | Seviye alanı zorunlu olmaz. | ✅ |
| CR-07 | Pozisyon | Değişiklik | Pozisyon eklerken Pozisyon Adı serbest metin yerine listeden seçilir. | ✅ |
| CR-08 | Adaylar | Değişiklik | Süreci olumlu sonuçlanan adaylar listede yeşil görünür. | ✅ |
| CR-09 | Ön görüşme | Değişiklik | Görüşme tarihi ve saati otomatik dolu gelir; geçmiş tarihli giriş de yapılabilir. | ✅ |
| CR-10 | Ön görüşme | Değişiklik | İşlem yapanların adının yanında unvanı görünür. | ✅ |
| CR-11 | Ön görüşme | Değişiklik | Yabancı dil listeden seçilir. Varsayılan: İngilizce, Almanca, Fransızca; diğerleri yönetim panelinden (CR-03). | ✅ |
| CR-12 | Ön görüşme | Yeni | Engel durumu alanı: Var / Yok. "Var" seçilirse açıklama doldurulur. | ⬜ |
| CR-13 | Mülakat | Yeni | Mülakat planlanırken birden fazla mülakatçı seçilebilir (ör. 1 İK + 1 Takım Lideri). | ⬜ |
| CR-14 | Mülakat | Yeni | Her mülakatçı kendi rolüne özel değerlendirme ekranını ve sorularını görür: Teknik mülakatçı/Takım Lideri → seçilen birime göre teknik sorular; İK → İK değerlendirme soruları + yorum alanı. | ⬜ |
| CR-15 | Mülakat | Yeni | İK, teknik mülakat sırasında kendi değerlendirmesini ve yorumunu girebilir (şu an yalnızca teknik mülakatçı girebiliyor, İK giremiyor). | ⬜ |
| CR-16 | Bildirim (mail) | Yeni | Mail şablonlarına "Olumsuz" ve "Beklemede / Süreç devam ediyor" şablonları eklenir. | ⬜ |
| CR-17 | Bildirim (mail) | Yeni | Aday olumsuz işaretlenince olumsuz maili otomatik gönderilir. | ⬜ |
| CR-18 | Bildirim (mail) | Yeni | İK, butonla adaya sürecin devam ettiğini (Beklemede şablonu) manuel bildirebilir. | ⬜ |
| CR-19 | Raporlar | Yeni | Raporlara filtre eklenir. | ⬜ |
| CR-20 | Raporlar | Yeni | Her ekranda o ekrana ait rapor butonları bulunur. | ⬜ |
| CR-21 | Bekleyen işlemlerim | Değişiklik | Liste yeniden eskiye (en yeni üstte) sıralanır. | ✅ |

## Mevcut kurallarla etkileşim ve açık sorular

Uygulamaya başlamadan önce aşağıdakiler netleşmeli veya şu varsayımla ilerlenmeli.

- **CR-01 (✅):** Doğrulama `ContactNormalizer.IsValidPhone` (backend) ve `isValidPhone` (frontend) ile yapılıyor; saklanan değer kullanıcının yazdığı biçimde kalır. Önceki kayıtlar değişmedi.
- **CR-01 (eski not):** Mükerrer kontrolü zaten telefonun son 10 hanesini karşılaştırıyor (`domain.md` §5). Doğrulama: rakam dışı karakterler (boşluk, tire, parantez) temizlenir, baştaki `0` ve `+90` atılır, kalan tam 10 hane olmalı. Uygulanacak yerler: aday, kullanıcı/mülakatçı iletişim ve tüm form alanları (frontend + backend validator). Var olan kayıtlar için veri temizliği gerekebilir.
- **CR-02 (✅):** Kök neden: sözlük ekranında "Ekle" butonu ad yazılana kadar `disabled` idi. Buton artık hep aktif; ad boşsa kutuyu işaretler ve odaklanır. Diğer yönetim ekranlarında ekle butonu zaten aktifti.
- **CR-02 (eski not):** Önce hatanın kökünü bul (rol/yetki mi, disabled koşulu mu, ortak `DictionaryPage` bileşeni mi). Tek kök neden ise tek yerde düzeltilir; tek tek ekran yamalama.
- **CR-03 / CR-11 (✅):** `Language` sözlüğü ("Yabancı diller") eklendi; ön görüşme formunda dil bu listeden seçilir (ad olarak saklanır, `EducationLevel` ile aynı kalıp; eski serbest metinler görünmeye devam eder). Şema değişmedi. Üretimde seed çalışmadığı için `docs/sql/20261103_SeedLanguageDictionary.sql` ile üç varsayılan dil eklenir.
- **CR-03 / CR-11 (eski not):** `Language` sözlüğü eklenir (seed: İngilizce, Almanca, Fransızca). `Screening.Languages` bugün serbest dil adı tutuyor; dil sözlük kaydına `LanguageId` ile bağlanır. Mevcut serbest metinli kayıtlar için migrasyon kararı gerekir (eşleşmeyenler "Diğer: metin" olarak korunabilir). "Çoktan seçmeli": bir ön görüşmede birden çok dil, her dilin seviyesi ayrı.
- **CR-04 (✅):** Ekran, uç noktalar (`/settings/holidays`), menü ve tatil tohumlama kaldırıldı; `BusinessDays` yalnızca hafta sonlarını sayar. `holidays` tablosu ve `Holiday` varlığı geriye dönük veri için korundu (şema değişmedi).
- **CR-04 (eski not):** Tatil tablosu iş günü hesabında kullanılıyor (CV değerlendirme SLA'sı: 3 iş günü). Karar (onaylandı): ekran kalkınca SLA yalnızca hafta sonlarını sayar. Tablo ve veri silinmez, yalnızca ekran/menü/uç nokta kaldırılır.
- **CR-05 (✅):** Kara Liste menüsü, rota ve sayfa kaldırıldı. Kaydın ayrıntısı, onay ve çıkarma işlemleri aday kartından açılan `BlacklistEntryDialog` ile yapılır; bildirim bağlantısı `/candidates/{id}?blacklistEntry={entryId}` olur; dashboard kartları `Aday Listesi?blacklist=yes` adresine gider. Kara liste uç noktaları ve raporu duruyor. `SystemAdmin` aday kartına giremediği için kayıtları artık görmez (talep: kayıtlar zaten yalnızca İK içindi).
- **CR-05 (eski not):** `domain.md` §6'daki kara liste kuralları (talep→onay, çıkarma, süre aşımı job'ı, audit, görünürlük) **aynen kalır**; yalnızca ayrı menü/ekran gider. Kara liste ekleme/çıkarma aday kartından yapılır; Aday listesine "Kara listede" rozeti ve filtresi gelir. Görünürlük kuralı korunur: kayıt/neden yalnızca `HrSpecialist`, `HrManager`, `SystemAdmin`. Dashboard'daki kara liste kartları ve `lists/blacklist` raporu bu değişiklikten etkilenir; rapor Aday listesi raporunun filtresine dönüşebilir.
- **CR-06:** `HiringRequest.LevelId` ve `Position.LevelId` nullable olur (migrasyon). Seviyeye bağlı yerler (soru bankası filtresi, rapor, kart) null'a dayanıklı olmalı.
- **CR-07 (✅):** Doğrudan pozisyon açma formu ve `CreatePosition` artık `PositionTitle` listesini zorunlu kılar; düzenlemede yalnızca değiştirilen ad listeden olmalıdır (eski serbest adlar korunur).
- **CR-07 (eski not):** `PositionTitle` sözlüğü ("Pozisyon adları", yönetim panelinde) kadro talebi formunda zaten kullanılıyor. Kalan iş: doğrudan pozisyon açma formu (`PositionFormDialog`) ve `CreatePositionCommand` doğrulaması aynı listeyi kullanacak. Karar: yönetim panelinden eklenecek (onaylandı).
- **CR-08 (✅):** Süreci `Hired` olan aday satırı yeşil (`candidate-row--hired`) ve satırda "İşe alındı" rozeti var. Karar: yalnızca işe alınanlar yeşil.
- **CR-08 (eski not):** "Olumlu sonuçlanan" tanımı: önerilen = adayın en az bir `Application`'ı `Hired` (veya `Offer` kabul edildi). Satır vurgusu yalnızca renge dayanmamalı (ikon/etiket de olsun, erişilebilirlik).
- **CR-09:** `InterviewedAt` varsayılanı form açılışındaki Türkiye saati; geçmiş tarih serbest. Gelecek tarihe izin verilmesi gerekmiyor (doğrulamada engellenebilir; onay bekleniyor).
- **CR-10:** Unvan `User.Title` alanından gelir (LDAP'tan güncellenir). Zaman çizelgesi, ön görüşme ve rapor çıktılarında "Ad Soyad — Unvan" biçimi; unvan boşsa yalnızca ad.
- **CR-12:** **Engel durumu özel nitelikli kişisel veridir (KVKK, sağlık).** Açık karar: yalnızca `HrSpecialist`/`HrManager` görsün (maaş gibi `TeamLead` ve `Executive`'den DTO seviyesinde gizli), arama indeksine ve takım liderine giden öneri özetine girmesin, erişim audit'e yazılsın. Hukuk birimi yorumu `SKILL.md` Açık Kararlar'daki KVKK maddesine bağlıdır. Alanlar: `HasDisability` (bool?), `DisabilityDescription` ("Var" ise zorunlu).
- **CR-13:** Veri modeli zaten çoklu mülakatçıya uygun (`Interview.Interviewers` çoka-çok). Değişen: planlama ekranında çoklu seçim ve her mülakatçının **mülakattaki rolü** (`InterviewerRole`: `HR`, `TechnicalLead`/`TeamLead`, ileride diğerleri). Çakışma kontrolü her mülakatçı için ayrı çalışır.
- **CR-14 / CR-15:** Şablon/scorecard seçimi mülakatçının rolüne göre yapılır: `Scorecard` mülakatçı başına zaten ayrı (`domain.md` §11). Eklenecekler: (a) `InterviewTemplate`/soru için **hedef rol** (Teknik / İK); (b) Soru bankasında "İK değerlendirme soruları" kümesi ve İK'nın yorum alanı; (c) scorecard oluşturulurken mülakatçının rolüne uyan şablonun seçilmesi; (d) İK'nın teknik mülakatta scorecard girebilmesi (yetki: `HrOnly` kullanıcılar atandıkları mülakatın İK scorecard'ını doldurur). Bağımsız değerlendirme kuralı (kendi scorecard'ını göndermeden diğerlerini görememe) ve aşama çıkış kuralı ("tüm atanmış mülakatçılar gönderdi") İK katılımcıları için de geçerli kalır; İK'nın scorecard'ı eksikse aşama çıkışını bloklaması beklenen davranıştır, onay bekleniyor.
- **CR-16 – CR-18:** Yeni şablon türleri `Rejection` (Olumsuz) ve `OnHold` (Beklemede / süreç devam ediyor). Otomatik gönderim (CR-17) outbox üzerinden, `Application` `Rejected` olduğunda; **Karar:** Aday ile çalışmaya devam edilmeyecekse (ör. teknik mülakat olumsuz), yani `Application` `Rejected` olduğunda tetiklenir; `Withdrawn` (adayın çekilmesi) göndermez. Olumsuz CV kararı ve ön görüşmede "Süreci sonlandır" da `Application`'ı sonlandırdığı ölçüde aynı yoldan geçer. Çift mail gitmemesi için aday+süreç başına tek olumsuz mail. Otomatik gönderimi kapatabilen bir ayar ve IK'nın göndermeden önizlemesi önerilir (yanlışlıkla olumsuz mail gitmesin). Beklemede maili (CR-18) aday kartında/süreç ekranında buton; gönderim zaman çizelgesine ve e-posta kaydına yazılır.
- **CR-19 / CR-20:** Rapor altyapısı hazır (`ReportDocument`, `IReportRenderer`, `ReportsController`). Filtreler: mevcut liste uç noktaları zaten sorgu parametresi alıyor; Raporlar sayfasına tarih aralığı, birim, pozisyon, durum gibi filtreler eklenir. "Her ekranda rapor butonu": ekranın mevcut filtresini rapor uç noktasına taşıyan ortak bir `ReportButton` bileşeni (PDF/Excel menüsü); ekranda görünen ile rapordaki veri aynı filtreyle üretilir. Rapor erişim kuralları (`HrOnly`, dashboard `HrManagerOnly`) ekrana göre aynen korunur.
- **CR-21:** "Yeniden eskiye": en yeni kayıt üstte. Sıralama anahtarı (görev oluşma tarihi) ve sunucu/istemci tarafı sıralama belirlenmeli.

## Önerilen uygulama sırası

1. **Hızlı düzeltmeler (✅ tamamlandı):** CR-02 (hata), CR-21, CR-06, CR-10, CR-09, CR-01.
2. **Sözlük ve ekran kaldırmaları (✅ tamamlandı):** CR-03, CR-11, CR-07, CR-04, CR-05, CR-08.
3. **Ön görüşme:** CR-12 (KVKK kararından sonra).
4. **Mülakat paketi:** CR-13 → CR-14 → CR-15 (birlikte; şema değişir).
5. **Mail:** CR-16 → CR-17 → CR-18.
6. **Raporlar:** CR-19 → CR-20.
