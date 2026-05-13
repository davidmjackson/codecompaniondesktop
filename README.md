# Code Companion Desktop

<!-- cspell:words Inno ISCC LOCALAPPDATA APPDATA -->

Windows desktop companion app for Code Companion Voice.

The first proof of concept is a small WPF tray app that can play a generated
local WAV test tone through normal Windows audio.

## Architecture

The target architecture is documented in `docs/architecture.md`.

Code Companion Desktop is the speech authority. It owns provider credentials,
speech policy, TTS provider calls, queueing, diagnostics, and native Windows
audio playback. Code Companion Voice should become a thin VS Code client that
observes Codex activity and forwards structured speech candidates to the Windows
app, independent of whether the active project is opened from Windows or WSL.

Every development session should identify the current milestone from
`docs/architecture.md` before implementation.

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

The VS Code extension source now lives in the Windows checkout:

```text
D:\Development\CodeCompanionVoice
/mnt/d/Development/CodeCompanionVoice
```

The architecture is moving away from WSL-hosted audio playback. The Windows
.NET app should own native Windows audio, credentials, provider calls, queueing,
and the local bridge surface. The WSL extension codebase still matters because
it owns VS Code commands, workspace context, Codex log watching, and requests to
the desktop bridge.

Keep the two repositories separate for now. They have different runtimes,
toolchains, packaging, and release lifecycle. Shared protocol details should be
documented clearly before extracting any shared package.

## Project Identity

This repository has a stable Code Companion project identity at:

```text
.code-companion\project.json
```

The identity is:

```json
{
  "schemaVersion": 1,
  "projectId": "codecompaniondesktop",
  "displayName": "Code Companion Desktop"
}
```

Code Companion Voice reads this file when the repository is opened in VS Code
and includes the identity in Desktop bridge candidate payloads. This avoids
using Windows and WSL path strings as the long-term project boundary.

## Spoken Work Updates

During development sessions, Codex can send short progress updates and
end-of-milestone summaries through Code Companion Desktop with:

```powershell
.\scripts\send-speech-candidate.ps1 -Text "Milestone summary text."
```

The script writes a valid speech candidate into the Desktop candidate inbox
using the Desktop-owned audio path. It does not rely on VS Code webview audio.

## Requirements

- Windows
- .NET 8 SDK or later

Check the SDK from PowerShell:

```powershell
dotnet --info
```

Building the Windows installer also requires Inno Setup 6. The installer build
script looks for `ISCC.exe` on PATH and in the default Inno Setup install
locations.

## Distribution Model

Code Companion uses two separate installs:

- Code Companion Desktop is installed separately as a Windows app.
- The VS Code extension is installed separately into VS Code.

The VS Code extension owns VS Code commands, workspace context, Codex log
watching, and calls to the local desktop bridge. The Windows app owns
credentials, ElevenLabs calls, queueing, and native Windows audio playback.

They are paired by launching Code Companion Desktop, copying the bridge token,
and saving it in the VS Code extension with
`Code Companion Voice: Set Desktop Bridge Token`.

Target production download locations:

- Code Companion Desktop: GitHub Releases for the Code Companion Desktop
  repository, with a Windows installer named
  `CodeCompanionDesktopSetup-<version>.exe`.
- Code Companion Voice: VS Code Marketplace.

Current development install locations:

- Code Companion Desktop: local installer under `artifacts\installer` after
  running `.\scripts\build-installer.ps1`.
- Code Companion Voice: local VSIX from the Voice repository after running
  `npm run package:vsix`.

Marketplace and release publication are deferred to Milestone 9 in
`docs/architecture.md`. Until that milestone is complete, use the local
development artifacts.

The paired Desktop and Voice release checklist is maintained in
`docs/release-checklist.md`.

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

## Build Installer

Create a self-contained publish output and compile the Windows installer from
PowerShell in the repository root:

```powershell
.\scripts\build-installer.ps1 -AppVersion 0.1.0
```

The default installer output is:

```text
artifacts\installer\CodeCompanionDesktopSetup-0.1.0.exe
```

The installer is a per-user install. It installs to:

```text
%LOCALAPPDATA%\Programs\Code Companion Desktop
```

It creates a Start Menu shortcut, offers an optional desktop shortcut, and shows
a post-install option to launch Code Companion Desktop. The app still stores
credentials in Windows Credential Manager and stores user settings under
`%APPDATA%\CodeCompanionDesktop`.

## Install For Daily Use

Preferred path after building an installer:

