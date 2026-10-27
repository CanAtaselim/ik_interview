# Frontend Kuralları (React + JavaScript + PrimeReact)

## İçindekiler
1. Paketler
2. Klasör yapısı
3. JavaScript kuralları (TypeScript yok)
4. Tasarım dili ve PrimeReact tema
5. Sunucu verisi: axios + TanStack Query
6. Formlar
7. Tablolar, kanban, takvim
8. Routing, yetki ve menü
9. UX ilkeleri (kabul kriteri)
10. Ekran envanteri (Faz 1)

---

## 1. Paketler

| Amaç | Paket |
| --- | --- |
| Build | `vite`, `@vitejs/plugin-react` |
| UI | `primereact`, `primeicons` |
| Routing | `react-router` (v7, data router) |
| Sunucu verisi | `@tanstack/react-query`, `axios` |
| Form | `react-hook-form`, `zod`, `@hookform/resolvers` |
| Global state | `zustand` (sadece oturum, UI tercihleri; sunucu verisi değil) |
| Tarih | `dayjs` + `tr` locale + `utc`, `timezone` eklentileri |
| Kanban | `@dnd-kit/core`, `@dnd-kit/sortable` |
| Takvim | `@fullcalendar/react` + `daygrid`, `timegrid`, `interaction` |
| Anlık bildirim | `@microsoft/signalr` |
| Kalite | `eslint` (+ `eslint-plugin-react`, `react-hooks`, `jsx-a11y`), `prettier` |
| Test | `vitest`, `@testing-library/react`, `msw` |

**Yasak:** `typescript`, `@types/*`, `.ts`/`.tsx` dosyaları, `tsconfig.json`. Editör desteği için yalnızca `jsconfig.json` (path alias `@/*` → `src/*`).

## 2. Klasör yapısı (özellik bazlı)

```
frontend/
  src/
    app/
      main.jsx            PrimeReactProvider, QueryClientProvider, RouterProvider
      router.jsx          rota tanımları + rol koruması
      queryClient.js
    shared/
      api/                http.js (axios instance + interceptor), signalr.js
      auth/               authStore.js (zustand), RequireRole.jsx, useAuth.js
      components/         PageHeader, EmptyState, ConfirmButton, StatusTag, MoneyInput,
                          FileDropzone, UserPicker, DictionarySelect, Timeline ...
      layout/             AppLayout.jsx, Sidebar.jsx, Topbar.jsx (global arama, bildirim zili)
      hooks/              useDebounce, useDictionary, useAutosave ...
      constants/          labels.js (enum → Türkçe etiket), routes.js
      utils/              format.js (tarih, para, telefon), errors.js (ProblemDetails → form hataları)
      styles/             tokens.css, theme-overrides.css, global.css
    features/
      dashboard/          MyTasksPage.jsx ("Bekleyen işlerim"), DashboardPage.jsx (IK Yöneticisi kart dashboard'u:
                          cards/ tema başına kartlar, charts/ ortak grafik bileşenleri, grafik renkleri tokens.css --chart-*;
                          kartlar SortableGrid içinde, her ChartCard bir cardId taşır. Kart sırası kullanıcı bazlı
                          user_preferences tablosunda ("dashboard.layout", yalnızca grid -> kart kimlikleri sırası);
                          yeni kart eklemek eski düzeni bozmaz, kimliği benzersiz tut)
      candidates/
        api.js            saf fonksiyonlar: getCandidate(id), createCandidate(data) ...
        queries.js        useCandidate(id), useCreateCandidate() ... + query key factory
        pages/            CandidateListPage.jsx, CandidateDetailPage.jsx
        components/       CandidateForm.jsx, ScreeningForm.jsx, CandidateActions.jsx ...
        schemas.js        zod şemaları
      blacklist/  hiring-requests/  positions/  pipeline/  cv-reviews/
      interviews/  questions/  scorecards/  admin/  referral/
```

Kurallar: bir özellik başka bir özelliğin `components/` klasöründen import etmez; ortak ihtiyaç `shared/`'a taşınır. Dosya adı = bileşen adı (PascalCase.jsx); hook ve yardımcılar camelCase.js.

## 3. JavaScript kuralları

- Sadece fonksiyon bileşen + hook. Class bileşen yok.
- Prop'ları ve api fonksiyonlarının dönüş şeklini **JSDoc** ile belgele:

```jsx
/**
 * @param {{ candidateId: string, onDone?: () => void }} props
 */
export default function ScreeningForm({ candidateId, onDone }) { ... }
```

