[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string] $Configuration = "Release",

    [string] $Runtime = "win-x64",

    [string] $AppVersion = "0.1.0",

    [switch] $FrameworkDependent,

    [switch] $NoClean,

    [string] $OutputPath
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "src\CodeCompanionDesktop\CodeCompanionDesktop.csproj"
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

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $repoRoot "artifacts\publish\CodeCompanionDesktop-$Runtime"
}

$isSelfContained = -not $FrameworkDependent.IsPresent

if ($AppVersion -notmatch '^\d+\.\d+\.\d+(\.\d+)?$') {
    throw "AppVersion must be a numeric version like 0.1.0 or 0.1.0.0."
}

$assemblyVersion = if (($AppVersion -split '\.').Count -eq 3) {
    "$AppVersion.0"
}
else {
    $AppVersion
}

if ((Test-Path $OutputPath) -and -not $NoClean.IsPresent) {
    Remove-Item -Path $OutputPath -Recurse -Force
}

$publishArgs = @(
    "publish",
    $projectPath,
    "--configuration", $Configuration,
    "--runtime", $Runtime,
    "--self-contained", $isSelfContained.ToString().ToLowerInvariant(),
    "--output", $OutputPath,
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:EnableCompressionInSingleFile=true",
    "-p:Version=$AppVersion",
    "-p:AssemblyVersion=$assemblyVersion",
    "-p:FileVersion=$assemblyVersion",
    "-p:InformationalVersion=$AppVersion"
)

$publishProcess = Start-Process -FilePath $dotnetPath -ArgumentList $publishArgs -NoNewWindow -Wait -PassThru
if ($publishProcess.ExitCode -ne 0) {
    throw "dotnet publish failed with exit code $($publishProcess.ExitCode)."
}

Write-Host ""
Write-Host "Published Code Companion Desktop to:"
Write-Host $OutputPath
Write-Host ""
Write-Host "Run:"
Write-Host (Join-Path $OutputPath "CodeCompanionDesktop.exe")
