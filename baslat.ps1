# Betakas platformunu baslatir: bagimliliklari denetler, sonra API'yi calistirir.
# Arayuz API ile ayni adresten servis edilir - ayrica bir web sunucusu gerekmez.
#
#   .\baslat.ps1              -> http://localhost:5187
#   .\baslat.ps1 -Https       -> https://localhost:7187 de acilir (dev sertifikasi gerekir)
#   .\baslat.ps1 -Provider SqlServer

param(
    [switch]$Https,
    [ValidateSet('Postgres', 'SqlServer')]
    [string]$Provider = 'Postgres'
)

$ErrorActionPreference = 'Stop'
$ApiDir   = Join-Path $PSScriptRoot 'server\Betakas.Api'
$HttpUrl  = 'http://localhost:5187'
$HttpsUrl = 'https://localhost:7187'

# dotnet PATH'i kurulumdan hemen sonra bu oturumda guncel olmayabilir.
$env:Path = [System.Environment]::GetEnvironmentVariable('Path', 'Machine') + ';' +
            [System.Environment]::GetEnvironmentVariable('Path', 'User')

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    Write-Host ".NET SDK bulunamadi." -ForegroundColor Red
    Write-Host "Kurmak icin: winget install --id Microsoft.DotNet.SDK.9 -e"
    exit 1
}

if ($Provider -eq 'Postgres') {
    # Baglanti uzak bir sunucuya (ornegin Supabase) gidiyorsa yerel servis aranmaz.
    # Sir user-secrets'te oldugu icin burada okumak yerine yalnizca ortam degiskenine bakariz;
    # o da yoksa yerel varsayilan kullanildigi varsayilir.
    $remote = $false
    if ($env:ConnectionStrings__Postgres -and
        $env:ConnectionStrings__Postgres -notmatch 'Host\s*=\s*(localhost|127\.0\.0\.1|::1)') {
        $remote = $true
    }
    else {
        # user-secrets'te uzak bir baglanti var mi? (parola okunmaz, yalnizca Host alanina bakilir)
        Push-Location $ApiDir
        $secretHost = (dotnet user-secrets list 2>$null |
            Select-String -Pattern 'ConnectionStrings:Postgres\s*=\s*(.+)$' |
            ForEach-Object { $_.Matches[0].Groups[1].Value }) -join ''
        Pop-Location
        if ($secretHost -and $secretHost -notmatch 'Host\s*=\s*(localhost|127\.0\.0\.1|::1)') {
            $remote = $true
        }
    }

    if ($remote) {
        Write-Host "Uzak PostgreSQL kullaniliyor (Supabase vb.) - yerel servis aranmadi." -ForegroundColor DarkGray
    }
    else {
        $pg = Get-Service -Name 'postgresql*' -ErrorAction SilentlyContinue
        if (-not $pg) {
            Write-Host "Yerel PostgreSQL servisi bulunamadi." -ForegroundColor Red
            Write-Host "Ya kur (README.md) ya da Supabase'e baglan: .\supabase-kur.ps1" -ForegroundColor Yellow
            exit 1
        }
        if ($pg.Status -ne 'Running') {
            Write-Host "PostgreSQL durmus, baslatiliyor..." -ForegroundColor Yellow
            Start-Service $pg.Name
        }
    }
}
else {
    $ms = Get-Service -Name 'MSSQL$*', 'MSSQLSERVER' -ErrorAction SilentlyContinue | Select-Object -First 1
    if (-not $ms) {
        Write-Host "SQL Server servisi bulunamadi. Kurulum icin README.md'ye bak." -ForegroundColor Red
        exit 1
    }
    if ($ms.Status -ne 'Running') { Start-Service $ms.Name }
}

# Yerel calistirma Development ortamindadir: sirlar verilmemisse guvenli varsayilanlara
# duser ve JWT anahtari .dev-jwt-key dosyasinda uretilir. Uretimde bu degerler
# ortam degiskeni olarak verilmelidir; aksi halde uygulama acilista hata verir.
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:Database__Provider = $Provider

if ($Https) {
    # Tarayicinin uyari vermemesi icin gelistirme sertifikasina bir kez guvenilmelidir.
    dotnet dev-certs https --check --trust | Out-Null
    if ($LASTEXITCODE -ne 0) {
        Write-Host "HTTPS gelistirme sertifikasina guveniliyor (bir kerelik)..." -ForegroundColor Yellow
        dotnet dev-certs https --trust | Out-Null
    }
    $env:ASPNETCORE_URLS = "$HttpsUrl;$HttpUrl"
    $target = $HttpsUrl
}
else {
    $env:ASPNETCORE_URLS = $HttpUrl
    $target = $HttpUrl
}

# React arayuzu derlenmemisse burada derlenir; aksi halde API eski web/ surumune duser.
$clientDir = Join-Path $PSScriptRoot 'client'
$distIndex = Join-Path $clientDir 'dist\index.html'
if ((Test-Path $clientDir) -and -not (Test-Path $distIndex)) {
    if (Get-Command npm -ErrorAction SilentlyContinue) {
        Write-Host "React arayuzu derleniyor (ilk calistirma)..." -ForegroundColor Yellow
        Push-Location $clientDir
        if (-not (Test-Path 'node_modules')) { npm install | Out-Null }
        npm run build | Out-Null
        Pop-Location
    }
    else {
        Write-Host "npm bulunamadi - eski web/ arayuzu kullanilacak." -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "Betakas API baslatiliyor -> $target" -ForegroundColor Cyan
Write-Host "Veritabani: $Provider  |  Demo sifresi: betakas  |  Durdurmak icin Ctrl+C" -ForegroundColor DarkGray
Write-Host ""

Set-Location $ApiDir
dotnet run --no-launch-profile
