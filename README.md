# Betakas — Platform sürümü (Web Client → .NET API → PostgreSQL / MSSQL)

Betakas'ın **çok kullanıcılı** sürümü. `betakas-version2` tek dosyalık bir prototipti ve tüm
veriyi tarayıcının localStorage'ında tutuyordu — her tarayıcı kendi ayrı dünyasında çalışıyordu.
Bu sürümde veri **ortak bir veritabanında**, iş kuralları ise **.NET 9 Web API** içinde:

```
Kullanıcı 1 ─┐                                                          ┌─ Supabase (önerilen)
Kullanıcı 2 ─┤                                                          │
Kullanıcı 3 ─┼─▶ React 19 (Web Client) ─HTTPS/JSON─▶ .NET 9 Web API ────┼─ yerel PostgreSQL 17
Kullanıcı 4 ─┤     Vite · TypeScript      JWT + refresh  (Betakas.Api)  │
Kullanıcı 5 ─┘                                                          └─ SQL Server
```

> En hızlı başlangıç: **[Supabase ile çalıştırma](#supabase-ile-çalıştırma-en-kolay-yol)** —
> hiçbir veritabanı kurulumu gerektirmez.

Elif'in açtığı test talebini Ayşe kendi tarayıcısından görür; Ayşe teslim edince Elif'in ekranı
kendiliğinden tazelenir. **Escrow, çift kayıtlı defter ve itibar hesapları sunucudadır** —
tarayıcıdan atlanamaz.

---

## Hızlı başlangıç

```powershell
.\baslat.ps1                      # http://localhost:5187
.\baslat.ps1 -Https               # https://localhost:7187 (dev sertifikası)
.\baslat.ps1 -Provider SqlServer  # PostgreSQL yerine SQL Server
```

Arayüz API ile aynı adresten servis edilir — ayrı web sunucusu veya CORS ayarı gerekmez.
Şema migration'larla açılışta uygulanır; veritabanı boşsa demo veri tohumlanır.

### Testler

```powershell
cd server\Betakas.Api.Tests
dotnet test
```

30 tümleşik test gerçek bir veritabanına karşı koşar (`betakas_test`) ve escrow matematiğini,
yetki kurallarını, feedback kalite kapısını ve oturum güvenliğini doğrular.

---

## Giriş bilgileri

Şifre tüm demo hesaplarında: **`betakas`**
(Giriş ekranındaki hesap kartlarına tıklayınca alanlar kendiliğinden dolar.)

| Rol | E-posta | Not |
|---|---|---|
| Kurucu | `elif@finbutce.co` | FinBütçe · açık talebi ve bekleyen başvurusu var |
| Kurucu | `mert@stokpro.co` | StokPro · Büyüme aboneliği, onay bekleyen teslim |
| Kurucu | `zeynep@mindpuzzle.co` | MindPuzzle · Başlangıç aboneliği |
| Testçi | `ayse@testci.co` | 5.0★ · 1.2× çarpan · teslim etmeyi bekleyen testi var |
| Testçi | `burak@testci.co` | 4.0★ · 1.1× çarpan · onay bekleyen çekim talebi |
| Yönetim | `yonetim@betakas.co` | Üyelik onayı, anlaşmazlık ve gelir paneli |

`selin@testci.co` ve `deniz@rotakurye.co` bilerek **onay bekliyor** durumundadır.

> **Çok kullanıcılı denemek için:** iki ayrı tarayıcı (biri gizli pencere) aç, birinde Elif,
> diğerinde Ayşe olarak gir. Birinde yapılan işlem diğerinde birkaç saniye içinde görünür.

---

## Mimari

### Ne nerede çalışıyor

| Katman | Nerede | Ne yapıyor |
|---|---|---|
| Kimlik | Sunucu | BCrypt parola, kısa ömürlü JWT + döndürülen yenileme jetonu |
| Kaba kuvvet | Sunucu | E-posta ve IP başına deneme sayacı, geçici kilit |
| **İş kuralları** | **Sunucu** | **Escrow, iki defter, itibar çarpanı, kalite kapısı, yetki** |
| Kalıcılık | Sunucu | EF Core · PostgreSQL veya SQL Server · migration'lar |
| Görüntüleme | İstemci | Ekranlar, tablolar, sentez raporu, sürüm karnesi (salt okunur türetmeler) |

İstemci **hiçbir defter satırı yazamaz, hiçbir bakiye değiştiremez**. Tarayıcıda yapılan her
işlem kendi dar ucuna gider; sunucu kuralı doğrular ve güncel state'i geri döndürür.

### Veri modeli

`server/Betakas.Api/Models/Entities.cs` — 11 tablo:

`users` · `versions` · `requests` · `sessions` · `ledger` (token defteri) · `cash_ledger` (₺
defteri) · `purchases` · `withdrawals` · `platform_state` · `refresh_tokens` · `login_attempts`

Şemasını istemci belirleyen iki alan JSON olarak saklanır: `sessions.feedback` (ürün aşamasına
göre şekil değiştirir) ve `versions.fixes` (bug numarası → tik durumu). PostgreSQL'de bunlar
`jsonb`, `skills`/`expertiseCategories` ise `text[]`; SQL Server'da hepsi `nvarchar(max)`.

Defter satırları kasıtlı olarak yabancı anahtarsızdır: `from`/`to` alanlarında kullanıcı
id'lerinin yanı sıra `system`, `escrow`, `revenue`, `pool`, `bank` sanal hesapları geçer.

### API uçları

**Oturum**

| Uç | Açıklama |
|---|---|
| `POST /api/auth/login` | E-posta + parola → erişim jetonu (20 dk) + yenileme jetonu (14 gün) |
| `POST /api/auth/refresh` | Yenileme jetonunu döndürür; kullanılmış jeton tekrar gelirse tüm oturumlar iptal edilir |
| `POST /api/auth/logout` | Yenileme jetonunu iptal eder |
| `POST /api/auth/logout-all` | Kullanıcının tüm cihazlardaki oturumlarını kapatır |
| `POST /api/auth/register` | Başvuru oluşturur (`pending`), parolayı BCrypt ile hash'ler |

**Okuma**

| Uç | Açıklama |
|---|---|
| `GET /api/state` | Tüm state (oturum gerektirir) |
| `GET /api/state/rev` | Yalnızca revizyon numarası — yoklama için ucuz |
| `GET /api/public/state` | Redakte state: giriş ekranı kartları + public profil |

**Domain eylemleri** — her biri kuralını sunucuda doğrular ve güncel state'i döndürür:

| Alan | Uçlar |
|---|---|
| Talep | `POST /api/requests` · `…/{id}/boost` · `…/{id}/close` |
| Sürüm | `POST /api/versions` · `PUT /api/versions/{id}/fixes` |
| Testçi | `POST /api/requests/{id}/apply` · `…/sessions/{id}/submit` · `…/rate-owner` |
| Kurucu | `…/sessions/{id}/approve` · `/reject` · `/accept` · `/dispute` |
| Faturalama | `POST /api/billing/buy` · `/subscribe` · `/cancel` · `/renew` |
| Çekim | `POST /api/withdrawals` · `…/{id}/resolve` |
| Profil | `PUT /api/me/expertise` |
| Yönetim | `…/sessions/{id}/resolve-dispute` · `/api/admin/users/{id}/approve` · `/reject` · `PUT /api/admin/settings/fee` · `/token-price` · `POST /api/admin/reset` |

### Sunucunun uyguladığı kurallar (örnekler)

Bunların hepsi eskiden tarayıcıdaydı ve konsoldan atlanabilirdi:

- **Escrow** — talep açılırken `slot × token` bakiyeden düşülüp kilitlenir; bakiyeden fazlası
  bloke edilemez. Kabul anında escrow'da yeterli token yoksa işlem reddedilir.
- **Ödeme** — token yalnızca escrow'dan çıkar; itibar bonusu `system` hesabından basılır ki
  escrow matematiği bozulmasın. Nakit yalnızca `pool` hesabından ve yalnızca testçi rolüne akar.
- **Kalite kapısı** — teslim formundaki her alanın minimum uzunluğu, ekran kaydı linki ve en az
  5 dakikalık süre sunucuda denetlenir.
- **Sahiplik** — yalnızca talep sahibi kabul/ret/itiraz edebilir; yalnızca sürüm sahibi o sürüme
  talep açabilir; kimse kendi talebine başvuramaz.
- **Rol** — komisyon oranı, token fiyatı, üyelik onayı, anlaşmazlık kararı ve demo sıfırlama
  yalnızca yönetimde. Kimse kendi rolünü yükseltemez.
- **Rakip gizliliği** — `exclude-sector` talebe aynı sektördeki kullanıcı başvuramaz.
- **Sürüm notu tikleri** — yalnızca bilinen durum değerleri ve yalnızca o kurucunun kendi
  sürümlerine bildirilmiş bug id'leri kabul edilir.

Parola hash'i hiçbir yanıtta istemciye gönderilmez.

### Eşzamanlılık

Sunucu bir revizyon sayacı tutar; her başarılı eylem artırır. İstemci 5 saniyede bir
`GET /api/state/rev` ile yoklar ve numara değiştiyse ekranı tazeler (kullanıcı bir forma
yazarken tazeleme yapılmaz). Çakışan işlemler — ör. dolu bir slota başvuru — sunucuda
**409** ile reddedilir; istemci state'i tazeleyip nedenini gösterir.

---

## Güvenlik

- **Parolalar** BCrypt ile hash'lenir; hash asla istemciye gitmez.
- **Erişim jetonu** 20 dakikalıktır ve iptal edilemez; **yenileme jetonu** veritabanında
  (yalnızca SHA-256 özeti) tutulur, dolayısıyla iptal edilebilir. Her kullanımda döndürülür;
  kullanılmış bir jeton ikinci kez gelirse sızıntı varsayılıp o kullanıcının tüm oturumları
  kapatılır.
- **Kaba kuvvet** — e-posta başına 5, IP başına 20 başarısız deneme (15 dakikalık pencere);
  aşılınca 429 ve `Retry-After`. Sayaçlar veritabanında tutulduğu için sunucu yeniden başlasa
  da korunur. Hatalı parola ile olmayan hesap **aynı** mesajı döndürür (hesap varlığı sızmaz).
- **HTTPS** — üretimde zorunludur (HSTS + 308 yönlendirme). Geliştirmede `-Https` bayrağıyla
  açılır, http demo kolaylığı için açık kalır.
- **Sırlar** — `appsettings.json` içinde sır **yoktur**. Çözüm sırası: ortam değişkeni →
  user-secrets → (yalnızca geliştirmede) yerel varsayılan / otomatik üretilen `.dev-jwt-key`.
  Üretimde eksik sır sessizce tolere edilmez; uygulama açılışta hata verir.

```powershell
# Üretim yapılandırması
$env:ConnectionStrings__Postgres = "Host=...;Database=betakas;Username=...;Password=..."
$env:Jwt__Key = "<en az 32 karakter rastgele değer>"
$env:ASPNETCORE_ENVIRONMENT = "Production"
```

---

## Supabase ile çalıştırma (en kolay yol)

**Supabase barındırılan PostgreSQL'dir** — ayrı bir sağlayıcı, ayrı bir kod yolu yoktur.
`Database:Provider` yine `Postgres` kalır; değişen tek şey bağlantı dizesidir. Yerel PostgreSQL
kurmak zorunda kalmazsın ve veritabanı her yerden erişilebilir olur.

```powershell
.\supabase-kur.ps1
```

Betik bağlantı dizesini sorar (**ekranda görünmez**), `dotnet user-secrets`'e yazar — yani
depoya, `appsettings.json`'a veya log'a hiçbir şey sızmaz — bağlantıyı dener ve migration'ları
uygular. Sonra normal başlatırsın:

```powershell
.\baslat.ps1
```

### Bağlantı dizesini nereden alacaksın

Supabase panosu → **Project Settings → Database → Connection string → .NET**
veya **Connection pooling** bölümündeki *Session pooler* adresi.

```
Host=aws-0-eu-central-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.<proje-ref>;Password=***
```

Betik `postgresql://user:pass@host:port/db` biçimini de kabul eder, ADO.NET biçimine kendisi çevirir.

### Bilmen gereken üç şey

| Konu | Durum |
|---|---|
| **TLS** | Supabase şifresiz bağlantı kabul etmez. `SSL Mode=Require` verilmemişse kod otomatik ekler ([PostgresConnection.cs](server/Betakas.Api/Services/PostgresConnection.cs)). |
| **Havuz portu** | *Transaction pooler* (6543) hazırlanmış ifadeleri desteklemez. Bu port algılanırsa Npgsql'in otomatik hazırlaması kapatılır. **Migration için 5432 (session pooler) kullan.** |
| **Doğrudan bağlantı** | `db.<ref>.supabase.co` adresi IPv6 gerektirir; IPv4 ağındaysan bağlanamazsın. Pooler adresini kullan. |

Uzak veritabanında geçici ağ hataları için EF Core yeniden deneme açıktır (3 deneme, 5 sn arayla).

> **Testler yerel kalır.** `dotnet test` her koşuda tüm tabloları silip yeniden tohumladığı
> için Supabase'e yönlendirilmez — aksi halde gerçek verini siler. Testler yerel
> `betakas_test` veritabanını kullanır. Bilerek değiştirmek istersen:
> `.\supabase-kur.ps1 -TestDatabase <ayri-veritabani-adi>`

---

## Vercel'e dağıtım

Uygulama Vercel'de **container olarak** çalışır. Vercel .NET'i yerel bir runtime olarak
desteklemez (Node.js, Bun, Python, Rust, Go, Ruby, Wasm, Edge) ama kök dizindeki
`Dockerfile.vercel` dosyasını görüp tüm trafiği o imaja yönlendirir. Container Registry
ücretsiz **Hobby** planında da kullanılabilir.

```powershell
npx vercel login      # bir kere, tarayıcı açılır
.\vercel-dagit.ps1
```

Betik bağlantı dizesini ve JWT anahtarını user-secrets'ten alıp Vercel ortam değişkenlerine
yazar, sonra dağıtımı başlatır. **İmaj Vercel tarafında derlenir — bu makinede Docker gerekmez.**

İlk dağıtımda Vercel proje adı gibi sorular sorar. **Framework Preset `Other` olmalı**;
Vite otomatik algılanırsa container atlanır ve yalnızca statik dosyalar yayınlanır.

### İmaj ne yapıyor

`Dockerfile.vercel` üç aşamalı: React derlenir → .NET yayınlanır → çalışma imajında ikisi
birleşir. API derlenmiş SPA'yi de servis ettiği için tek servis yeterlidir, CORS gerekmez.

Arayüzün yeri `Ui__Root` ile açıkça bildirilir; Kestrel `$PORT`'a bağlanır (Vercel'in beklediği gibi).

