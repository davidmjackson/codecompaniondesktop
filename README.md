# Code Companion Desktop

Windows desktop companion app for Code Companion Voice.

The first proof of concept is a small WPF tray app that can play a generated
local WAV test tone through normal Windows audio.

## Requirements

- Windows
- .NET 8 SDK or later

Check the SDK from PowerShell:

```powershell
dotnet --info
```

## Run

From PowerShell in this folder:

```powershell
dotnet run --project .\src\CodeCompanionDesktop\CodeCompanionDesktop.csproj
```

## Build

```powershell
dotnet build .\src\CodeCompanionDesktop\CodeCompanionDesktop.csproj
```

## Current Scope

- WPF status window
- Windows tray icon with Show, Play Test Sound, and Exit menu items
- Generated local WAV test tone in the user's temp directory
- Windows audio playback using `System.Media.SoundPlayer`

## Next Milestones

1. Store and retrieve an ElevenLabs API key using Windows Credential Manager.
2. Generate TTS from a hardcoded test phrase and play the audio.
3. Add a local authenticated bridge endpoint for VS Code extension requests.

