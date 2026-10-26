# Demo veri

İK ekibinin tüm işe alım senaryosunu baştan sona deneyebilmesi için örnek veri üretir. Yalnızca **geliştirme ortamı** içindir: Docker Compose ile açılan uygulama ve sahte AD kullanıcıları (`ikyonetici`, `ikuzman`, `mudur`, `takimlideri`, `yigit`, `yonetim`; şifre `Test123!`).

Her kayıt uygulamanın kendi API'sinden geçer; yani iş kuralları, bildirimler ve e-postalar gerçekte olduğu gibi çalışır. İki istisna vardır ve her ikisi de yerel veritabanına doğrudan SQL ile yazılır: geçmiş mülakatlar (uygulama geçmişe mülakat planlatmaz) ve gecikmiş CV değerlendirmesi için tarihler geri alınır.

## Kullanım

```sh
docker compose up -d --build            # uygulama açık olmalı (http://localhost:8080)
node tools/demo-data/seed.mjs           # örnek veriyi yükler (~1 dk)
node tools/demo-data/cleanup.mjs --dry-run   # silmeyi dener, hiçbir şey silmeden geri alır
node tools/demo-data/cleanup.mjs        # yalnızca demo kayıtlarını siler
node tools/demo-data/cleanup.mjs --clear-mailpit   # ayrıca Mailpit kutusunu boşaltır
```

Node 20 veya üstü gerekir; bağımlılık yoktur. Betikler `docker exec` ile `recruitment-postgres` ve `recruitment-api` konteynerlerine erişir (adlar `DB_CONTAINER` ve `API_CONTAINER` ile değiştirilebilir).

## Neleri ekler

- 5 kadro talebi: taslak, departman yöneticisi onayında, İK yöneticisi onayında, reddedilmiş, onaylanmış (pozisyonu otomatik açılır).
- 3 pozisyon, 7 soru, 2 mülakat şablonu.
- 11 aday (hepsinde `demo` etiketi): CV (PDF), ön görüşme formu, yetenek havuzu, kara liste.
- Süreç hattında değişik aşamalarda adaylar: CV bekleyen, gecikmiş CV, olumsuz, çekilen, yarın mülakatı olan, puanlaması yarım, puanlaması tamam ve final aşamasında.
- Mülakatçı müsaitlikleri, bildirimler ve Mailpit e-postaları.
- Aday kaynağı (3) ve toplantı odası (2) sözlük öğeleri.

## Geri alma nasıl çalışır

`seed.mjs`, ürettiği her kaydın kimliğini `.state.json` dosyasına yazar. `cleanup.mjs` yalnızca bu kimlikleri (ve onlara bağlı mülakat, değerlendirme, bildirim, e-posta, dosya kayıtlarını) siler. Elle girdiğiniz veriye dokunmaz; demo olmayan bir mülakat veya değerlendirme bir demo şablonunu ya da soruyu kullanıyorsa o şablon/soru yerinde bırakılır. Demo adaylara veya pozisyonlara bağlı her şey (sizin girdiğiniz mülakat ve değerlendirmeler dahil) silinir; testler sürerken önce `--dry-run` ile bakın. `.state.json` yoksa temizlenecek bir şey olmadığını söyler. Seed yarıda kesilirse dosya o ana kadar üretilenleri içerir; `cleanup.mjs` ile temizleyip yeniden başlayabilirsiniz.

Yeniden yüklemek için önce `cleanup.mjs`, sonra `seed.mjs` çalıştırın; tarihler "bugüne göre" yeniden hesaplanır.