### Ortam değişkenleri

| Değişken | Ne için |
|---|---|
| `ConnectionStrings__Postgres` | Supabase **pooler** adresi (aşağıya bak) |
| `Jwt__Key` | Üretim imza anahtarı — betik üretip user-secrets'te saklar |
| `Database__Provider` | `Postgres` |

`ASPNETCORE_ENVIRONMENT=Production` ve `Ui__Root` imajda tanımlıdır.

> Üretimde eksik sır sessizce tolere edilmez: `Jwt__Key` ya da bağlantı dizesi yoksa uygulama
> açılışta hata verir. Bu bilinçlidir — varsayılan bir anahtarla canlıya çıkılamaz.

### ⚠️ Pooler zorunlu (IPv4)

Supabase'in **doğrudan** adresi (`db.<ref>.supabase.co`) yalnızca IPv6 üzerinden erişilebilir.
Vercel IPv4 kullandığı için dağıtımda mutlaka **pooler** adresi gerekir:

```
aws-0-<bölge>.pooler.supabase.com:5432    (session pooler)
```

Betik bunu kendisi denetler; aktif bağlantı doğrudan adresteyse pooler yedeğine geçer.

Session havuzu (5432) seçilir çünkü migration'lar oturum düzeyinde advisory lock kullanır;
işlem havuzu (6543) bunu desteklemez.

