[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $AppVersion,

    [string] $Repository = "davidmjackson/codecompaniondesktop",

    [string] $InstallerOutputPath,

    [string] $ChecksumOutputPath,

    [string] $ReleaseNotesOutputPath,

    [switch] $Create
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

function Resolve-GitHubCliPath {
    $ghCommand = Get-Command gh -ErrorAction SilentlyContinue
    if ($ghCommand) {
        return $ghCommand.Source
    }

    $candidatePaths = @(
        (Join-Path $env:ProgramFiles "GitHub CLI\gh.exe"),
        (Join-Path ${env:ProgramFiles(x86)} "GitHub CLI\gh.exe"),
        (Join-Path $env:LOCALAPPDATA "Programs\GitHub CLI\gh.exe")
    ) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

    return $candidatePaths | Where-Object { Test-Path $_ } | Select-Object -First 1
}

function ConvertTo-ProcessArgument {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Argument
    )

    if ($Argument -notmatch '[\s"]') {
        return $Argument
    }

    return '"' + $Argument.Replace('"', '\"') + '"'
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

$installerFileName = "CodeCompanionDesktopSetup-$AppVersion.exe"
$installerPath = Join-Path $InstallerOutputPath $installerFileName
$checksumPath = Join-Path $ChecksumOutputPath "$installerFileName.sha256"
$releaseNotesPath = Join-Path $ReleaseNotesOutputPath "desktop-$AppVersion.md"
$tagName = "v$AppVersion"

foreach ($requiredPath in @($installerPath, $checksumPath, $releaseNotesPath)) {
    if (-not (Test-Path $requiredPath)) {
        throw "Required release artifact was not found: $requiredPath. Run scripts\build-release-package.ps1 first."
    }
}

$expectedHash = (Get-FileHash -Path $installerPath -Algorithm SHA256).Hash.ToLowerInvariant()
$checksumText = (Get-Content -Path $checksumPath -Raw).Trim()
if ($checksumText -notmatch "^\s*$expectedHash\s+$([regex]::Escape($installerFileName))\s*$") {
    throw "Checksum file does not match installer hash. Expected '$expectedHash  $installerFileName'."
}

$releaseArgs = @(
    "release",
    "create",
    $tagName,
    $installerPath,
    $checksumPath,
    "--repo",
    $Repository,
    "--draft",
    "--title",
    "Code Companion Desktop $AppVersion",
    "--notes-file",
    $releaseNotesPath
)

if (-not $Create.IsPresent) {
    $releaseCommandLine = ($releaseArgs | ForEach-Object { ConvertTo-ProcessArgument $_ }) -join ' '
    Write-Host "Dry run. Add -Create to create the draft GitHub Release."
    Write-Host "Repository:    $Repository"
    Write-Host "Tag:           $tagName"
    Write-Host "Installer:     $installerPath"
    Write-Host "Checksum:      $checksumPath"
    Write-Host "Release notes: $releaseNotesPath"
    Write-Host ""
    Write-Host "Command:"
    Write-Host "gh $releaseCommandLine"
    return
}

$ghPath = Resolve-GitHubCliPath
if ([string]::IsNullOrWhiteSpace($ghPath) -or -not (Test-Path $ghPath)) {
    throw "Unable to find gh. Install GitHub CLI or add gh to PATH."
}

$releaseCommandLine = ($releaseArgs | ForEach-Object { ConvertTo-ProcessArgument $_ }) -join ' '
$process = Start-Process -FilePath $ghPath -ArgumentList $releaseCommandLine -NoNewWindow -Wait -PassThru
if ($process.ExitCode -ne 0) {
    throw "gh release create failed with exit code $($process.ExitCode)."
}

Write-Host "Draft GitHub Release created for $tagName in $Repository."
