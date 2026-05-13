[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $Text,

    [string] $ProjectId = "codecompaniondesktop",

    [string] $DisplayName = "Code Companion Desktop",

    [string] $Root = "D:\Development\CodeCompanionDesktop",

    [string] $SpeechHint = "manual-speak-last",

    [string] $MessageId,

    [string] $InboxPath
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($MessageId)) {
    $MessageId = "manual-update-$([DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds())"
}

if ([string]::IsNullOrWhiteSpace($InboxPath)) {
    $InboxPath = Join-Path $env:APPDATA "CodeCompanionDesktop\candidate-inbox"
}

New-Item -ItemType Directory -Path $InboxPath -Force | Out-Null

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
