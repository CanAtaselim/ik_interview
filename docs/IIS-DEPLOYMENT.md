# IIS'te Yayınlama Kılavuzu

Bu doküman İK İşe Alım uygulamasını Windows Server üzerinde IIS ile yayınlamak içindir. Yalnızca
kurulum ve yayın (deployment) sürecini anlatır.

## 1. Mimari

Tarayıcı tek bir adresi görür (örn. `https://interview.basarsoft.com.tr`). Docker kurulumundaki nginx'in
yaptığı işi IIS yapar:

```
Tarayıcı ──HTTPS 443──► [ik-web sitesi]  statik arayüz (React build)
                           │  /api/*  ve  /hubs/*  → URL Rewrite + ARR
                           ▼
                        [ik-api sitesi]  http://127.0.0.1:5085  (yalnızca bu sunucudan erişilir)
                           │  ASP.NET Core (in-process)
                           ▼
                        PostgreSQL  ·  AD/LDAP  ·  SMTP  ·  (ops.) Graph
```

Neden tek adres: refresh token cookie'si `SameSite=Strict`, yolu `/api/v1/auth`. Arayüz ve API
aynı adresten sunulunca CORS ve cookie sorunu yaşanmaz. API'yi ayrı bir alan adına koymayın.

İki ayrı site, iki ayrı uygulama havuzu kullanılır:

| Site | Havuz | Binding | Fiziksel klasör |
|---|---|---|---|
| `ik-web` | `ik-web` | `https://interview.basarsoft.com.tr:443` (+ 80 → 443 yönlendirme) | `D:\inetpub\ik\web` |
| `ik-api` | `ik-api` | `http://127.0.0.1:5085` | `D:\inetpub\ik\api` |

Klasör ve alan adlarını kendi ortamınıza göre değiştirin. Aşağıdaki komutlarda aynı adlar geçer.

## 2. Sunucu gereksinimleri

1. **IIS rolü** ve bileşenleri: Web Server, Static Content, Default Document, **WebSocket Protocol**,
   Request Filtering.
   ```powershell
   Install-WindowsFeature Web-Server, Web-Static-Content, Web-Default-Doc, Web-WebSockets, Web-Filtering, Web-Mgmt-Console
   ```
2. **.NET 10 Hosting Bundle** (ASP.NET Core Module V2 dahil). SDK değil, "Hosting Bundle" inecek.
   Kurulumdan sonra IIS'i yeniden başlatın: `iisreset`.
3. **URL Rewrite** modülü (Microsoft, ücretsiz).
4. **Application Request Routing (ARR) 3.x** modülü. Kurulumdan sonra IIS Manager → sunucu düğümü →
   *Application Request Routing Cache* → *Server Proxy Settings* → **Enable proxy** işaretli olsun.
   Aynı ekranda şunları ayarlayın:
   - *Time-out*: `3600` saniye (bildirim bağlantısı uzun süre açık kalır).
   - *Preserve client IP in the following header*: boş bırakılabilir (bkz. §10).
   - *Reverse rewrite host in response headers*: kapalı.
   - *Preserve Host header* (`system.webServer/proxy` → `preserveHostHeader`): `true` yapın.
5. **PostgreSQL 17**: aynı sunucuya veya ayrı bir sunucuya. Bu doküman ayrı bir PostgreSQL olduğunu
   varsayar (aynı sunucuya kuruyorsanız `localhost` kullanın).
6. **SSL sertifikası**: şirket CA'sından veya kurumsal bir sertifika. `interview.basarsoft.com.tr` için, sunucunun
   sertifika deposuna kurulu olsun. HTTPS zorunludur (bkz. §10).
7. Sunucudan şunlara erişim: PostgreSQL (5432), AD alan denetleyicisi (LDAP 389 veya LDAPS 636), SMTP
   sunucusu, kullanılacaksa `graph.microsoft.com` ve `login.microsoftonline.com` (443).

Derleme makinesinde (sunucuda olması şart değil): **.NET 10 SDK** ve **Node.js 24** (npm dahil).

## 3. Veritabanı

```sql
-- postgres kullanıcısıyla
CREATE ROLE recruitment LOGIN PASSWORD '<güçlü-şifre>';
CREATE DATABASE recruitment OWNER recruitment;
```

