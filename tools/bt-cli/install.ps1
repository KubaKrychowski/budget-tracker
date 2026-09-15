#Requires -Version 5.1
<#
    Instaluje modul 'Bt' do katalogu modulow biezacego uzytkownika i ustawia BT_API_URL
    (trwale, per-uzytkownik). Po tym, w KAZDYM nowym oknie PowerShell, `bt` dziala od razu —
    bez importowania modulu recznie i bez wpisow w $PROFILE (PowerShell sam doladowuje moduly
    z $PSModulePath przy pierwszym uzyciu nierozpoznanej komendy).

    Uzycie:
        .\install.ps1                                  # zapyta o adres API przy pierwszej instalacji
        .\install.ps1 -ApiUrl http://localhost:5031     # ustawia/nadpisuje adres API bez pytania
#>
[CmdletBinding()]
param(
    [string]$ApiUrl
)

$ErrorActionPreference = 'Stop'

$source = Join-Path $PSScriptRoot 'Bt'
if (-not (Test-Path $source)) {
    throw "Nie znalazlem katalogu modulu '$source' — uruchamiaj install.ps1 z tools/bt-cli/."
}

$targetRoot = $env:PSModulePath -split ';' | Where-Object { $_ -like "$HOME*" } | Select-Object -First 1
if (-not $targetRoot) {
    throw "Nie znalazlem katalogu modulow uzytkownika w `$env:PSModulePath. Skopiuj '$source' recznie do dowolnego katalogu z `$env:PSModulePath."
}

$target = Join-Path $targetRoot 'Bt'
if (Test-Path $target) { Remove-Item $target -Recurse -Force }
Copy-Item $source $target -Recurse
Write-Host "Zainstalowano modul 'Bt' w: $target"

if (-not $ApiUrl) {
    $current = [Environment]::GetEnvironmentVariable('BT_API_URL', 'User')
    if ($current) {
        Write-Host "BT_API_URL juz ustawiony na '$current' — zostawiam bez zmian (podaj -ApiUrl, zeby nadpisac)."
    } else {
        $ApiUrl = Read-Host 'Adres API budget-tracker (np. http://localhost:5031)'
    }
}

if ($ApiUrl) {
    [Environment]::SetEnvironmentVariable('BT_API_URL', $ApiUrl, 'User')
    $env:BT_API_URL = $ApiUrl
    Write-Host "BT_API_URL ustawiony na: $ApiUrl"
}

Write-Host ''
Write-Host "Gotowe. Otworz NOWE okno PowerShell i sprawdz:  bt help"
