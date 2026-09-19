<#
.SYNOPSIS
    Eksportuje zaufany certyfikat deweloperski ASP.NET do plikow PEM dla `ng serve` (web/.certs).

.DESCRIPTION
    Identity, API i front dzialaja lokalnie na https. Kestrel bierze certyfikat deweloperski sam z magazynu
    Windows, ale `ng serve` potrzebuje go w plikach - stad eksport. Wszystkie trzy uzywaja TEGO SAMEGO certyfikatu
    (CN=localhost), wiec przegladarka ufa im od razu, jesli ufa jemu.

    Katalog web/.certs jest w .gitignore: klucz prywatny nie moze trafic do repo. To certyfikat tylko dla
    localhost, ale nadal klucz - nie kopiuj go na inne maszyny.

    Jednorazowo, jesli certyfikat nie jest jeszcze zaufany (wyskoczy okno Windows z prosba o potwierdzenie):
        dotnet dev-certs https --trust
#>
param(
    [string]$OutDir = (Join-Path $PSScriptRoot '..\web\.certs')
)

$ErrorActionPreference = 'Stop'

dotnet dev-certs https --check --trust | Out-Null
if ($LASTEXITCODE -ne 0) {
    Write-Host 'Certyfikat deweloperski nie jest zaufany. Uruchom: dotnet dev-certs https --trust' -ForegroundColor Red
    exit 1
}

New-Item -ItemType Directory -Path $OutDir -Force | Out-Null
$pem = Join-Path $OutDir 'localhost.pem'

# --format Pem tworzy dwa pliki obok siebie: localhost.pem (certyfikat) i localhost.key (klucz prywatny).
dotnet dev-certs https --export-path $pem --format Pem --no-password
if ($LASTEXITCODE -ne 0) { throw 'Eksport certyfikatu nie powiodl sie.' }

Write-Host "Zapisano: $pem oraz localhost.key w $OutDir" -ForegroundColor Green
