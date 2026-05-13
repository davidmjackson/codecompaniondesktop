[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",

    [string] $Runtime = "win-x64",

    [string] $AppVersion = "0.1.0",

    [string] $PublishPath,

    [string] $InstallerOutputPath,

    [switch] $SkipPublish
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$publishScript = Join-Path $PSScriptRoot "publish-release.ps1"
$installerScript = Join-Path $repoRoot "installer\CodeCompanionDesktop.iss"

if ([string]::IsNullOrWhiteSpace($PublishPath)) {
    $PublishPath = Join-Path $repoRoot "artifacts\publish\CodeCompanionDesktop-$Runtime"
}

if ([string]::IsNullOrWhiteSpace($InstallerOutputPath)) {
    $InstallerOutputPath = Join-Path $repoRoot "artifacts\installer"
}

if (-not $SkipPublish.IsPresent) {
    & $publishScript `
        -Configuration $Configuration `
        -Runtime $Runtime `
        -AppVersion $AppVersion `
        -OutputPath $PublishPath
}

$publishedExe = Join-Path $PublishPath "CodeCompanionDesktop.exe"
for ($attempt = 0; $attempt -lt 20 -and -not (Test-Path $publishedExe); $attempt++) {
    Start-Sleep -Milliseconds 250
}

if (-not (Test-Path $publishedExe)) {
    throw "Published executable was not found at $publishedExe. Run scripts\publish-release.ps1 first or omit -SkipPublish."
}

for ($attempt = 0; $attempt -lt 40; $attempt++) {
    try {
        $stream = [System.IO.File]::Open($publishedExe, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
        $stream.Dispose()
        break
    }
    catch {
        if ($attempt -eq 39) {
            throw "Published executable is still locked at $publishedExe."
        }

        Start-Sleep -Milliseconds 250
    }
}

$isccCommand = Get-Command ISCC.exe -ErrorAction SilentlyContinue
$candidateIsccPaths = @(
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe")
) | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }

$isccPath = if ($isccCommand) {
    $isccCommand.Source
}
else {
    $candidateIsccPaths | Where-Object { Test-Path $_ } | Select-Object -First 1
}

if ([string]::IsNullOrWhiteSpace($isccPath) -or -not (Test-Path $isccPath)) {
    throw "Unable to find ISCC.exe. Install Inno Setup 6 or add ISCC.exe to PATH, then rerun this script."
}

New-Item -ItemType Directory -Path $InstallerOutputPath -Force | Out-Null

$installerPath = Join-Path $InstallerOutputPath "CodeCompanionDesktopSetup-$AppVersion.exe"
if (Test-Path $installerPath) {
    Remove-Item -Path $installerPath -Force
}

$isccArgs = @(
    "/DAppVersion=$AppVersion",
    "/DSourceDir=$PublishPath",
    "/DOutputDir=$InstallerOutputPath",
    $installerScript
)

$isccProcess = Start-Process -FilePath $isccPath -ArgumentList $isccArgs -WorkingDirectory $repoRoot -Wait -PassThru
if ($isccProcess.ExitCode -ne 0) {
    throw "Inno Setup compiler failed with exit code $($isccProcess.ExitCode)."
}

if (-not (Test-Path $installerPath)) {
    throw "Installer build completed but expected output was not found at $installerPath."
}

Write-Host ""
Write-Host "Built Code Companion Desktop installer:"
Write-Host $installerPath
