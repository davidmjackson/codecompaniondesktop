# Code Companion Desktop

Windows desktop companion app for Code Companion Voice.

The first proof of concept is a small WPF tray app that can play a generated
local WAV test tone through normal Windows audio.

## Repository Layout

Start desktop-app work in this Windows checkout:

```text
D:\Development\CodeCompanionDesktop
```

Open this folder in a normal local Windows VS Code window, not a VS Code WSL
Remote window. WPF targets `net8.0-windows` and depends on the WindowsDesktop
SDK, so C# Dev Kit needs to run on the Windows side. If the same files are
opened through WSL, `MainWindow.xaml.cs` may show false red underscores for
XAML-generated members such as `InitializeComponent`, `PlayButton`,
`StatusText`, and `AudioPathText`.

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
toolchains, packaging, and release lifecycle. Shared protocol details should be
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
- ElevenLabs API key storage in Windows Credential Manager
- ElevenLabs test speech generation from a hardcoded phrase

## Credential Storage

The ElevenLabs API key is stored as a generic Windows Credential Manager secret
for the current Windows user.

```text
Target: CodeCompanionDesktop/ElevenLabsApiKey
User name: ElevenLabs
```

The app UI can save, load, and clear this credential. Status messages report
only whether a key exists and its character count; they do not display the key.

## ElevenLabs Test Speech

The app can generate and play a hardcoded ElevenLabs test phrase:

```text
Code Companion desktop speech test.
```

The test uses the ElevenLabs create-speech endpoint with voice
`JBFqnCBsd6RMkjVDRZzb`, model `eleven_multilingual_v2`, and MP3 output format
`mp3_44100_128`. Generated MP3 files are written to the user's temp directory
under `CodeCompanionDesktop`.

If no ElevenLabs API key is saved in Windows Credential Manager, the test button
reports that a key must be saved first.

## Session Notes

See `docs/session-log.md` for the latest development status, verification, and
next steps.

## Next Milestones

1. Manually verify ElevenLabs test playback with a real saved desktop API key.
2. Add a local authenticated bridge endpoint for VS Code extension requests.
