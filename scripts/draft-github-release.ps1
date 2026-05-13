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
    Write-Host "Dry run. Add -Create to create the draft GitHub Release."
    Write-Host "Repository:    $Repository"
    Write-Host "Tag:           $tagName"
    Write-Host "Installer:     $installerPath"
    Write-Host "Checksum:      $checksumPath"
    Write-Host "Release notes: $releaseNotesPath"
    Write-Host ""
    Write-Host "Command:"
    Write-Host "gh $($releaseArgs -join ' ')"
    return
}

$ghCommand = Get-Command gh -ErrorAction SilentlyContinue
if (-not $ghCommand) {
    throw "Unable to find gh. Install GitHub CLI or add gh to PATH."
}

$process = Start-Process -FilePath $ghCommand.Source -ArgumentList $releaseArgs -NoNewWindow -Wait -PassThru
if ($process.ExitCode -ne 0) {
    throw "gh release create failed with exit code $($process.ExitCode)."
}

Write-Host "Draft GitHub Release created for $tagName in $Repository."
