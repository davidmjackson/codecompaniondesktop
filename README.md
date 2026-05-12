# Code Companion Desktop

Windows desktop companion app for Code Companion Voice.

The first proof of concept is a small WPF tray app that can play a generated
local WAV test tone through normal Windows audio.

## Repository Layout

Start desktop-app work in this Windows checkout:

```text
D:\Development\CodeCompanionDesktop
```

The existing VS Code extension remains in WSL:

```text
/var/www/CodeCompanion
```

The architecture is moving away from WSL-hosted audio playback. The Windows
.NET app should own native Windows audio, credentials, provider calls, queueing,
and the local bridge surface. The WSL extension codebase still matters because
it owns VS Code commands, workspace context, Codex log watching, and requests to
the desktop bridge.

Keep the two repositories separate for now. They have different runtimes,
toolchains, packaging, and release lifecycles. Shared protocol details should be
documented clearly before extracting any shared package.

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

Running plain `dotnet run` from the repository root will not find the nested
project. Use the `--project` command above from the root folder.

## Build

```powershell
dotnet build .\src\CodeCompanionDesktop\CodeCompanionDesktop.csproj
```

## Current Scope

- WPF status window
- Windows tray icon with Show, Play Test Sound, and Exit menu items
- Generated local WAV test tone in the user's temp directory
- Windows audio playback using `System.Media.SoundPlayer`

## Session Notes

See `docs/session-log.md` for the latest development status, verification, and
next steps.

## Next Milestones

1. Store and retrieve an ElevenLabs API key using Windows Credential Manager.
2. Generate TTS from a hardcoded test phrase and play the audio.
3. Add a local authenticated bridge endpoint for VS Code extension requests.