### Ters vekil arkasında HTTPS

Vercel TLS'i kenarda sonlandırıp uygulamaya düz HTTP iletir. `X-Forwarded-Proto` okunmazsa
`UseHttpsRedirection` isteği zaten https olan adrese tekrar yönlendirir ve sonsuz döngü olur.
Bu yüzden `UseForwardedHeaders()` diğer her şeyden önce çalışır (bkz. Program.cs).

### Bilinmesi gerekenler

- **Soğuk başlangıç.** Container üretimde 5 dakika trafik almazsa ölçeklenip kapanır. Sonraki
  istek konteyneri yeniden başlatır — .NET için birkaç saniye. Arkadaşın ara sıra girecekse
  ilk açılış yavaş gelebilir.
- **Bölge.** `vercel.json` içinde `fra1` (Frankfurt) seçilidir — Türkiye'ye yakın. Ama
  veritabanı Seul'de olduğu için her sorgu Frankfurt→Seul gidip gelir. Asıl çözüm Supabase
  projesini Avrupa'ya taşımaktır; Supabase mevcut projenin bölgesini değiştirmediği için
  yeni proje açıp veriyi taşımak gerekir.
- **Hobby planı ticari kullanım içindir değildir.** Demo/portfolyo için uygundur.
- **İmaj depolama** $0,10/GB. .NET imajı ~100 MB, aylık maliyeti ihmal edilebilir.

