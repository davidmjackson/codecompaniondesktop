# Code Companion Desktop Handover

Date: 2026-05-12

## Goal

Build a Windows desktop companion app for Code Companion Voice so speech playback no longer depends on VS Code webview audio.

The current VS Code extension works, but VS Code/Chromium webview autoplay rules are fragile across project windows, reloads, hidden panels, and closed panels. A Windows desktop app can use normal Windows audio and run independently of the VS Code panel lifecycle.

## Current Extension State

Repository:

```text
/var/www/CodeCompanion
```

Current branch:

```text
fix/hold-blocked-playback
```

Latest relevant commits:

```text
d0e08bc Prefer desktop audio after unlock
31106a4 Add voice check-in playback fallback
df471b6 Fix cross-project voice autoplay unlock
```

Installed extension version during testing:

```text
local.code-companion-voice@0.0.29
```

What works now:

- The extension opens the voice panel at startup when voice is enabled and a provider key exists.
- The user clicks `Unlock Audio for This Session` once per VS Code window.
- On Windows/WSL, after unlock, desktop audio is preferred for playback.
- The VS Code webview audio path remains as a fallback.
- Voice check-in phrases are detected from Codex chat logs.
- Example check-in phrases:
  - `Morning`
  - `Afternoon`
  - `Evening`
  - `Hello`
  - `Hi`
  - `Can you update me?`
  - `What's the status?`
  - `Voice check please`
- Check-ins mark the next assistant response as speech-eligible.
- Privacy filtering and speech rewriting still run before TTS.

Manual verification already completed:

- Retrospective project reloaded.
- User clicked unlock.
- Welcome audio played.
- Synthetic check-in was appended for `/var/www/retrospective`.
- Extension log showed:

```text
[desktop-audio] available: true
[spoken] elevenlabs/desktop
[policy] voice-check-in
[spoken] elevenlabs/desktop
```

- User confirmed both the unlock welcome and check-in response were audible.

Important note:

```text
Selected model is at capacity. Please try a different model.
```

This message appeared during testing, but the voice extension output still showed successful desktop playback. Treat that as a separate chat/model availability issue, not a Code Companion Voice playback failure.

## Current Desktop Fallback Behavior

The VS Code extension currently implements a Windows/WSL desktop playback fallback.

It activates only when:

- The extension runs in WSL/Linux.
- `WSL_DISTRO_NAME` is present.
- `powershell.exe` is available from WSL.
- `wslpath` is available.
- The user has clicked unlock in the current VS Code window.

The extension writes generated TTS audio to a WSL temp file, converts the path to a Windows path using `wslpath`, and plays it with Windows PowerShell using `System.Windows.Media.MediaPlayer`.

This proves the Windows desktop audio route is viable, but it is still embedded in the VS Code extension. The proposed desktop app moves this responsibility into a proper Windows process.

## Proposed New Architecture

Use two projects:

```text
/var/www/CodeCompanion
D:\Development\CodeCompanionDesktop
```

Current direction:

```text
D:\Development\CodeCompanionDesktop
- Primary repo for desktop-app sessions
- .NET/WPF Windows app
- Native Windows audio, credentials, provider calls, queueing, and bridge API

/var/www/CodeCompanion
- Existing VS Code extension repo in WSL
- VS Code commands, settings, workspace context, Codex log watching, and bridge client
```

The previous WSL desktop-audio fallback is useful historical proof that Windows
audio playback works from the extension, but it is not the target architecture.
New playback work should move into the .NET desktop app and expose a small
authenticated local bridge for the extension to call.

Responsibilities:

```text
CodeCompanion VS Code extension
- VS Code commands and settings UI
- Current workspace/project context
- Codex session/log context
- Bridge to the desktop app
- Optional fallback webview panel

CodeCompanionDesktop Windows app
- TTS provider calls
- Audio playback
- Speech queue
- Policy decisions, or at least final playback decisions
- Secure provider key storage
- Tray/status UI
- Local logs
- Startup behavior
```

Recommended communication model:

```text
VS Code extension <-> local Windows app
```

Preferred transport options:

1. Named pipes with a pairing token.
2. Localhost HTTP/WebSocket with a pairing token.

Named pipes are a good fit for a local Windows app. Localhost is easier to debug and can work well if locked down to loopback and protected by a token.

## Secrets

Move provider API keys out of VS Code SecretStorage for the Windows app version.

Recommended storage:

```text
Windows Credential Manager
```

Reason:

- The Windows app will call ElevenLabs/OpenAI directly.
- The app should own the provider key lifecycle.
- User-level Windows credential storage avoids repo files and avoids tying secrets to one VS Code remote environment.

The VS Code extension can still expose commands such as:

- Open desktop settings
- Set provider key
- Select provider
- Select voice/model

But those commands should pass the user to the Windows app or call a local authenticated app endpoint that stores secrets in Windows Credential Manager.

