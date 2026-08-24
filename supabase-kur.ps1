# Betakas'i Supabase'e baglar.
#
# Supabase barindirilan PostgreSQL'dir; ayri bir saglayici gerekmez. Bu betik yalnizca
# baglanti dizesini GUVENLI bir yere (dotnet user-secrets) yazar, baglantiyi dogrular ve
# semayi olusturur. Parola hicbir dosyaya, depoya veya log'a yazilmaz.
#
#   .\supabase-kur.ps1
#
# Baglanti dizesini nereden alacaksin:
#   Supabase panosu -> Project Settings -> Database -> Connection string -> ".NET"
#   ya da "Connection pooling" bolumundeki Session pooler adresi (port 5432).
#
# NOT: Migration uygularken SESSION havuzunu (5432) veya dogrudan baglantiyi kullan.
#      Transaction havuzu (6543) hazirlanmis ifadeleri desteklemez; uygulama calisirken
#      sorun cikarmaz (kod otomatik ayarliyor) ama migration icin 5432 daha guvenlidir.

[CmdletBinding()]
param(
    # Testler VARSAYILAN OLARAK YEREL PostgreSQL'de kalir.
    # Testler her kosuda tum tablolari silip yeniden tohumlar; Supabase'deki gercek veriyi
    # silmemesi icin oraya yonlendirilmez. Yine de istersen bos olmayan bir ad ver.
    [string]$TestDatabase = ''
)

$ErrorActionPreference = 'Stop'
$ApiDir  = Join-Path $PSScriptRoot 'server\Betakas.Api'
$TestDir = Join-Path $PSScriptRoot 'server\Betakas.Api.Tests'

$env:Path = [System.Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' +
            [System.Environment]::GetEnvironmentVariable('Path', 'User') + ';' +
            "$env:USERPROFILE\.dotnet\tools"

Write-Host ""
Write-Host "Betakas -> Supabase baglantisi" -ForegroundColor Cyan
Write-Host "------------------------------" -ForegroundColor Cyan
Write-Host "Supabase panosundan: Project Settings > Database > Connection string > .NET"
Write-Host "Ornek:" -ForegroundColor DarkGray
Write-Host "  Host=aws-0-eu-central-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.abcdefgh;Password=***" -ForegroundColor DarkGray
Write-Host ""

# Parola ekranda gorunmesin diye guvenli girdi.
$secure = Read-Host "Baglanti dizesini yapistir" -AsSecureString
$cs = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure))

if ([string]::IsNullOrWhiteSpace($cs)) {
    Write-Host "Bos girdi, iptal edildi." -ForegroundColor Red
    exit 1
}

# Supabase panosu bazen URI bicimi verir: postgresql://user:pass@host:port/db
if ($cs -match '^postgres(ql)?://') {
    try {
        $u = [Uri]$cs
        $userInfo = $u.UserInfo -split ':', 2
        $db = $u.AbsolutePath.TrimStart('/')
        if (-not $db) { $db = 'postgres' }
        $cs = "Host=$($u.Host);Port=$($u.Port);Database=$db;Username=$($userInfo[0]);Password=$($userInfo[1])"
        Write-Host "URI bicimi ADO.NET bicimine cevrildi." -ForegroundColor DarkGray
    }
    catch {
        Write-Host "URI cozumlenemedi, oldugu gibi kullanilacak." -ForegroundColor Yellow
    }
}

if ($cs -notmatch 'Ssl\s*Mode') { $cs = "$cs;SSL Mode=Require" }
if ($cs -notmatch 'Trust\s*Server\s*Certificate') { $cs = "$cs;Trust Server Certificate=true" }

# --- Sirlari user-secrets'e yaz (depoya girmez, appsettings'e yazilmaz) ---
Push-Location $ApiDir
dotnet user-secrets set "ConnectionStrings:Postgres" $cs | Out-Null
dotnet user-secrets set "Database:Provider" "Postgres" | Out-Null
Pop-Location

Write-Host "Baglanti dizesi user-secrets'e kaydedildi." -ForegroundColor Green

if ($TestDatabase) {
    # Acikca istendi: testleri de ayni sunucudaki BASKA bir veritabanina yonlendir.
    $testCs = $cs -replace 'Database=[^;]+', "Database=$TestDatabase"
    Push-Location $TestDir
    dotnet user-secrets set "ConnectionStrings:PostgresTest" $testCs | Out-Null
    Pop-Location
    Write-Host "Testler '$TestDatabase' veritabanina yonlendirildi." -ForegroundColor Yellow
    Write-Host "DIKKAT: testler her kosuda bu veritabanini tamamen siler." -ForegroundColor Yellow
}
else {
    Write-Host "Testler yerel PostgreSQL'de birakildi (betakas_test)." -ForegroundColor DarkGray
    Write-Host "Sebep: testler her kosuda tum tablolari siler; Supabase verisi korunsun." -ForegroundColor DarkGray
}

# --- Baglantiyi dogrula ve semayi olustur ---
Write-Host ""
Write-Host "Baglanti deneniyor ve migration'lar uygulaniyor..." -ForegroundColor Cyan

$env:ConnectionStrings__Postgres = $cs
Push-Location $ApiDir
dotnet ef database update --context PostgresDbContext
$ok = $?
Pop-Location

if (-not $ok) {
    Write-Host ""
    Write-Host "Baglanti veya migration basarisiz." -ForegroundColor Red
    Write-Host "Sik nedenler:" -ForegroundColor Yellow
    Write-Host "  - Yanlis parola (Supabase panosundan 'Reset database password' ile yenileyebilirsin)"
    Write-Host "  - Dogrudan baglanti (db.<ref>.supabase.co) IPv6 gerektirir; onun yerine pooler adresini kullan"
    Write-Host "  - Kullanici adi pooler'da 'postgres.<proje-ref>' bicimindedir"
    exit 1
}

Write-Host ""
Write-Host "Hazir. Simdi calistir:" -ForegroundColor Green
Write-Host "  .\baslat.ps1" -ForegroundColor White
Write-Host ""
Write-Host "Demo veri ilk acilista otomatik tohumlanir. Testler icin:" -ForegroundColor DarkGray
Write-Host "  cd server\Betakas.Api.Tests; dotnet test" -ForegroundColor White