### Arkadaşın kayıt olması

Dağıtımdan sonra link herkese açıktır. Arkadaşın giriş ekranından **"Testçi olarak başvur"**
(veya "Ekosisteme başvur") ile kendi hesabını açar, kendi şifresini belirler. Başvurusu
`onay bekliyor` durumunda düşer ve **sen yönetim panelinden onaylayana kadar giriş yapamaz**.
Onayladığında hesabına 100 başlangıç tokeni tanımlanır.

## Performans (uzak veritabanı)

Supabase gibi uzak bir veritabanında gecikme baskındır. Bu projede iki iyileştirme uygulanır
([StateService.cs](server/Betakas.Api/Services/StateService.cs)):

1. **Revizyona dayalı önbellek** — sunucu tek yazardır ve her eylem `rev`'i artırır, dolayısıyla
   rev değişmediyse veri de değişmemiştir. Sayfa yüklemeleri tek bir ucuz `rev` sorgusuna iner.
   Sıfırlama rev sayacını geri sardığı için orada önbellek açıkça temizlenir — geçersizleştirme
   `SeedService.ResetAsync` içinde, mutasyonun yanında durur ki hangi yoldan çağrılırsa çağrılsın
   (uç nokta, testler) atlanmasın.
2. **Paralel toplama** — önbellek ıskalandığında dokuz tablo ayrı bağlantılardan aynı anda
   okunur; toplam süre dokuz sorgunun toplamı değil, en yavaşı kadar olur.

