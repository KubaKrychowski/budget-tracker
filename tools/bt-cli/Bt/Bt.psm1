function Get-BtTokenPath {
    $dir = Join-Path $env:LOCALAPPDATA 'bt-cli'
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
    Join-Path $dir 'token.json'
}

function Get-BtIdentityUrl {
    if ($env:BT_IDENTITY_URL) { return $env:BT_IDENTITY_URL.TrimEnd('/') }
    return 'https://localhost:7226'
}

# bt-cli jest klientem PUBLICZNYM (kod autoryzacyjny + PKCE, przekierowanie na loopback wg RFC 8252) - bez sekretu,
# bo CLI jest dla wszystkich uzytkownikow, a sekret rozdany kazdemu nie jest sekretem.
$script:BtClientId = 'bt-cli'
$script:BtScope = 'openid profile email offline_access budgettracker_api'

# UWAGA: lista musi byc identyczna z OAuthDefaults.CliLoopbackPorts po stronie serwera tozsamosci
# (BudgetTracker.Identity/Infrastructure/OAuthDefaults.cs). Serwer porownuje redirect_uri DOSLOWNIE, z portem,
# wiec port spoza listy konczy sie bledem "redirect_uri nie jest zarejestrowany". Pilnuje tego test w Identity.Tests.
$script:BtLoopbackPorts = @(53682, 53683, 53684, 53685, 53686)

# Ile czekamy na powrot z przegladarki, zanim uznamy logowanie za porzucone.
$script:BtLoginTimeoutSeconds = 180

