[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $AppVersion,

    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",

    [string] $Runtime = "win-x64",

    [string] $InstallerOutputPath,

    [string] $ChecksumOutputPath,

    [string] $ReleaseNotesOutputPath,

    [string] $FreshInstallSummary = "Pending fresh-install verification from the GitHub Release asset.",

    [string] $KnownLimitations = "Pending final release review.",

    [switch] $SkipChecks
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$solutionPath = Join-Path $repoRoot "CodeCompanionDesktop.sln"
$buildInstallerScript = Join-Path $PSScriptRoot "build-installer.ps1"

function Invoke-CheckedNativeCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string] $FilePath,

        [Parameter(ValueFromRemainingArguments = $true)]
        [string[]] $ArgumentList
    )

    Write-Host "Running: $FilePath $($ArgumentList -join ' ')"
    $process = Start-Process -FilePath $FilePath -ArgumentList $ArgumentList -NoNewWindow -Wait -PassThru
    if ($process.ExitCode -ne 0) {
        throw "Command failed with exit code $($process.ExitCode): $FilePath $($ArgumentList -join ' ')"
    }
}

if ([string]::IsNullOrWhiteSpace($InstallerOutputPath)) {
    $InstallerOutputPath = Join-Path $repoRoot "artifacts\installer"
}

if ([string]::IsNullOrWhiteSpace($ChecksumOutputPath)) {
    $ChecksumOutputPath = Join-Path $repoRoot "artifacts\checksums"
}

if ([string]::IsNullOrWhiteSpace($ReleaseNotesOutputPath)) {
    $ReleaseNotesOutputPath = Join-Path $repoRoot "artifacts\release-notes"
}

if ($AppVersion -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') {
    throw "AppVersion must be a numeric version like 0.1.0 or 0.1.0.0."
}

if (-not $SkipChecks.IsPresent) {
    $dotnetCommand = Get-Command dotnet -ErrorAction SilentlyContinue
    $dotnetPath = if ($dotnetCommand) {
        $dotnetCommand.Source
    }
    else {
        Join-Path $env:ProgramFiles "dotnet\dotnet.exe"
    }

    if (-not (Test-Path $dotnetPath)) {
        throw "Unable to find dotnet. Install the .NET SDK or add dotnet.exe to PATH."
    }

    Invoke-CheckedNativeCommand $dotnetPath build $solutionPath --configuration $Configuration
    Invoke-CheckedNativeCommand $dotnetPath test $solutionPath --configuration $Configuration --no-build

    $gitCommand = Get-Command git -ErrorAction SilentlyContinue
    if ($gitCommand) {
        Invoke-CheckedNativeCommand $gitCommand.Source diff --check
    }
}

& $buildInstallerScript `
    -Configuration $Configuration `
    -Runtime $Runtime `
    -AppVersion $AppVersion `
    -InstallerOutputPath $InstallerOutputPath

$installerFileName = "CodeCompanionDesktopSetup-$AppVersion.exe"
$installerPath = Join-Path $InstallerOutputPath $installerFileName
if (-not (Test-Path $installerPath)) {
    throw "Installer was not found at $installerPath."
}

New-Item -ItemType Directory -Path $ChecksumOutputPath -Force | Out-Null
New-Item -ItemType Directory -Path $ReleaseNotesOutputPath -Force | Out-Null

$hash = Get-FileHash -Path $installerPath -Algorithm SHA256
$checksumPath = Join-Path $ChecksumOutputPath "$installerFileName.sha256"
$checksumLine = "$($hash.Hash.ToLowerInvariant())  $installerFileName"
Set-Content -Path $checksumPath -Value $checksumLine -Encoding UTF8

$releaseNotesPath = Join-Path $ReleaseNotesOutputPath "desktop-$AppVersion.md"
$releaseNotes = @"
# Code Companion Desktop $AppVersion

## Installer

- File: $installerFileName
- SHA256: $($hash.Hash.ToLowerInvariant())

## Requirements

- Windows 10 or later.
- No separate .NET runtime install is expected for the standard self-contained installer.

## Verification

$FreshInstallSummary

## Known Limitations

$KnownLimitations
"@

Set-Content -Path $releaseNotesPath -Value $releaseNotes -Encoding UTF8

Write-Host ""
Write-Host "Built Code Companion Desktop release package:"
Write-Host "Installer:     $installerPath"
Write-Host "Checksum:      $checksumPath"
Write-Host "Release notes: $releaseNotesPath"
