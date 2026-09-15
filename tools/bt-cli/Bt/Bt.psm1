function bt {
    <#
        .SYNOPSIS
        Klient budget-tracker CLI. `bt <rzeczownik> <czasownik> --flagi`, np. `bt budget list`.
        Wysyla linie na POST /api/cli/execute pod adresem z $env:BT_API_URL.
    #>
    [CmdletBinding()]
    param(
        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]]$Tokens
    )

    if (-not $env:BT_API_URL) {
        Write-Error "BT_API_URL nie jest ustawiony. Uruchom install.ps1 albo: `$env:BT_API_URL = 'http://localhost:5031'"
        return
    }

    # PowerShell juz rozdzielil argumenty po spacjach — token ze spacja w srodku (np. wartosc flagi
    # albo surowy JSON) trzeba z powrotem opakowac w cudzyslow, zeby CLI-owy tokenizer zobaczyl go
    # jako jeden token. JSON (zaczyna sie od { albo [) idzie w pojedynczym cudzyslowie — tak samo jak
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

    try {
        $result = Invoke-RestMethod -Method Post -Uri $uri -ContentType 'application/json; charset=utf-8' -Body $bodyBytes
    } catch {
        # Windows PowerShell 5.1 (WebException) nie wypelnia $_.ErrorDetails.Message dla
        # Invoke-RestMethod tak jak PowerShell 7 (HttpResponseException) — trzeba samemu
        # przeczytac cialo odpowiedzi ze strumienia, inaczej blad z API (np. zly GUID) ginie
        # za goloslownym "The remote server returned an error: (400) Bad Request.".
        $body = $_.ErrorDetails.Message
        if (-not $body -and $_.Exception.Response -and ($_.Exception.Response | Get-Member -Name GetResponseStream)) {
            try {
                $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $body = $reader.ReadToEnd()
            } catch { }
        }
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