function ConvertTo-BtBase64Url {
    param([Parameter(Mandatory)][byte[]]$Bytes)
    [Convert]::ToBase64String($Bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

# PKCE (RFC 7636): verifier to losowe 32 bajty, challenge to SHA-256 verifiera. Serwer dostaje challenge przy
# autoryzacji, a verifier dopiero przy wymianie kodu - kod skradziony z adresu bez verifiera jest bezuzyteczny.
# -Verifier istnieje tylko po to, zeby test mogl sprawdzic skrot na wektorze z RFC 7636; w uzyciu zawsze losowy.
function New-BtPkcePair {
    param([string]$Verifier)

    if ($Verifier) {
        $verifier = $Verifier
    } else {
        $bytes = New-Object byte[] 32
        $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
        try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
        $verifier = ConvertTo-BtBase64Url $bytes
    }

    $sha = [System.Security.Cryptography.SHA256]::Create()
    try { $hash = $sha.ComputeHash([System.Text.Encoding]::ASCII.GetBytes($verifier)) } finally { $sha.Dispose() }

    [pscustomobject]@{ Verifier = $verifier; Challenge = (ConvertTo-BtBase64Url $hash) }
}

# Otwiera nasluch na pierwszym wolnym porcie z listy. Wylacznie 127.0.0.1 - kod nie moze byc osiagalny z sieci.
function Start-BtLoopbackListener {
    foreach ($port in $script:BtLoopbackPorts) {
        $listener = New-Object System.Net.HttpListener
        $listener.Prefixes.Add("http://127.0.0.1:$port/")
        try {
            $listener.Start()
            return [pscustomobject]@{ Listener = $listener; Port = $port }
        } catch {
            $listener.Close()
        }
    }
    return $null
}

function Write-BtBrowserPage {
    param($Response, [string]$Text)

    # Statyczny tekst, nic z zadania nie jest wstawiane do strony (zadne odbicie parametrow z adresu).
    $html = "<!doctype html><html><head><meta charset='utf-8'><title>bt</title></head><body style='font-family:sans-serif'><p>$Text</p></body></html>"
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($html)
    $Response.ContentType = 'text/html; charset=utf-8'
    $Response.ContentLength64 = $bytes.Length
    $Response.OutputStream.Write($bytes, 0, $bytes.Length)
    $Response.OutputStream.Close()
}

# Czeka na przekierowanie z serwera tozsamosci na /callback. Odpytuje co sekunde, zeby Ctrl+C dzialalo.
function Wait-BtAuthorizationCode {
    param($Listener, [string]$ExpectedState)

    $deadline = (Get-Date).AddSeconds($script:BtLoginTimeoutSeconds)
    $pending = $Listener.GetContextAsync()

    while ((Get-Date) -lt $deadline) {
        if (-not $pending.Wait(1000)) { continue }

        $context = $pending.Result
        $pending = $Listener.GetContextAsync()

        if ($context.Request.Url.AbsolutePath -ne '/callback') {
            # Np. /favicon.ico z przegladarki - to nie jest powrot z logowania.
            $context.Response.StatusCode = 404
            $context.Response.Close()
            continue
        }

        $query = $context.Request.QueryString
        if ($query['error']) {
            Write-BtBrowserPage $context.Response 'Logowanie nie powiodlo sie. Wroc do terminala.'
            throw "Logowanie odrzucone: $($query['error']) $($query['error_description'])".Trim()
        }
        if ($query['state'] -ne $ExpectedState -or -not $query['code']) {
            # Inne zadanie w ten port (np. inna aplikacja) - nie ufamy mu i nie konczymy logowania.
            $context.Response.StatusCode = 400
            $context.Response.Close()
            continue
        }

        Write-BtBrowserPage $context.Response 'Zalogowano. Mozesz zamknac to okno i wrocic do terminala.'
        return $query['code']
    }

    throw "Nie doczekano sie powrotu z przegladarki w ciagu $($script:BtLoginTimeoutSeconds) s. Uruchom bt login ponownie."
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

# Token na dysku jest szyfrowany DPAPI (ConvertFrom-SecureString bez klucza): odszyfruje go tylko to samo konto
# Windows na tym samym komputerze. Refresh token zapisany jawnie pozwalalby kazdemu, kto odczyta plik (malware,
# kopia zapasowa, zrzut dysku), logowac sie bez hasla i bez 2FA az do wygasniecia tokenu.
function Write-BtTokenRecord {
    param([Parameter(Mandatory)]$Record)

    $json = $Record | ConvertTo-Json
    $protected = ConvertTo-SecureString -String $json -AsPlainText -Force | ConvertFrom-SecureString
    Set-Content -Path (Get-BtTokenPath) -Value $protected -Encoding ASCII
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
    Write-BtTokenRecord $record
}

function Read-BtStoredToken {
    $path = Get-BtTokenPath
    if (-not (Test-Path $path)) { return $null }
    try {
        $raw = (Get-Content -Path $path -Raw).Trim()

        # Starszy format to jawny JSON. Wczytujemy go jeszcze raz i od razu zapisujemy zaszyfrowany,
        # zeby po aktualizacji modulu nie zostal na dysku plik z refresh tokenem otwartym tekstem.
        if ($raw.StartsWith('{')) {
            $legacy = $raw | ConvertFrom-Json
            Write-BtTokenRecord $legacy
            return $legacy
        }

        $secure = ConvertTo-SecureString -String $raw -ErrorAction Stop
        ConvertFrom-BtSecureString $secure | ConvertFrom-Json
    } catch {
        # Uszkodzony plik albo plik z innego konta/komputera - traktujemy jak brak logowania (bt login).
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
        Loguje do BudgetTracker.Identity przez przegladarke: kod autoryzacyjny + PKCE, odbior kodu na 127.0.0.1.
        Haslo nigdy nie przechodzi przez CLI (dziala tez konto z 2FA). Token zapamietuje lokalnie.
    #>
    $server = Start-BtLoopbackListener
    if (-not $server) {
        Write-Host ("Zaden z portow " + ($script:BtLoopbackPorts -join ', ') + " nie jest wolny - zamknij program, ktory je zajmuje, i sprobuj ponownie.") -ForegroundColor Red
        return
    }

    try {
        $redirectUri = "http://127.0.0.1:$($server.Port)/callback"
        $pkce = New-BtPkcePair

        $stateBytes = New-Object byte[] 16
        $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
        try { $rng.GetBytes($stateBytes) } finally { $rng.Dispose() }
        $state = ConvertTo-BtBase64Url $stateBytes

        $query = @(
            'response_type=code'
            "client_id=$([uri]::EscapeDataString($script:BtClientId))"
            "redirect_uri=$([uri]::EscapeDataString($redirectUri))"
            "scope=$([uri]::EscapeDataString($script:BtScope))"
            "state=$state"
            "code_challenge=$($pkce.Challenge)"
            'code_challenge_method=S256'
        ) -join '&'
        $authorizeUrl = "$(Get-BtIdentityUrl)/connect/authorize?$query"

        Write-Host 'Otwieram przegladarke, zaloguj sie tam. Jesli sie nie otworzy, wklej ten adres:' -ForegroundColor Cyan
        Write-Host $authorizeUrl
        try { Start-Process $authorizeUrl } catch { }

        try {
            $code = Wait-BtAuthorizationCode -Listener $server.Listener -ExpectedState $state
        } catch {
            Write-Host "Logowanie nie powiodlo sie: $_" -ForegroundColor Red
            return
        }

        try {
            $token = Invoke-BtTokenRequest -Form @{
                grant_type    = 'authorization_code'
                code          = $code
                redirect_uri  = $redirectUri
                client_id     = $script:BtClientId
                code_verifier = $pkce.Verifier
            }
        } catch {
            Write-Host "Logowanie nie powiodlo sie: $_" -ForegroundColor Red
            return
        }

        Save-BtToken $token
        Write-Host 'Zalogowano.' -ForegroundColor Green
    } finally {
        $server.Listener.Close()
    }
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
            client_id     = $script:BtClientId
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
        `bt login` / `bt logout` obsluguje logowanie do BudgetTracker.Identity (przegladarka, bez hasla w CLI).
    #>
    [CmdletBinding()]
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Tokens
    )

    if ($Tokens.Count -ge 1 -and $Tokens[0] -eq 'login') { Connect-Bt; return }
    if ($Tokens.Count -ge 1 -and $Tokens[0] -eq 'logout') { Disconnect-Bt; return }

    if (-not $env:BT_API_URL) {
        Write-Error "BT_API_URL nie jest ustawiony. Uruchom install.ps1 albo: `$env:BT_API_URL = 'https://localhost:7133'"
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