**Seçenek A — temiz kurulum:** Boş veritabanı bırakın. API ilk açılışta migration'ları uygular ve
temel verileri (sözlükler, varsayılan şablonlar) kendisi yazar (§6'daki `Database__ApplyMigrationsOnStartup`).

**Seçenek B — mevcut veriyle (dump'tan):**
```powershell
pg_restore -h <db-sunucu> -U recruitment -d recruitment --no-owner --no-privileges recruitment-YYYYMMDD-HHMM.backup
```
Dump'taki migration geçmişi koddakiyle aynıysa API açılışta yeni bir şey uygulamaz. Yeni bir migration
varsa uygular.

## 4. Derleme (publish)

Depo kökünden, derleme makinesinde:

```powershell
# Backend (framework-dependent, IIS in-process)
dotnet publish backend\src\Recruitment.Api\Recruitment.Api.csproj -c Release -o publish\api

# Frontend
cd frontend
npm ci
npm run build          # çıktı: frontend\dist
cd ..
Copy-Item frontend\dist publish\web -Recurse
```

Notlar:
- `dotnet publish` çıktısına `web.config` otomatik eklenir (`hostingModel="inprocess"`). Elle yazmanız gerekmez.
- Arayüz `frontend\.env.production` içindeki `VITE_API_URL=/api/v1` ile derlenir. Adres göreli olduğu için
  ortam değişince yeniden derleme gerekmez.
- Arayüz için IIS ayarı [frontend/public/web.config](../frontend/public/web.config) dosyasındadır. Vite bunu
  `dist` içine kopyalar. API sitenizin portunu değiştirirseniz bu dosyadaki `5085` değerini de değiştirip
  yeniden derleyin.
- Derleme çıktısı iki klasördür: `publish\api` ve `publish\web`. Bunları sunucuya kopyalayacaksınız (§7).

## 5. IIS'te site ve havuz oluşturma

Klasörleri oluşturup içeriği kopyalayın:

```powershell
New-Item -ItemType Directory D:\inetpub\ik\api, D:\inetpub\ik\web -Force
robocopy publish\api D:\inetpub\ik\api /E
robocopy publish\web D:\inetpub\ik\web /E
```

### 5.1 Uygulama havuzları

```powershell
Import-Module WebAdministration

# API havuzu
New-WebAppPool -Name ik-api
Set-ItemProperty IIS:\AppPools\ik-api -Name managedRuntimeVersion -Value ''            # No Managed Code
Set-ItemProperty IIS:\AppPools\ik-api -Name startMode -Value AlwaysRunning
Set-ItemProperty IIS:\AppPools\ik-api -Name processModel.idleTimeout -Value ([TimeSpan]::Zero)
Set-ItemProperty IIS:\AppPools\ik-api -Name processModel.loadUserProfile -Value $true
Set-ItemProperty IIS:\AppPools\ik-api -Name recycling.periodicRestart.time -Value ([TimeSpan]::Zero)
Set-ItemProperty IIS:\AppPools\ik-api -Name recycling.disallowOverlappedRotation -Value $true

# Arayüz havuzu (yalnızca statik dosya)
New-WebAppPool -Name ik-web
Set-ItemProperty IIS:\AppPools\ik-web -Name managedRuntimeVersion -Value ''
```

Her ayarın nedeni:

| Ayar | Değer | Neden |
|---|---|---|
| .NET CLR version | No Managed Code | ASP.NET Core kendi runtime'ını taşır. |
| Start Mode | AlwaysRunning | Uygulama arka plan işleri (Hangfire: e-posta kuyruğu, mülakat hatırlatmaları, CV süre takibi) çalıştırır. Hazır beklemezse işler durur. |
| Idle Time-out | 0 | Trafik yokken havuz kapanmasın; yoksa arka plan işleri durur. |
| Load User Profile | True | Data Protection anahtarları ve geçici dosyalar için profil gerekir. |
| Periodic Restart | 0 (kapalı) | Gün içinde rastgele yeniden başlatma olmasın. Gerekirse gece sabit saat verin. |
| Disallow Overlapped Rotation | True | Yeniden başlatmada eski ve yeni süreç aynı anda çalışıp işleri çift çalıştırmasın. |

Havuz kimliği olarak varsayılan `ApplicationPoolIdentity` yeterlidir. Veritabanına Windows kimliğiyle değil
şifreyle bağlanıldığı için ek hesap gerekmez.

### 5.2 Siteler ve binding

```powershell
# API: yalnızca localhost'tan erişilir
New-Website -Name ik-api -PhysicalPath D:\inetpub\ik\api -ApplicationPool ik-api `
  -IPAddress 127.0.0.1 -Port 5085 -HostHeader ''
Set-ItemProperty IIS:\Sites\ik-api -Name applicationDefaults.preloadEnabled -Value $true

# Arayüz: HTTPS
New-Website -Name ik-web -PhysicalPath D:\inetpub\ik\web -ApplicationPool ik-web `
  -Port 443 -HostHeader interview.basarsoft.com.tr -Ssl
# Sertifikayı bağlayın: IIS Manager → ik-web → Bindings → https → SSL certificate
# (komutla: Get-ChildItem Cert:\LocalMachine\My | ... | New-Item IIS:\SslBindings\0.0.0.0!443!interview.basarsoft.com.tr)
```

Port 80'e gelenleri 443'e yönlendirmek için ik-web'e `http` binding ekleyip URL Rewrite'ta
"HTTP → HTTPS redirect" kuralı ekleyin (IIS Manager → URL Rewrite → *Add Rule(s)* → *Blank rule*
yerine hazır şablon *Redirect HTTP to HTTPS*).

Güvenlik duvarında yalnızca 443 (ve istenirse 80) açık olsun. **5085 dışarıya açılmasın**.

### 5.3 Klasör izinleri

API havuzu kimliği `logs` ve `storage` klasörlerine yazabilmeli (log dosyaları ve yüklenen CV'ler):

```powershell
New-Item -ItemType Directory D:\inetpub\ik\api\logs, D:\inetpub\ik\api\storage -Force
icacls D:\inetpub\ik\api\logs    /grant "IIS AppPool\ik-api:(OI)(CI)M"
icacls D:\inetpub\ik\api\storage /grant "IIS AppPool\ik-api:(OI)(CI)M"
```

Havuz kimlikleri uygulama klasörünü okuyabildiği için kalan klasörlerde ek izin gerekmez. Yüklenen CV'lerin
silinmemesi için `storage` yayın sırasında ezilmemeli (§8).

## 6. Yapılandırma (gizli değerler dahil)

`appsettings.json` içindeki ayarlar ortam değişkenleriyle ezilir; iç içe anahtarlar `__` (çift alt çizgi) ile
yazılır. Gizli değerleri (şifre, imza anahtarı) `appsettings.json` içine veya depoya yazmayın. Havuz bazında
ortam değişkeni olarak verin. Böylece yayın sırasında dosyalar ezilse bile ayarlar kaybolmaz.

Ortam değişkenlerini `ik-api` havuzuna yazan komut (yönetici PowerShell):

```powershell
$appcmd = "$env:windir\system32\inetsrv\appcmd.exe"
function Set-PoolEnv($name, $value) {
  & $appcmd set config -section:system.applicationHost/applicationPools `
    "/+[name='ik-api'].environmentVariables.[name='$name',value='$value']" /commit:apphost
}

Set-PoolEnv 'ASPNETCORE_ENVIRONMENT'                 'Production'
Set-PoolEnv 'ConnectionStrings__Default'             'Host=<db-sunucu>;Port=5432;Database=recruitment;Username=recruitment;Password=<db-şifre>'
Set-PoolEnv 'Jwt__SigningKey'                        '<en az 32 karakter rastgele>'   # üretmek için: [Convert]::ToBase64String((1..48|%{[byte](Get-Random -Max 256)}))
Set-PoolEnv 'Recruitment__FrontendBaseUrl'           'https://interview.basarsoft.com.tr'
Set-PoolEnv 'Cors__AllowedOrigins__0'                'https://interview.basarsoft.com.tr'

# Active Directory
Set-PoolEnv 'Ldap__Host'                             'dc01.sirket.local'
Set-PoolEnv 'Ldap__Port'                             '636'
Set-PoolEnv 'Ldap__UseSsl'                           'true'
Set-PoolEnv 'Ldap__Domain'                           'SIRKET'
Set-PoolEnv 'Ldap__BaseDn'                           'DC=sirket,DC=local'
Set-PoolEnv 'Ldap__BindUser'                         'svc_ik_ldap'
Set-PoolEnv 'Ldap__BindPassword'                     '<servis hesabı şifresi>'

# E-posta (boş bırakılırsa e-postalar gönderilmez, yalnızca kayda geçer)
Set-PoolEnv 'Smtp__Host'                             'smtp.sirket.local'
Set-PoolEnv 'Smtp__Port'                             '587'
Set-PoolEnv 'Smtp__UseSsl'                           'true'
Set-PoolEnv 'Smtp__Username'                         '<kullanıcı>'
Set-PoolEnv 'Smtp__Password'                         '<şifre>'
Set-PoolEnv 'Smtp__FromAddress'                      'ik-ise-alim@sirket.com'

# İlk kurulumda veritabanı şemasını API kursun (bkz. aşağıdaki not)
Set-PoolEnv 'Database__ApplyMigrationsOnStartup'     'true'
```

Ayar notları:

- **`Jwt__SigningKey`** zorunludur ve en az 32 karakter olmalıdır. Boşsa uygulama açılmaz (HTTP 500.30). Bu anahtarı
  değiştirmek herkesin oturumunu düşürür.
- **LDAP:** Production'da `Ldap__Host` ve `Ldap__BaseDn` boş olamaz, yoksa uygulama açılmaz. `Ldap__UseFake`
  yalnızca Development içindir; Production'da `true` yapılırsa uygulama açılmaz. Kullanıcıların rolü AD
  gruplarından gelir. Varsayılan eşleme `Recruitment-<Rol>` adlı gruplardır (örn. `Recruitment-SystemAdmin`,
  `Recruitment-HrManager`). Bu grupları AD'de oluşturup kullanıcıları ekleyin; farklı grup adları kullanacaksanız
  uygulamada **Yönetim → Rol eşlemesi** ekranından değiştirirsiniz (yalnızca Sistem Yöneticisi görür). İlk
  girişi yapacak kişi `Recruitment-SystemAdmin` (veya eşlediğiniz grup) üyesi olmalı.
- **`Database__ApplyMigrationsOnStartup`:** İlk kurulumda `true` bırakın, API şemayı kursun. Sonraki sürümlerde
  yeni migration varsa yine API açılışta uygular. Bu davranışı kapatmak istiyorsanız `false` yapıp migration'ı
  elle uygularsınız. Elle yönteminde derleme makinesinde `dotnet ef database update -p
  backend/src/Recruitment.Infrastructure -s backend/src/Recruitment.Api --connection "<bağlantı>"` çalıştırılır.
- **Outlook müsaitlik (Graph):** Kullanılacaksa `Graph__TenantId`, `Graph__ClientId`, `Graph__ClientSecret`,
  `Graph__OrganizerMailbox` değişkenlerini aynı şekilde ekleyin. Boş bırakılırsa Outlook okunmaz, yalnızca
  sistem içi müsaitlik kullanılır. `Graph__UseFake` Production'da kullanılamaz.
- **Dosya depolama:** CV'ler `D:\inetpub\ik\api\storage` altına yazılır. Başka bir diske almak için
  `FileStorage__RootPath` değişkenine mutlak yol verin (örn. `E:\ik-storage`) ve o klasöre §5.3'teki izni verin.
- **Loglar:** `logs\recruitment-<tarih>.log`, günlük döner, 30 gün tutulur.

Ortam değişkenlerini değiştirdikten sonra havuzu yeniden başlatın: `Restart-WebAppPool ik-api`.

> `ASPNETCORE_ENVIRONMENT` ayarını boş bırakırsanız zaten Production olur. Yine de açıkça yazmak iyidir.
> **Development yapmayın**: sahte kullanıcılar (şifre `Test123!`) ve sahte takvim açılır.

## 7. İlk yayın ve doğrulama

1. `ik-api` havuzu ve siteyi başlatın, sonra `ik-web`.
2. API'nin ayakta olduğunu sunucudan kontrol edin:
   ```powershell
   Invoke-WebRequest http://127.0.0.1:5085/api/v1/auth/me -SkipHttpErrorCheck | Select StatusCode
   ```
   `401` dönmesi API'nin çalıştığını gösterir (oturum gerekli). Başlamadıysa §11'e bakın.
3. Tarayıcıdan `https://interview.basarsoft.com.tr` adresini açın, AD hesabıyla giriş yapın.
4. Kontrol listesi:
   - Giriş yapılıyor ve sayfa yenilenince (F5) oturum düşmüyor.
   - Aday listesi ve kadro talepleri geliyor.
   - CV yüklenebiliyor (10 MB sınırı).
   - Sağ üstteki bildirim zili canlı güncelleniyor (WebSocket çalışıyor).
   - Bir test e-postası gönderen işlem yapıp e-postanın geldiğini doğrulayın (mülakat daveti gibi).
   - `logs\` klasöründe log dosyası oluşmuş.
5. İlk kurulum bittikten sonra `Database__ApplyMigrationsOnStartup` değişkenini ihtiyaca göre `false` yapın
   (bkz. §6).

## 8. Güncelleme (yeni sürüm yayınlama)

Derleme makinesinde §4'ü tekrarlayın, sonra sunucuda:

```powershell
# 1) API: önce siteyi "bakımda" göster, dosyaları kilitlemeden kopyala
Copy-Item .\app_offline.htm D:\inetpub\ik\api\app_offline.htm      # içeriği: "Bakımdayız, birazdan döneceğiz"
Start-Sleep 3

# 2) logs ve storage'a DOKUNMADAN yayın klasörünü eşitle
robocopy publish\api D:\inetpub\ik\api /MIR /XD logs storage /XF app_offline.htm

# 3) Bakım dosyasını kaldır: uygulama yeni sürümle açılır
Remove-Item D:\inetpub\ik\api\app_offline.htm

# 4) Arayüz
robocopy publish\web D:\inetpub\ik\web /MIR
```

- `/MIR` hedefte olmayan dosyaları siler. `/XD logs storage` ile yüklenen CV'ler ve loglar korunur.
  Bu parametreyi **unutmayın**; unutulursa tüm CV'ler silinir.
- `app_offline.htm` varken ASP.NET Core Module uygulamayı durdurur ve bu sayfayı gösterir. Dosyalar kilitli kalmaz.
- Arayüz dosya adları her derlemede değişir. Kullanıcılar `index.html`'i önbelleğe almadığı için yeni sürümü
  hemen görür. Sürümden önce açık olan sekmeler bir sonraki yenilemede güncellenir.
- Veritabanı şemasında değişiklik varsa API açılışta migration'ı uygular (§6). Büyük bir sürümden önce
  yedek alın (§9).
- Geri alma: bir önceki `publish` klasörlerini saklayın, sorun olursa aynı adımları eski çıktıyla çalıştırın.
  Migration uygulandıysa şemayı geri almak için veritabanı yedeğinden dönmek gerekir.

## 9. Yedekleme

Yedeklenmesi gerekenler:

- **PostgreSQL:** gece `pg_dump -Fc -h <db-sunucu> -U recruitment recruitment > recruitment-yyyyMMdd.backup`
  (Görev Zamanlayıcı ile).
- **`storage` klasörü** (yüklenen CV ve belgeler).
- **Ortam değişkenleri / ayarlar:** şifresiz bir kopyasını (hangi değişkenlerin tanımlı olduğu listesi) saklayın.
  Şifreleri şirketin şifre yöneticisinde tutun.

## 10. Bilinen noktalar

- **HTTPS zorunlu.** Production'da refresh token cookie'si her zaman `Secure` işaretiyle yazılır. Siteyi `http://`
  üzerinden açarsanız tarayıcı cookie'yi saklamaz ve oturum 15 dakikada (access token süresi) düşer.
- **İstemci IP adresi.** Uygulama `X-Forwarded-For` başlığını işlemiyor. Arayüz sitesi isteği API'ye ARR ile
  ilettiği için denetim kaydında ve giriş denemesi sınırlamasında IP, istemci yerine `127.0.0.1` görünür. Bu
  davranış sorun olursa API'ye bir satırlık `UseForwardedHeaders` eklenmesi gerekir (kod değişikliği, ayrıca
  istenebilir).
- **Tek sunucu.** Arka plan işleri uygulama içinde çalışır. Aynı veritabanına bağlı birden fazla API sunucusu
  kurulacaksa önceden konuşalım (oturum anahtarları, dosya depolama paylaşımı ve iş koordinasyonu gerekir).
- **Data Protection anahtarları.** CV indirme bağlantılarının imzası için kullanılır; ömrü 5 dakikadır. Havuz
  yeniden başlayınca eski bağlantılar geçersiz olabilir, kullanıcı bağlantıyı yeniden açar. Ek işlem gerekmez.

## 11. Sorun giderme

| Belirti | Olası neden ve çözüm |
|---|---|
| **HTTP 500.30** (ASP.NET Core app failed to start) | Yapılandırma doğrulaması başarısız. `Jwt__SigningKey` boş veya 32 karakterden kısa, `Ldap__Host`/`Ldap__BaseDn` boş, veritabanına bağlanılamıyor. Ayrıntı için `logs\` klasörüne ve Event Viewer → Windows Logs → Application'a bakın. |
| **HTTP 500.19** | `web.config` okunamıyor: URL Rewrite veya ASP.NET Core Module (Hosting Bundle) kurulu değil. Hosting Bundle kurulumdan sonra `iisreset`. |
| **HTTP 502.5 / 500.0** | Hosting Bundle'ın .NET 10 sürümü kurulu değil veya yanlış kuruldu. `dotnet --list-runtimes` çıktısında `Microsoft.AspNetCore.App 10.x` olmalı. |
| **Sayfa açılıyor, API çağrıları 404/502** | ARR proxy kapalı (§2.4) veya `ik-api` sitesi durmuş / `web.config`'deki port yanlış. |
| **Sayfayı yenileyince 404** | `ik-web` altında `web.config` yok veya URL Rewrite kurulu değil. |
| **Giriş yapılıyor ama hemen düşüyor** | Site `http://` üzerinden açılmış (§10) veya Jwt anahtarı değişmiş. |
| **Bildirim zili güncellenmiyor** | IIS'te *WebSocket Protocol* özelliği kurulu değil veya ARR zaman aşımı kısa. Uygulama bağlantıyı yine de uzun yoklamayla (long polling) sürdürür, ama gecikme olur. |
| **CV yüklenirken 413** | İstek sınırı: `web.config` içindeki `maxAllowedContentLength` (arayüz sitesi) ve uygulamadaki 10 MB sınırı. |
| **Giriş: "kullanıcı adı veya şifre hatalı" (AD'de doğru)** | LDAP erişimi: DC'ye port açık mı, `Ldap__BaseDn`/`Ldap__Domain` doğru mu, LDAPS'te sertifika sunucu tarafından güveniliyor mu. |
| **E-postalar gitmiyor** | `Smtp__*` boş veya yanlış; e-postalar kayda geçer ama gönderilmez. Loglarda `DispatchOutgoingEmails` satırlarına bakın. |
| **Ayrıntılı hata görmek** | Geçici olarak `ik-api\web.config` içinde `stdoutLogEnabled="true"` yapın, `logs\stdout` klasörüne yazılır (klasör var ve yazılabilir olmalı). İşiniz bitince kapatın. |

## 12. Alternatif yerleşim: API, arayüz sitesinin altında `/api` uygulaması

Bu yerleşimde tek site vardır. API, sitenin kök klasörünün içindeki `api` klasöründe çalışır:

```
C:\inetpub\wwwroot\basarinterview\          ← ik-web sitesi (arayüz: index.html, assets, web.config)
C:\inetpub\wwwroot\basarinterview\api\      ← API (Recruitment.Api.dll, appsettings*.json, web.config)
```

ARR, ayrı port ve ayrı API sitesi gerekmez. Yapılacaklar:

1. IIS Manager → sitenin üzerine sağ tık → **Add Application**: Alias `api`, Application pool `ik-api`
   (No Managed Code), Physical path `...\basarinterview\api`. (`api` klasörü zaten uygulama olarak
   görünüyorsa tekrar eklemeyin.)
2. Arayüzü sitenin köküne, API'yi `api` klasörüne kopyalayın. Arayüzün `web.config` dosyası
   `inheritInChildApplications="false"` ile yazıldığı için kuralları `/api` uygulamasına geçmez.
   `frontend\public\web.config` bu yerleşime göredir. İçinde API yönlendirmesi yoktur.
3. API uygulamasının `web.config` dosyasını (publish üretir) olduğu gibi bırakın.

IIS, `/api` önekini uygulamanın PathBase'ine taşır; API bunu `Program.cs` içindeki küçük ara katmanla
`/api/v1/...` rotalarına geri çevirir. Bildirim kanalı `/api/hubs/notifications` adresinden bağlanır.
Bu davranış ayrı site yerleşiminde ve Docker'da da aynen çalışır (PathBase boşsa ara katman bir şey yapmaz).

Bu yerleşimde API klasörü web kökünün içinde durduğu için, `api` bir uygulama olarak tanımlı değilse
`appsettings.Local.json` gibi dosyalar statik dosya olarak indirilebilir. `api` düğümünün uygulama olarak
göründüğünü IIS Manager'dan doğrulayın.