Ayrıca uzak bağlantılarda havuz sıcak tutulur (`MinPoolSize`, `KeepAlive`) — her yeni bağlantı
bir TLS el sıkışması demektir.

Seul bölgesindeki bir Supabase projesine Türkiye üzerinden ölçüm (gidiş-dönüş ~250 ms):

| Uç | Önce | Sonra |
|---|---|---|
| `/api/public/state` (giriş ekranı) | 2,09 s | **0,29 s** |
| `/api/state` (her sayfa) | 3,28 s | **0,60 s** |
| `/api/state/rev` (yoklama) | 0,30 s | 0,30 s |

> **Sıfırlama hâlâ ~16 saniye sürer.** 11 tabloyu silip yeniden yazdığı için gidiş-dönüş sayısı
> yüksektir ve önbellekle hızlandırılamaz. Sunum sırasında sıfırlayacaksan bunu hesaba kat;
> yerel PostgreSQL'de aynı işlem ~1 saniyedir.
>
> Kalıcı çözüm bölgeyi yakınlaştırmaktır. Supabase mevcut projenin bölgesini değiştirmediği için
> yeni proje açıp veriyi taşımak gerekir.

### Pooler yerine doğrudan bağlantı

Bu kurulumda Supabase'in **pooler'ı devre dışıdır**; bağlantı doğrudan
`db.<ref>.supabase.co` adresinedir. Sebep: pooler (Supavisor) veritabanından kimlik
bilgilerini alamadı ve devre kesici yeni bağlantıları blokladı:

```
FATAL: (EAUTHQUERY) auth_query secret check timed out
FATAL: (ECIRCUITBREAKER) failed to retrieve database credentials
```

Doğrudan bağlantı IPv6 gerektirir. Pooler düzelirse geri dönmek için eski dize
`ConnectionStrings:PostgresPooler` adıyla user-secrets'te saklıdır.

> Doğrudan bağlantının kısıtı: havuzlama yapmaz, Supabase'in eşzamanlı bağlantı limiti daha
> düşüktür (nano örnekte ~60). Tek kişilik demo için sorun değil, gerçek yük altında pooler gerekir.

## Veritabanı sağlayıcısı

Diyagramdaki iki seçenek de desteklenir. Şema ve ilişkiler aynıdır; yalnızca sütun tipleri
değişir. Migration setleri context tipine bağlıdır, bu yüzden her sağlayıcının kendi türetilmiş
context'i ve kendi migration klasörü vardır:

```
Migrations/Postgres/    -> PostgresDbContext   (text[], jsonb)
Migrations/SqlServer/   -> SqlServerDbContext  (nvarchar(max))
```

Sağlayıcıyı değiştirmek:

```powershell
.\baslat.ps1 -Provider SqlServer
# veya kalıcı olarak appsettings.json -> "Database": { "Provider": "SqlServer" }
```