1. Run `artifacts\installer\CodeCompanionDesktopSetup-0.1.0.exe`.
2. Launch Code Companion Desktop from the installer, Start Menu, or desktop
   shortcut.
3. Confirm the app opens with the Code Companion icon and the tray icon appears.
4. Save or confirm the ElevenLabs API key in the desktop app.
5. Use `Copy Token` in the Local Bridge section.
6. In VS Code, run `Code Companion Voice: Set Desktop Bridge Token` and paste the
   token.
7. Reload the VS Code window, run `Code Companion Voice: Open Panel`, and confirm
   the panel shows `Enable Voice`, `Mute`, and `Desktop Test`.
8. Click `Desktop Test` and confirm Code Companion Desktop speaks the test
   phrase.
9. In the Startup section, enable `Start hidden to tray` if you want the app to
   stay out of the way after launch.
10. Enable `Start with Windows sign-in` from the installed app.
11. Click `Refresh Diagnostics` and confirm the registered executable path points
   to `%LOCALAPPDATA%\Programs\Code Companion Desktop\CodeCompanionDesktop.exe`.

For developer testing, the published folder can still be used as a portable
daily build:

1. Run `.\scripts\publish-release.ps1` from PowerShell in the repository root.
2. Launch
   `artifacts\publish\CodeCompanionDesktop-win-x64\CodeCompanionDesktop.exe`.
3. Confirm the app opens with the Code Companion icon and the tray icon appears.
4. In the Startup section, enable `Start hidden to tray` if you want the app to
   stay out of the way after launch.
5. Enable `Start with Windows sign-in` from the published app, not from a debug
   build.
6. Click `Refresh Diagnostics` and confirm the registered executable path points
   to `artifacts\publish\CodeCompanionDesktop-win-x64\CodeCompanionDesktop.exe`.

For normal use, start the published executable rather than the debug build under
`src\CodeCompanionDesktop\bin`. If you move or delete the published folder,
launch the app from its new location and toggle `Start with Windows sign-in`
off and on again so Windows starts the correct executable.

## Current Scope

- Styled WPF status window using the custom Code Companion app icon
- Packaged Windows executable icon metadata for release builds
- Self-contained Windows publish script for daily desktop use
- Inno Setup installer script and build wrapper for per-user Windows installs
- Custom Windows tray icon with Show, Play Test Sound, and Exit menu items
- Generated local WAV test tone in the user's temp directory
- Windows audio playback using `System.Media.SoundPlayer`
- ElevenLabs API key storage in Windows Credential Manager
- Desktop-owned ElevenLabs voice, model, and output format settings
- ElevenLabs test speech generation from a hardcoded phrase
- Local authenticated bridge endpoint for VS Code extension requests
- Desktop speech diagnostics for bridge clients, candidates, policy decisions,
  queue state, provider key state, provider errors, playback errors, and recent
  speech results
- Persisted recent bridge client and speech result history under
  `%APPDATA%\CodeCompanionDesktop`

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

The test uses the ElevenLabs create-speech endpoint with the voice, model, and
output format configured in the Windows app. Defaults are voice
`JBFqnCBsd6RMkjVDRZzb`, model `eleven_multilingual_v2`, and MP3 output format
`mp3_44100_128`. Generated MP3 files are written to the user's temp directory
under `CodeCompanionDesktop`.

If no ElevenLabs API key is saved in Windows Credential Manager, the test button
reports that a key must be saved first.

## Speech History

Recent bridge clients and speech results are stored for desktop diagnostics in:

```text
%APPDATA%\CodeCompanionDesktop\speech-history.json
```

The history is diagnostic only. Provider API keys remain in Windows Credential
Manager and are not written to this file. The main Desktop window includes a
Project Speech History panel that groups recent speech candidate decisions by
stable `projectId`.

Observed project identities and path aliases are stored for desktop diagnostics
in:

```text
%APPDATA%\CodeCompanionDesktop\project-registry.json
```

The project registry is keyed by stable `projectId` and records display names,
observed roots, client names, environments, and last-seen times. It lets Desktop
treat Windows and WSL paths as aliases of one project instead of as separate
project identities. The main Desktop window includes a Project Registry panel
with Refresh, Copy, Add Alias, and Remove Alias actions for troubleshooting.

## Local Bridge

The desktop app starts a local HTTP bridge on port `47321` when it launches.

```text
GET  /health
POST /v1/client/hello
POST /v1/speech/candidates
POST /speak
```

`GET /health` returns bridge state:

