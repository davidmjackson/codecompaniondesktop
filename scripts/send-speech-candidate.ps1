[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Text,

    [string] $ProjectId = "codecompaniondesktop",

    [string] $DisplayName = "Code Companion Desktop",

    [string] $Root = "D:\Development\CodeCompanionDesktop",

    [string] $SpeechHint = "manual-speak-last",

    [string] $MessageId,

    [string] $InboxPath,

    [switch] $WaitForPlayback,

    [int] $PlaybackTimeoutSeconds = 180,

    [string] $BridgeHealthUrl = "http://127.0.0.1:47321/health"
)

$ErrorActionPreference = "Stop"

function Get-BridgeHealth {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Url
    )

    try {
        return Invoke-RestMethod -Uri $Url -TimeoutSec 2
    }
    catch {
        return $null
    }
}

function Wait-ForBridgeIdle {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Url,

        [Parameter(Mandatory = $true)]
        [int] $TimeoutSeconds
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $health = Get-BridgeHealth -Url $Url
        if ($null -eq $health -or $health.speaking -ne $true) {
            return $true
        }

        Start-Sleep -Milliseconds 500
    }

    Write-Warning "Timed out waiting for Desktop speech to become idle before sending candidate."
    return $false
}

function Wait-ForPlaybackComplete {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Url,

        [Parameter(Mandatory = $true)]
        [int] $TimeoutSeconds
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    $sawSpeaking = $false

    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $health = Get-BridgeHealth -Url $Url
        if ($null -ne $health -and $health.speaking -eq $true) {
            $sawSpeaking = $true
        }
        elseif ($sawSpeaking) {
            return $true
        }

        Start-Sleep -Milliseconds 500
    }

    if ($sawSpeaking) {
        Write-Warning "Timed out waiting for Desktop speech playback to finish."
    }
    else {
        Write-Warning "Timed out waiting for Desktop speech playback to start."
    }

    return $false
}

if ([string]::IsNullOrWhiteSpace($MessageId)) {
    $MessageId = "manual-update-$([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())"
}

if ([string]::IsNullOrWhiteSpace($InboxPath)) {
    $InboxPath = Join-Path $env:APPDATA "CodeCompanionDesktop\candidate-inbox"
}

New-Item -ItemType Directory -Path $InboxPath -Force | Out-Null

if ($WaitForPlayback) {
    Wait-ForBridgeIdle -Url $BridgeHealthUrl -TimeoutSeconds $PlaybackTimeoutSeconds | Out-Null
}

$payload = [ordered]@{
    schemaVersion = 1
    client = [ordered]@{
        clientId = "codex-manual-update"
        name = "Codex Manual Update"
        version = "1.0.0"
        host = "windows"
        environment = "windows"
    }
    workspace = [ordered]@{
        projectId = $ProjectId
        displayName = $DisplayName
        roots = @($Root)
    }
    codex = [ordered]@{
        sessionId = "manual-update"
        messageId = $MessageId
        timestamp = [DateTimeOffset]::UtcNow.ToString("O")
    }
    candidate = [ordered]@{
        kind = "assistant-message"
        phase = "commentary"
        speechHint = $SpeechHint
        text = $Text
        source = "manual-candidate-inbox"
    }
}

$path = Join-Path $InboxPath "$MessageId.json"
$payload | ConvertTo-Json -Depth 8 | Set-Content -Path $path -Encoding UTF8
Write-Host $path

if ($WaitForPlayback) {
    Wait-ForPlaybackComplete -Url $BridgeHealthUrl -TimeoutSeconds $PlaybackTimeoutSeconds | Out-Null
}