- Enum değerleri backend'deki İngilizce string ile birebir (`'Phone'`, `'Teams'`); Türkçe karşılığı `shared/constants/labels.js`'ten.
- Kullanıcıya görünen her metin Türkçe. Tarih `DD.MM.YYYY HH:mm`, para `1.250.000 ₺`, ondalık virgül.
- `useEffect` ile veri çekme yok → TanStack Query. Türetilebilen değer için state tutma.
- ESLint uyarısız, Prettier formatlı kod.

## 4. Tasarım dili ve PrimeReact tema

Hedef: sade, ferah, modern; "varsayılan PrimeReact" gibi görünmemeli. Tüm görsel kararlar token'larda toplanır, bileşenlerde sabit renk/px yazılmaz.

- Başlangıç teması `lara-light-*` (styled mode); üzerine `theme-overrides.css` ile token'lara bağlanır.
- `shared/styles/tokens.css`:

```css
:root {
  --color-primary: #4f46e5;        /* tek vurgu rengi */
  --color-primary-soft: #eef2ff;
  --color-bg: #f7f8fa;             /* sayfa zemini */
  --color-surface: #ffffff;        /* kart */
  --color-border: #e5e7eb;
  --color-text: #111827;
  --color-text-muted: #6b7280;
  --color-success: #16a34a;  --color-warning: #d97706;  --color-danger: #dc2626;
  --radius-sm: 6px;  --radius-md: 10px;  --radius-lg: 14px;
  --space-1: 4px; --space-2: 8px; --space-3: 12px; --space-4: 16px; --space-6: 24px; --space-8: 32px;
  --shadow-card: 0 1px 2px rgb(16 24 40 / 0.04), 0 1px 3px rgb(16 24 40 / 0.06);
  --font-sans: "Inter", system-ui, sans-serif;
}
```

- Tipografi: Inter, gövde 14px, sayfa başlığı 22px/600, bölüm başlığı 16px/600. Türkçe karakterleri destekleyen font şart.
- Yerleşim: sol daraltılabilir sidebar + üst çubuk (global arama, bildirim zili, kullanıcı menüsü). İçerik kartlarda, kartlar arası `--space-6`.
- Durumlar renkli `Tag` ile ve **her zaman metinle** (sadece renge güvenme): Olumlu yeşil, Beklemede sarı, Olumsuz kırmızı, Taslak gri.
- Boş ekranlar `EmptyState` (ikon + bir cümle + birincil buton).
- Yükleniyor durumu `Skeleton`; tam sayfa spinner kullanma.
- PrimeReact Türkçe locale: `addLocale('tr', {...})` + `locale('tr')` uygulama açılışında (gün/ay adları, "Temizle", "Bugün").
- Kontrast WCAG AA; tüm etkileşimli öğeler klavyeyle erişilebilir.

## 5. Sunucu verisi

- `shared/api/http.js`: `baseURL = import.meta.env.VITE_API_URL`, `withCredentials: true` (refresh cookie), Authorization header store'dan, 401 → refresh kuyruğu (bkz. `auth.md`).
- Hata: backend `ProblemDetails` döner. `utils/errors.js` alan hatalarını react-hook-form'a `setError` ile basar; genel hata `Toast` ile Türkçe mesaj.
- Query key factory her özellikte:

```js
export const candidateKeys = {
  all: ['candidates'],
  list: (filters) => ['candidates', 'list', filters],
  detail: (id) => ['candidates', 'detail', id],
};
```

- Mutation sonrası ilgili key'leri `invalidateQueries`; liste/aşama taşımada optimistic update + hata olursa geri al.
- SignalR bildirimi gelince ilgili query'yi invalidate et ve zil sayacını artır.

## 6. Formlar

- `react-hook-form` + `zod`. Şema `schemas.js`'te; mesajlar Türkçe.
- **IK Ön Görüşme Formu'nda tüm alanlar opsiyonel**; şema sadece biçim kontrolü yapar. Form `useAutosave` ile 2 sn debounce taslak kaydeder ve "Kaydedildi · 14:32" gösterir.
- Aday hızlı ekleme: ad, soyad, e-posta/telefon, CV. Gerisi aday kartında sonradan. E-posta/telefon alanından çıkınca mükerrer ve kara liste kontrolü çağrılır, sonuç formun üstünde uyarı kutusu olarak gösterilir.
- Uzun formlar bölümlere ayrılır (`Panel`/`Fieldset`), en fazla 2 sütun, mobilde tek sütun.
- Kaydet butonu her zaman görünür (sticky footer); kaydedilmemiş değişiklikle sayfadan çıkışta uyarı.

## 7. Tablolar, kanban, takvim