```json
{
  "status": "ok",
  "bridge": "listening",
  "version": "0.2.0",
  "protocolVersion": 1,
  "appVersion": "0.1.0",
  "speaking": false,
  "queueEnabled": false,
  "queued": 0,
  "queueLimit": 3
}
```

`POST /v1/client/hello` accepts VS Code client and workspace metadata. It is the
first versioned bridge contract for the environment-agnostic speech
architecture:

```json
{
  "schemaVersion": 1,
  "client": {
    "clientId": "generated-non-secret-id",
    "name": "Code Companion Voice",
    "version": "0.1.0",
    "host": "windows",
    "environment": "windows"
  },
  "workspace": {
    "projectId": "codecompaniondesktop",
    "displayName": "Code Companion Desktop",
    "roots": ["D:\\Development\\CodeCompanionDesktop"]
  }
}
```

The current response uses compatibility authorization while desktop-managed
pairing is still pending:

```json
{
  "status": "ok",
  "authorization": "allowed",
  "mode": "compatibility-token",
  "bridgeVersion": "0.2.0",
  "protocolVersion": 1
}
```

`POST /v1/speech/candidates` accepts structured Codex speech candidate events.
The desktop app validates the request, applies deterministic speech policy,
redacts common secret patterns, deduplicates by message ID and normalized text,
then sends accepted text through the desktop queue and ElevenLabs playback path.
This endpoint currently requires the same `Authorization: Bearer <token>` header
as `/speak` until desktop-managed pairing replaces copied tokens.

```json
{
  "schemaVersion": 1,
  "client": {
    "clientId": "generated-non-secret-id",
    "name": "Code Companion Voice",
    "version": "0.1.0",
    "host": "wsl",
    "environment": "wsl:Ubuntu-24.04"
  },
  "workspace": {
    "projectId": "codecompaniondesktop",
    "displayName": "Code Companion Desktop",
    "roots": ["/mnt/d/Development/CodeCompanionDesktop"]
  },
  "codex": {
    "sessionId": "019e1ff1-6137-72d2-abc1-8095584e6adf",
    "messageId": "732d2e9ba4e5ce04",
    "timestamp": "2026-05-13T07:06:13.303Z"
  },
  "candidate": {
    "kind": "assistant-message",
    "phase": "final",
    "text": "Implemented the bridge health check and verified tests.",
    "source": "codex-jsonl"
  }
}
```

Example spoken response:

```json
{
  "status": "accepted",
  "decision": "spoken",
  "reason": "accepted",
  "queuePosition": 0
}
```

When bridge queueing is enabled, a valid candidate can return
`"decision": "queued"` with a positive `queuePosition`. Duplicate candidates
return `"decision": "duplicate"` and are not spoken. Non-final or unsupported
candidate kinds return `"decision": "ignored"`.

## Candidate Inbox

Code Companion Desktop also watches a Windows-owned candidate inbox:

```text
%APPDATA%\CodeCompanionDesktop\candidate-inbox
```

Each `*.json` file in this directory uses the same JSON shape as
`POST /v1/speech/candidates`. The desktop app validates the file, applies the
same speech pipeline as the HTTP bridge, then deletes accepted files. Invalid or
rejected files are moved under `candidate-inbox\rejected`.

This inbox is the Milestone 4A migration path away from `\\wsl.localhost` Codex
log scraping. Windows, WSL, and future clients can write explicit structured
candidate events into a Windows-owned location while Code Companion Desktop
remains the only component that owns policy, provider calls, diagnostics, and
native audio playback.

`POST /speak` requires an `Authorization: Bearer <token>` header and a JSON body:

```json
{ "text": "Speech text to generate and play." }
```

If speech is already playing, `/speak` returns `409 Conflict` with
`{"error":"busy"}` by default.

The Local Bridge section includes `Queue bridge speech requests`. When enabled,
new `/speak` requests and valid `/v1/speech/candidates` requests are accepted
into a bounded queue instead of being rejected while another bridge speech
request is playing. The queue limit is stored with the app settings and can be
set to 1, 3, 5, or 10 pending requests. When the queue is full, `/speak` returns
`409 Conflict` with `{"error":"queue_full"}` and speech candidates return a
structured rejected decision with reason `queue_full`.

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

Bridge speech queue preferences are stored in the same settings file.

## Session Notes

See `docs/session-log.md` for the latest development status, verification, and
next steps.

## Next Milestones

1. Add VS Code extension first-run checks for the desktop bridge and installer
   link.
2. Add an update/signing flow for non-developer daily use.
