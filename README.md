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

## Publish For Daily Use

Create a self-contained Windows build from PowerShell in the repository root:

```powershell
.\scripts\publish-release.ps1
```

The default output is:

```text
artifacts\publish\CodeCompanionDesktop-win-x64\CodeCompanionDesktop.exe
```

Run that executable for normal desktop use instead of the `bin\Debug` build.
The `artifacts` folder is ignored by git.

If `Start with Windows sign-in` is already enabled from a debug build, launch
the published app, turn `Start with Windows sign-in` off, then turn it back on.
The Startup diagnostics should then show the published executable path.

## Current Scope

- Styled WPF status window using the custom Code Companion app icon
- Packaged Windows executable icon metadata for release builds
- Self-contained Windows publish script for daily desktop use
- Custom Windows tray icon with Show, Play Test Sound, and Exit menu items
- Generated local WAV test tone in the user's temp directory
- Windows audio playback using `System.Media.SoundPlayer`
- ElevenLabs API key storage in Windows Credential Manager
- ElevenLabs test speech generation from a hardcoded phrase
- Local authenticated bridge endpoint for VS Code extension requests

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

## Local Bridge

The desktop app starts a local HTTP bridge on port `47321` when it launches.

```text
GET  /health
POST /speak
```

`GET /health` returns bridge state:

```json
{ "status": "ok", "bridge": "listening", "speaking": false }
```

`POST /speak` requires an `Authorization: Bearer <token>` header and a JSON body:

```json
{ "text": "Speech text to generate and play." }
```

If speech is already playing, `/speak` returns `409 Conflict` with
`{"error":"busy"}`.

The bridge token is generated once and stored in Windows Credential Manager:

```text
Target: CodeCompanionDesktop/BridgeToken
User name: CodeCompanionDesktop Bridge
```

Use the app's `Copy Token` button to paste the token into the VS Code extension
with `Code Companion Voice: Set Desktop Bridge Token`.

The tray menu also includes `Bridge Status` and `Copy Bridge Token` actions.

The bridge listens on Windows port `47321`. From WSL, the extension discovers the
Windows host IP and calls that address rather than `127.0.0.1`.

## Startup Behavior

Closing the window with `X` or clicking `Hide to Tray` keeps the app and bridge
running in the tray. Use `Exit` from the window or tray menu to stop the app.

The Startup section includes `Start hidden to tray`. When enabled, future
launches start the tray icon and bridge without showing the main window.

The same section includes `Start with Windows sign-in`. When enabled, the app
registers itself for the current Windows user under:

```text
HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run
```

Use `Start with Windows sign-in` together with `Start hidden to tray` for a
tray-only startup after signing in to Windows.

The Startup section also includes diagnostics for the registered Windows Run
command. Use `Refresh Diagnostics` to re-read the registry value and confirm the
registered executable still exists and matches the running app. Use
`Copy Diagnostics` when pasting startup details into an issue or handover note.

The hidden-to-tray preference is stored for the current Windows user at:

```text
%APPDATA%\CodeCompanionDesktop\settings.json
```

## Session Notes

See `docs/session-log.md` for the latest development status, verification, and
next steps.

## Next Milestones

1. Add installer/autostart guidance for the packaged app path.
2. Add a queue/settings surface for bridge speech behavior.