## Codex Log Strategy

There are two viable designs.

Recommended first implementation:

```text
VS Code extension watches Codex logs in WSL and sends eligible speech events to the Windows app.
```

Why:

- The extension already knows the active workspace.
- The existing watcher already handles Codex JSONL parsing.
- WSL file watching from Windows through `\\wsl$` can be less reliable.
- This avoids making the Windows app understand every WSL distro/path edge case immediately.

Possible later implementation:

```text
Windows app reads WSL logs directly through \\wsl$.
```

That could make the app more standalone, but it should come after the bridge design is stable.

## Suggested Development Location

Use the Windows filesystem for the desktop app:

```text
D:\Development\CodeCompanionDesktop
```

Do not build the Windows desktop app inside WSL paths such as:

```text
/var/www/CodeCompanionDesktop
```

Reason:

- It is a real Windows desktop/tray app.
- It will use Windows audio APIs.
- It may use Windows Credential Manager.
- Building on the Windows filesystem avoids WSL path and tooling friction.

Keep the VS Code extension in `/var/www/CodeCompanion` until the bridge is
stable. Co-locating both codebases in one repository would add friction now:
the desktop app and extension use different build tools, package formats, and
runtime assumptions. Revisit a monorepo only after the bridge protocol and
release process are stable.

## Permissions

The app should not require administrator rights for normal use.

Admin rights may be needed only for installing prerequisites, such as:

- .NET SDK
- Visual Studio Build Tools, if required
- VS Code WSL extension
- WSL itself

Normal app behavior should run as the current Windows user:

- Play audio
- Store user-level credentials
- Start tray process
- Listen on loopback or named pipe
- Communicate with VS Code extension

## Recommended Technology

Start with:

```text
.NET 8 or later
Windows tray app
```

Likely UI options:

- WinUI 3
- WPF

Pragmatic recommendation for first proof of concept:

```text
WPF tray app
```

Reason:

- Mature and well-documented.
- Straightforward tray integration.
- Easy enough settings window.
- Good fit for a utility app.

## First Proof Of Concept

Keep the first version deliberately small.

Milestone 1:

- Create a .NET Windows tray app.
- Show tray icon.
- Add a simple status/settings window.
- Play a local WAV/MP3 test file through Windows audio.

Milestone 2:

- Store and retrieve an ElevenLabs API key using Windows Credential Manager.
- Generate TTS from a hardcoded test phrase.
- Play the generated audio through Windows audio.

Milestone 3:

- Add a local bridge endpoint.
- Send a test speech request from the VS Code extension to the desktop app.
- Desktop app queues and plays it.

Milestone 4:

- Move normal milestone/check-in playback from extension-owned TTS to desktop-app-owned TTS.
- Keep the extension as the Codex watcher and context bridge.

Milestone 5:

- Add OpenAI TTS as fallback/comparison provider.
- Add installer/startup behavior.

## Extension Changes Likely Needed Later

The current extension can remain useful, but its role should shrink.

Likely changes:

- Add setting for desktop app bridge URL or pipe name.
- Add pairing token storage.
- Add command: `Code Companion Voice: Connect Desktop App`.
- Add command: `Code Companion Voice: Open Desktop App`.
- Send speech events to desktop app instead of streaming TTS itself.
- Keep current webview/desktop fallback for compatibility during migration.

## Testing Expectations

For the VS Code extension project:

```bash
npm run compile
npm test
find dist -name '*.js' -print0 | xargs -0 -n1 node --check
git diff --check
npm audit --omit=dev
npm run package:vsix
```

Manual extension test:

- Install VSIX.
- Reload Retrospective window.
- Unlock audio.
- Trigger `Can you update me?`.
- Confirm desktop audio response.

For the Windows app:

- Build from Windows filesystem.
- Run without admin rights.
- Confirm tray icon appears.
- Confirm test audio plays.
- Confirm provider key can be stored and retrieved.
- Confirm app receives a local bridge request.
- Confirm app logs playback success/failure.

## Open Decisions

Decide before implementation:

- WPF vs WinUI 3.
- Named pipes vs localhost bridge.
- Whether the desktop app owns policy immediately or only playback first.
- Whether the VS Code extension keeps watching Codex logs in the first desktop-app version.
- Installer format: MSIX, simple installer, or zip/self-contained publish for early builds.

Recommended defaults:

- WPF tray app.
- Localhost bridge for easier first debugging, then consider named pipes.
- Extension watches Codex logs initially.
- Desktop app owns TTS generation, queue, audio playback, and key storage.
- Start without a formal installer; use `dotnet publish` for early manual testing.

## Next Step

Scaffold the new Windows app in:

```text
D:\Development\CodeCompanionDesktop
```

Start with a minimal WPF tray app proof of concept that can play a test phrase or local audio file through Windows audio.