- Listeler PrimeReact `DataTable` **lazy mode** (sunucu taraflı sayfalama, sıralama, filtre). Filtreler URL query string'inde tutulur (link paylaşılabilir, geri tuşu çalışır).
- Satırdan çoklu seçim + toplu işlem çubuğu (red, etiket, öner).
- Kanban: `@dnd-kit`; kart üzerinde ad, pozisyon, aşamadaki gün sayısı, son karar rozeti. Geçersiz geçişte kart geri döner ve sebebi toast'ta yazar. Başarılı taşımadan sonra 10 sn "Geri al" toast'u.
- Takvim: FullCalendar `timeGridWeek` varsayılan, mülakatçı ve oda filtresi; boş alana tıklayınca mülakat planlama penceresi açılır.

## 8. Routing, yetki ve menü

- Rotalar `router.jsx`'te tek yerde; her rota `handle: { roles: [...] }` taşır, `RequireRole` sarmalayıcısı kontrol eder.
- Menü aynı rota tanımlarından üretilir; kullanıcı yetkisi olmayan öğeyi **görmez** (pasif gösterme).
- `referral` scope'unda sadece `/referral` ve `/referral/my` rotaları vardır; sidebar render edilmez, tek sayfalık sade yerleşim.
- Bilinmeyen rota → 404 sayfası, yetkisiz → 403 sayfası (Türkçe, ana sayfaya dön butonu).

## 9. UX ilkeleri (kabul kriteri)

Kullanıcı açıkça "uygulama karışık olmamalı, her şey kolay yönetilmeli" dedi. Her ekranı bunlara göre kontrol et:

1. **Rolüne göre sade menü** — kişi sadece işine yarayanı görür.
2. **"Bekleyen işlerim" ana sayfa** — incelenecek CV'ler, puanlanacak mülakatlar, onay bekleyen talepler, yaklaşan mülakatlar, onboarding görevleri; her öğe tek tıkla ilgili ekrana gider.
3. **Tek ekran aday kartı** — üstte özet (ad, iletişim, etiketler, aktif süreçler, uyarılar: kara liste/mükerrer), altta sekmeler: Genel · Ön Görüşme · Süreçler · Mülakatlar · Dosyalar · Zaman Çizelgesi. Sağ üstte birincil aksiyonlar: **Öner**, **Mülakat Planla**, **Süreci Sonlandır**.
4. **En fazla 3 adım** — sık işler aday kartından açılan tek pencerede tamamlanır.
5. **Global arama** (üst çubuk, `Ctrl+K`) — aday, pozisyon, talep.
6. **Toplu işlem ve sürükle-bırak.**
7. **Anlaşılır dil** — "Submit" değil "Gönder"; hata mesajı ne yapılacağını söyler ("Ret nedeni seçmelisiniz.").
8. **Geri alınabilirlik** — kritik işlemde onay penceresi (sonucu açıklayan cümleyle), aşama değişikliğinde "Geri al".
9. **Mobil uyum** — mülakatçı telefondan CV inceleyip puan verebilmeli; scorecard ekranı mobilde tek sütun, büyük puan butonları (1–5).
10. **Tutarlılık** — aynı iş her yerde aynı bileşenle ve aynı kelimeyle yapılır.

Bir ekran bitince kendine sor: IK uzmanı bunu eğitim almadan kullanabilir mi? Gereksiz alan, buton veya adım var mı?

## 10. Ekran envanteri (Faz 1)

| Ekran | Rota | Roller |
| --- | --- | --- |
| Giriş | `/login` | herkes |
| Bekleyen işlerim | `/` | tam erişim |
| Adaylar (liste + filtre) | `/candidates` | IK |
| Aday kartı | `/candidates/:id` | IK; TeamLead (kendine önerilen, kısıtlı görünüm) |
| Kara liste | `/blacklist` | IK |
| Kadro talepleri | `/hiring-requests` | TeamLead, DepartmentManager, IK |
| Pozisyonlar | `/positions`, `/positions/:id` | IK, ilgili TeamLead |
| Süreç hattı (kanban) | `/positions/:id/pipeline` | IK |
| Bekleyen CV'lerim | `/cv-reviews` | TeamLead |
| Mülakat takvimi | `/interviews` | IK, mülakatçılar |
| Mülakat değerlendirme | `/interviews/:id/scorecard` | atanmış mülakatçı |
| Aday karşılaştırma | `/positions/:id/compare` | IK, ilgili TeamLead |
| Soru bankası ve şablonlar | `/questions`, `/interview-templates` | TeamLead, IK |
| Yönetim (sözlükler, akışlar, rol eşlemesi, e-posta şablonları) | `/admin/*` | SystemAdmin, HrManager (kısmen) |
