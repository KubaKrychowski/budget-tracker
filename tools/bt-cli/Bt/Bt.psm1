function Get-BtTokenPath {
    $dir = Join-Path $env:LOCALAPPDATA 'bt-cli'
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    Join-Path $dir 'token.json'
}

function Get-BtIdentityUrl {
    if ($env:BT_IDENTITY_URL) { return $env:BT_IDENTITY_URL.TrimEnd('/') }
    return 'http://localhost:5172'
}

function Get-BtCliSecret {
    if ($env:BT_CLI_CLIENT_SECRET) { return $env:BT_CLI_CLIENT_SECRET }
    throw "BT_CLI_CLIENT_SECRET nie jest ustawiony. Ustaw go tak samo jak BT_API_URL: " +
        "[Environment]::SetEnvironmentVariable('BT_CLI_CLIENT_SECRET', '<sekret z BudgetTracker.Identity>', 'User'). " +
        "Sekret trzyma serwer tozsamosci w user-secrets (Clients:Cli:Secret) - poproc administratora/dewelopera o wartosc."
}

function ConvertFrom-BtSecureString {
    param([Parameter(Mandatory)][securestring]$Secure)
    $ptr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($Secure)
    try {
        [System.Runtime.InteropServices.Marshal]::PtrToStringBSTR($ptr)
    } finally {
        [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($ptr)
    }
}

function Save-BtToken {
    param([Parameter(Mandatory)]$TokenResponse)

    $expiresAt = (Get-Date).ToUniversalTime().AddSeconds([int]$TokenResponse.expires_in)
    $record = [ordered]@{
        access_token  = $TokenResponse.access_token
        refresh_token = $TokenResponse.refresh_token
        expires_at    = $expiresAt.ToString('o')
        identity_url  = Get-BtIdentityUrl
    }
    $record | ConvertTo-Json | Set-Content -Path (Get-BtTokenPath) -Encoding UTF8
}

function Read-BtStoredToken {
    $path = Get-BtTokenPath
    if (-not (Test-Path $path)) { return $null }
    try {
        Get-Content -Path $path -Raw | ConvertFrom-Json
    } catch {
        $null
    }
}

# Zwraca blad z odpowiedzi tokenu OpenIddict albo domyslny komunikat - ten sam wzorzec
# odczytu body bledu, co reszta modulu (WebException w Windows PowerShell 5.1 nie wypelnia
# ErrorDetails.Message tak jak w PS7).
function Read-BtHttpErrorBody {
    param($ErrorRecord)

    $body = $ErrorRecord.ErrorDetails.Message
    if (-not $body -and $ErrorRecord.Exception.Response -and ($ErrorRecord.Exception.Response | Get-Member -Name GetResponseStream)) {
        try {
            $reader = New-Object System.IO.StreamReader($ErrorRecord.Exception.Response.GetResponseStream())
            $body = $reader.ReadToEnd()
        } catch { }
    }
    return $body
}

function Invoke-BtTokenRequest {
    param([Parameter(Mandatory)][hashtable]$Form)

    $uri = "$(Get-BtIdentityUrl)/connect/token"
    try {
        Invoke-RestMethod -Method Post -Uri $uri -Body $Form -ContentType 'application/x-www-form-urlencoded'
    } catch {
        $body = Read-BtHttpErrorBody $_
        $message = $null
        if ($body) {
            try { $message = ($body | ConvertFrom-Json).error_description } catch { }
        }
        if (-not $message) { $message = $_.Exception.Message }
        throw $message
    }
}

function Connect-Bt {
    <#
        .SYNOPSIS
        Loguje do BudgetTracker.Identity (grant hasla - bt-cli jest zaufanym klientem
        pierwszej strony, patrz DECISIONS.md) i zapamietuje token lokalnie.
    #>
    try {
        $secret = Get-BtCliSecret
    } catch {
        Write-Host $_ -ForegroundColor Red
        return
    }

    $email = Read-Host 'Adres e-mail'
    $securePassword = Read-Host 'Haslo' -AsSecureString
    $password = ConvertFrom-BtSecureString $securePassword

    $form = @{
        grant_type    = 'password'
        username      = $email
        password      = $password
        scope         = 'openid profile email offline_access budgettracker_api'
        client_id     = 'bt-cli'
        client_secret = $secret
    }

    try {
        $token = Invoke-BtTokenRequest -Form $form
    } catch {
        Write-Host "Logowanie nie powiodlo sie: $_" -ForegroundColor Red
        return
    }

    Save-BtToken $token
    Write-Host "Zalogowano jako $email." -ForegroundColor Green
}

function Disconnect-Bt {
    <#.SYNOPSIS
        Usuwa lokalnie zapamietany token - kolejne "bt" poprosi o ponowne logowanie.#>
    $path = Get-BtTokenPath
    if (Test-Path $path) { Remove-Item $path -Force }
    Write-Host 'Wylogowano.'
}

# Zwraca wazny access token albo $null, jesli trzeba sie zalogowac (bt login).
# Odswieza cicho, gdy access token wygasl, a jest jeszcze refresh_token.
function Get-BtAccessToken {
    $stored = Read-BtStoredToken
    if (-not $stored) { return $null }

    # [datetime] rzutowany na string z "Z" konwertuje na czas LOKALNY zamiast zostac przy UTC -
    # porownanie z ToUniversalTime() wtedy klamie o kilka godzin. DateTimeOffset::Parse z
    # RoundtripKind zachowuje offset z formatu "o" zapisanego w Save-BtToken.
    $expiresAt = [datetimeoffset]::Parse(
        $stored.expires_at, [System.Globalization.CultureInfo]::InvariantCulture,
        [System.Globalization.DateTimeStyles]::RoundtripKind)
    if ([datetimeoffset]::UtcNow.AddSeconds(30) -lt $expiresAt) {
        return $stored.access_token
    }

    if (-not $stored.refresh_token) { return $null }

    try {
        $token = Invoke-BtTokenRequest -Form @{
            grant_type    = 'refresh_token'
            refresh_token = $stored.refresh_token
            client_id     = 'bt-cli'
            client_secret = Get-BtCliSecret
        }
    } catch {
        return $null
    }

    Save-BtToken $token
    return $token.access_token
}

function bt {
    <#
        .SYNOPSIS
        Klient budget-tracker CLI. `bt <rzeczownik> <czasownik> --flagi`, np. `bt budget list`.
        Wysyla linie na POST /api/cli/execute pod adresem z $env:BT_API_URL.
        `bt login` / `bt logout` obsluguje logowanie do BudgetTracker.Identity.
    #>
    [CmdletBinding()]
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Tokens
    )

    if ($Tokens.Count -ge 1 -and $Tokens[0] -eq 'login') { Connect-Bt; return }
    if ($Tokens.Count -ge 1 -and $Tokens[0] -eq 'logout') { Disconnect-Bt; return }

    if (-not $env:BT_API_URL) {
        Write-Error "BT_API_URL nie jest ustawiony. Uruchom install.ps1 albo: `$env:BT_API_URL = 'http://localhost:5031'"
        return
    }

    $accessToken = Get-BtAccessToken
    if (-not $accessToken) {
        Write-Host "Nie jestes zalogowany. Uruchom: bt login" -ForegroundColor Yellow
        return
    }

    # PowerShell juz rozdzielil argumenty po spacjach - token ze spacja w srodku (np. wartosc flagi
    # albo surowy JSON) trzeba z powrotem opakowac w cudzyslow, zeby CLI-owy tokenizer zobaczyl go
    # jako jeden token. JSON (zaczyna sie od { albo [) idzie w pojedynczym cudzyslowie - tak samo jak
    # w panelu terminala w apce, zeby wlasne " w srodku JSON-a nie kolidowaly.
    $line = ($Tokens | ForEach-Object {
        if ($_ -notmatch '\s') {
            $_
        } elseif ($_ -match '^[\[{]') {
            "'$_'"
        } else {
            '"' + ($_ -replace '"', '\"') + '"'
        }
    }) -join ' '

    $uri = "$($env:BT_API_URL.TrimEnd('/'))/api/cli/execute"
    $bodyBytes = [System.Text.Encoding]::UTF8.GetBytes((@{ line = $line } | ConvertTo-Json -Compress))
    $headers = @{ Authorization = "Bearer $accessToken" }

    try {
        $result = Invoke-RestMethod -Method Post -Uri $uri -ContentType 'application/json; charset=utf-8' -Body $bodyBytes -Headers $headers
    } catch {
        if ($_.Exception.Response -and [int]$_.Exception.Response.StatusCode -eq 401) {
            Write-Host "Sesja wygasla. Uruchom: bt login" -ForegroundColor Yellow
            return
        }

        # Windows PowerShell 5.1 (WebException) nie wypelnia $_.ErrorDetails.Message dla
        # Invoke-RestMethod tak jak PowerShell 7 (HttpResponseException) - trzeba samemu
        # przeczytac cialo odpowiedzi ze strumienia, inaczej blad z API (np. zly GUID) ginie
        # za goloslownym "The remote server returned an error: (400) Bad Request.".
        $body = Read-BtHttpErrorBody $_
        $message = $null
        if ($body) {
            try { $message = ($body | ConvertFrom-Json).error } catch { }
        }
        if (-not $message) { $message = $body }
        if (-not $message) { $message = $_.Exception.Message }
        Write-Host $message -ForegroundColor Red
        return
    }

    if ($null -ne $result) {
        $result | ConvertTo-Json -Depth 10
    }
}

Export-ModuleMember -Function bt