> ### ℹ️ SQL Server yolunun doğrulama durumu
> **PostgreSQL** uçtan uca çalışır durumda: migration'lar uygulandı, 30 test ve tarayıcı
> testleri bu sağlayıcıya karşı geçti.
>
> **SQL Server** tarafında model ve migration'lar hazır ve doğrulandı — EF Core, modeli
> SQL Server sağlayıcısına karşı derleyip tam şema betiğini üretiyor (11 tablo, `nvarchar(max)`
> eşlemeleri doğru, PostgreSQL'e özgü tip sızmıyor). Betiği görmek için:
>
> ```powershell
> cd server\Betakas.Api
> dotnet ef migrations script --context SqlServerDbContext --idempotent
> ```
>
> Ancak **canlı bir SQL Server örneğine karşı çalıştırılmadı**: bu makinede SQL Server kurulu
> değil ve kurulum yönetici yükseltmesi (UAC) gerektirdiği için tamamlanamadı. Kurduktan sonra
> doğrulamak için:
>
> ```powershell
> $env:ConnectionStrings__SqlServer = "Server=localhost\SQLEXPRESS;Database=betakas;Trusted_Connection=True;TrustServerCertificate=True"
> .\baslat.ps1 -Provider SqlServer
> ```

Yeni bir alan eklediğinde her iki sete de migration üret:

```powershell
cd server\Betakas.Api
dotnet ef migrations add <Ad> --context PostgresDbContext  --output-dir Migrations/Postgres
dotnet ef migrations add <Ad> --context SqlServerDbContext --output-dir Migrations/SqlServer
```

---

## Kurulum (sıfırdan bir makinede)

```powershell
winget install --id Microsoft.DotNet.SDK.9 -e
winget install --id PostgreSQL.PostgreSQL.17 -e --override "--mode unattended --unattendedmodeui none --superpassword betakas123 --serverport 5432 --locale C"
dotnet tool install --global dotnet-ef
```

Veritabanlarını oluştur (uygulama şemayı kendisi kurar):

```powershell
$env:PGPASSWORD='betakas123'
$psql = 'C:\Program Files\PostgreSQL\17\bin\psql.exe'
& $psql -U postgres -h localhost -c "CREATE DATABASE betakas ENCODING 'UTF8' TEMPLATE template0;"
& $psql -U postgres -h localhost -c "CREATE DATABASE betakas_test ENCODING 'UTF8' TEMPLATE template0;"
```

> ### ⚠️ Türkçe Windows'ta `--locale C` şart
> Bu bayrak olmadan PostgreSQL kurulumu **başarısız olur**. Kurucu, veritabanı kümesini sistem
> yerel ayarıyla oluşturmaya çalışır ve initdb şu hatayı verir:
>
> ```
> initdb: hata: locale name "Turkish_Türkiye.1254" contains non-ASCII characters
> ```
>
> "Türkiye" içindeki `ü` initdb tarafından kabul edilmiyor. `--locale C` bunu aşar; veritabanı
> `ENCODING 'UTF8'` ile oluşturulduğu için Türkçe karakterler sorunsuz saklanır.

SQL Server kullanacaksan ayrıca:

```powershell
winget install --id Microsoft.SQLServer.2022.Express -e --override "/ACTION=Install /QUIET /IACCEPTSQLSERVERLICENSETERMS /INSTANCENAME=SQLEXPRESS /FEATURES=SQLENGINE /TCPENABLED=1"
```

---

## Prototipe göre davranış farkları

Tek kullanıcılı demo varsayımları çok kullanıcılı ortamda geçerli olmadığı için:

- **Hesap değiştirme parola ister.** Eski "Demo Personası" menüsü oturumu parolasız
  değiştiriyordu; artık seçilen hesabın e-postası dolu olarak giriş ekranına düşer.
  Sunum Modu turundaki rol geçişleri de bu yüzden giriş ekranından geçer.
- **"Demoyu Sıfırla" yalnızca yönetimde** — ortak veritabanını sıfırlar.
- **Kayıt formunda şifre alanı var** (eskiden herkese `betakas` atanıyordu).
- **Public profil** (`#/t/<id>`) giriş gerektirmeden çalışmaya devam eder; redakte uçtan
  beslenir — feedback metinleri ve e-postalar bu yükte yoktur.

---

## Klasör yapısı

```
betakas-platform/
├─ baslat.ps1                        Başlatma betiği (-Https, -Provider)
├─ server/
│  ├─ Betakas.Api/
│  │  ├─ Program.cs                  Yapılandırma, auth uçları, statik dosya sunumu
│  │  ├─ Endpoints/                  Domain ve public uçları
│  │  ├─ Models/Entities.cs          11 tablonun EF Core karşılığı
│  │  ├─ Data/                       DbContext, sağlayıcı context'leri, jsonb dönüştürücüler
│  │  ├─ Migrations/{Postgres,SqlServer}/
│  │  ├─ Dto/                        İstek/yanıt şekilleri
│  │  └─ Services/
│  │     ├─ EconomyService.cs        Paketler, planlar, komisyon, ödeme oranı
│  │     ├─ LedgerService.cs         Çift kayıtlı defterler, escrow, id sayacı
│  │     ├─ ReputationService.cs     İtibar ve token çarpanı
│  │     ├─ *Actions.cs              24 domain eylemi (kurallar burada)
│  │     ├─ TokenService.cs          JWT + yenileme jetonu rotasyonu
│  │     ├─ LoginThrottle.cs         Kaba kuvvet sayaçları
│  │     ├─ BugExtractor.cs          Serbest metinden numaralı bug listesi
│  │     └─ SeedService*.cs          Demo veri
│  └─ Betakas.Api.Tests/             30 tümleşik test
└─ web/
   ├─ index.html                     Arayüz
   └─ api.js                         Sunucu köprüsü (JWT, otomatik tazeleme, 24 eylem)
```

---

## Arayüz (React)

Arayüz **React 19 + TypeScript + Vite**'tır ve `client/` altındadır.

```powershell
cd client
npm install
npm run dev     # http://localhost:5173 — /api istekleri 5187'ye proxy'lenir
npm run build   # client/dist üretir; API bunu servis eder
```

`baslat.ps1` ilk çalıştırmada `client/dist` yoksa arayüzü kendisi derler.

### Yapı

```
client/src/
├─ lib/
│  ├─ types.ts        Sunucudaki StateDto'nun TypeScript karşılığı
│  ├─ api.ts          24 domain ucu + JWT + otomatik jeton tazeleme
│  ├─ derive.ts       Salt-okunur türetmeler (bakiye, itibar, escrow, sıralama)
│  ├─ bugs.ts         Bug çıkarımı ve sürüm notu tikleri
│  └─ constants.ts    Paketler, planlar, teslim formu şablonları
├─ state/
│  └─ BetakasProvider.tsx   Tek state kaynağı, eylem sarmalayıcısı, yoklama
├─ components/        Shell (sidebar/topbar), ui.tsx, Avatar, Toasts
├─ pages/             14 ekran
└─ styles/            betakas.css (özgün tema) + app.css (bileşen yerleşimi)
```

### Tasarım kararları

- **Optimistic update yok.** Her eylem sunucudan güncel state'i döndürür ve onu yerleştiririz;
  ekranda gördüğün her zaman sunucunun gerçeğidir. Bir eylem sunucuda reddedilirse ekran
  değişmez, hata mesajı gösterilir.
- **İş mantığı taşınmadı** — zaten sunucudaydı. `derive.ts` yalnızca *görüntüleme* için
  hesap yapar (bakiye, itibar); bunların doğrulaması sunucudaki `LedgerService` /
  `ReputationService` içindedir.
- **Yönlendirme URL tabanlıdır**: `/panel`, `/talep/r1`, `/surum/v2`, `/t/<id>`. Derin linkler
  ve sayfa yenileme çalışır — API tarafındaki SPA fallback bunu sağlar.
- **Rol koruması iki katmanlı**: istemcide `Protected` bileşeni yanlış rolü kendi paneline
  yollar, sunucu ise uçlarda ayrıca doğrular.

> **Eski arayüz** `web/` altında duruyor. `client/dist` yoksa API otomatik ona düşer.
> Silmek istersen `web/` klasörünü kaldırman yeterli — başka hiçbir yere bağlı değil.

---

## Sorun giderme

| Belirti | Sebep / çözüm |
|---|---|
| "Sunucuya ulaşılamıyor" ekranı | API çalışmıyor — `.\baslat.ps1` |
| `Jwt:Key yapılandırılmamış` | Production ortamında sır verilmemiş; `$env:Jwt__Key` ayarla veya Development'ta çalıştır |
| `Npgsql...refused` | PostgreSQL durmuş: `Get-Service postgresql*` → `Start-Service` |
| `database "betakas" does not exist` | Yukarıdaki `CREATE DATABASE` adımlarını çalıştır |
| initdb "non-ASCII characters" | Kurulumda `--locale C` bayrağını unutma |
| Giriş "Çok fazla başarısız deneme" | Kaba kuvvet kilidi; 15 dakika bekle veya `login_attempts` tablosunu boşalt |
| `dotnet test` bağlanamıyor | `betakas_test` veritabanı yok — yukarıdaki komutla oluştur |
| Veriler karıştı | Yönetim olarak gir → **Demoyu Sıfırla** |

## İlişkili sürümler

- `../betakas-version2/` — localStorage sürümü (kurulum istemez, sunum için hâlâ çalışır)
- `../betakas-poc/` — sürüm 1, token ekonomisi öncesi
