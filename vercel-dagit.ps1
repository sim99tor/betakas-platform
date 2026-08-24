# Betakas'i Vercel'e dagitir.
#
# Vercel .NET'i yerel runtime olarak desteklemez, ama Dockerfile.vercel ile rastgele
# container calistirir. Bu betik ortam degiskenlerini ayarlar ve dagitimi baslatir.
# Imaj Vercel tarafinda derlenir; bu makinede Docker gerekmez.
#
#   npx vercel login      (bir kere, tarayici acilir)
#   .\vercel-dagit.ps1
#
# Sirlar user-secrets'ten okunur ve Vercel'e aktarilir; ekrana yazilmaz.

[CmdletBinding()]
param(
    # Uretim yerine onizleme dagitimi yapar.
    [switch]$Preview
)

# npx/vercel stderr`e uyari yazabiliyor; PowerShell bunu NativeCommandError sayip
# betigi durdurmasin diye Stop kullanilmaz. Hatalar $LASTEXITCODE ile denetlenir.
$ErrorActionPreference = 'Continue'
$ApiDir = Join-Path $PSScriptRoot 'server\Betakas.Api'
$Target = if ($Preview) { 'preview' } else { 'production' }

$env:Path = [System.Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' +
            [System.Environment]::GetEnvironmentVariable('Path', 'User')

function Fail($msg) { Write-Host $msg -ForegroundColor Red; exit 1 }

# --- On kontroller ---
if (-not (Test-Path (Join-Path $PSScriptRoot 'Dockerfile.vercel'))) {
    Fail "Dockerfile.vercel bulunamadi. Bu betigi proje kokunden calistir."
}

Write-Host "Vercel oturumu kontrol ediliyor..." -ForegroundColor Cyan
$who = npx --yes vercel whoami 2>&1 | Out-String
if ($LASTEXITCODE -ne 0) {
    Write-Host "Vercel'e giris yapilmamis." -ForegroundColor Red
    Write-Host "Once sunu calistir:  npx vercel login" -ForegroundColor Yellow
    exit 1
}
Write-Host "Giris yapilmis: $($who.Trim())" -ForegroundColor Green

# --- Baglanti dizesi (Supabase) ---
Push-Location $ApiDir
$secrets = dotnet user-secrets list 2>$null
Pop-Location

$line = $secrets | Select-String -Pattern '^ConnectionStrings:Postgres\s*=\s*(.+)$'
if (-not $line) { Fail "user-secrets'te ConnectionStrings:Postgres yok. Once .\supabase-kur.ps1 calistir." }
$cs = $line.Matches[0].Groups[1].Value

# Vercel IPv4 kullanir; Supabase'in DOGRUDAN adresi (db.<ref>.supabase.co) IPv6 gerektirir.
# Bu yuzden dagitimda mutlaka pooler adresi kullanilmalidir.
if ($cs -match 'Host\s*=\s*db\.[^;]*\.supabase\.co') {
    $poolerLine = $secrets | Select-String -Pattern '^ConnectionStrings:PostgresPooler\s*=\s*(.+)$'
    if ($poolerLine) {
        $cs = $poolerLine.Matches[0].Groups[1].Value
        Write-Host "Dogrudan baglanti IPv6 ister; dagitim icin pooler adresi kullanilacak." -ForegroundColor Yellow
    }
    else {
        Fail "Aktif baglanti dogrudan (IPv6) ve pooler yedegi yok. Vercel'den baglanamaz."
    }
}

# --- JWT anahtari ---
# Uretimde sabit ve gizli olmali; her dagitimda degisirse aktif oturumlar duser.
$jwtLine = $secrets | Select-String -Pattern '^Jwt:ProdKey\s*=\s*(.+)$'
if ($jwtLine) {
    $jwt = $jwtLine.Matches[0].Groups[1].Value
    Write-Host "Mevcut uretim JWT anahtari kullanilacak." -ForegroundColor DarkGray
}
else {
    $bytes = New-Object byte[] 48
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($bytes)
    $jwt = [Convert]::ToBase64String($bytes)
    Push-Location $ApiDir
    dotnet user-secrets set "Jwt:ProdKey" $jwt | Out-Null
    Pop-Location
    Write-Host "Yeni uretim JWT anahtari uretildi ve user-secrets'e kaydedildi." -ForegroundColor Green
}

# --- Ortam degiskenlerini Vercel'e yaz ---
function Set-VercelEnv($name, $value) {
    # Varsa once sil ki guncel deger yazilabilsin (hata onemsiz).
    npx --yes vercel env rm $name $Target --yes 2>&1 | Out-Null

    # Deger BORU ile gecirilmez: PowerShell 5.1 cikisa UTF-8 BOM ekliyor ve BOM
    # baglanti dizesinin ilk anahtarina yapisip Npgsql`i patlatiyor.
    # Bunun yerine BOM`suz gecici dosyadan stdin yonlendirilir.
    $tmp = [System.IO.Path]::GetTempFileName()
    [System.IO.File]::WriteAllText($tmp, $value, (New-Object System.Text.UTF8Encoding($false)))
    $cmdLine = 'npx --yes vercel env add ' + $name + ' ' + $Target + ' < "' + $tmp + '"'
    cmd /c $cmdLine 2>&1 | Out-Null
    $ok = ($LASTEXITCODE -eq 0)
    Remove-Item $tmp -Force -ErrorAction SilentlyContinue

    if ($ok) { Write-Host "  $name ayarlandi" -ForegroundColor DarkGray }
    else { Write-Host "  $name AYARLANAMADI" -ForegroundColor Red }
}

# Proje bagli degilse soru sormadan bagla (varsayilan ayarlar).
if (-not (Test-Path (Join-Path $PSScriptRoot '.vercel/project.json'))) {
    Write-Host ""
    Write-Host "Proje baglaniyor..." -ForegroundColor Cyan
    npx --yes vercel link --yes 2>&1 | Out-Null
    if ($LASTEXITCODE -ne 0) { Fail "Proje baglanamadi." }
}

Write-Host ""
Write-Host "Ortam degiskenleri yaziliyor ($Target)..." -ForegroundColor Cyan
Set-VercelEnv 'ConnectionStrings__Postgres' $cs
Set-VercelEnv 'Jwt__Key' $jwt
Set-VercelEnv 'Database__Provider' 'Postgres'

# --- Dagit ---
Write-Host ""
Write-Host "Dagitim baslatiliyor (imaj Vercel'de derlenir, birkac dakika surebilir)..." -ForegroundColor Cyan
Write-Host ""

if ($Preview) { npx --yes vercel deploy --yes }
else { npx --yes vercel deploy --prod --yes }

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Dagitim basarisiz." -ForegroundColor Red
    Write-Host "Sik nedenler:" -ForegroundColor Yellow
    Write-Host "  - Proje ilk kez dagitiliyorsa sorulara cevap vermen gerekir (proje adi vb.)"
    Write-Host "  - Framework Preset 'Other' olmali; Vite otomatik algilanirsa container atlanir"
    Write-Host "  - Loglar: npx vercel logs <dagitim-url>"
    exit 1
}

Write-Host ""
Write-Host "Dagitim tamam." -ForegroundColor Green
Write-Host "Demo sifresi: betakas - arkadasin kayit olabilir, sen yonetimden onaylarsin." -ForegroundColor DarkGray
