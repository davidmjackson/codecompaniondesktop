# Session Log

Use this log to preserve project context between work sessions. Keep entries
concise: what changed, what was verified, decisions made, and the next useful
options.

## 2026-05-12 Local Desktop Bridge Endpoint

### Windows Login Startup Follow-up

- Created branch `feature/windows-login-startup` from
  `feature/start-hidden-to-tray`.
- Added `WindowsStartupRegistration`, using the current-user Windows Run key:
  `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`.
- Added `Start with Windows sign-in` to the Startup section.
- Kept Windows login startup independent from `Start hidden to tray`; users can
  enable both for tray-only login startup.
- Documented the registry location and recommended pairing in the README.

### Windows Login Startup Verification

- Initial build encountered a locked `CodeCompanionDesktop.exe` from manual
  testing; stopped the process and reran verification.
- `dotnet build CodeCompanionDesktop.sln`
- Clean rebuild completed with 0 warnings and 0 errors.
- XML parse check passed for `MainWindow.xaml` and
  `CodeCompanionDesktop.csproj`.
- `git diff --check`
- Hidden-to-tray launch smoke test with temporary app settings: process started
  and bridge health returned
  `{"status":"ok","bridge":"listening","speaking":false}`.

### Startup Behavior Follow-up

- Created branch `feature/start-hidden-to-tray` from `feature/local-bridge-endpoint`.
- Added `AppSettingsStore` and `AppSettings`.
- Persisted startup preferences to
  `%APPDATA%\CodeCompanionDesktop\settings.json`.
- Added a Startup section with `Start hidden to tray`.
- When enabled, the app launches the tray icon and bridge without showing the
  main window.
- Added a startup tray balloon when the app launches hidden.
- Added `Hide to Tray` to the tray menu.
- Documented hide/exit behavior and startup preference storage in the README.

### Startup Behavior Verification

- `dotnet build CodeCompanionDesktop.sln`
- XML parse check passed for `MainWindow.xaml` and `CodeCompanionDesktop.csproj`.
- `git diff --check`
- Launch smoke test with temporary app settings and default visible startup:
  process started and bridge health returned
  `{"status":"ok","bridge":"listening","speaking":false}`.
- Launch smoke test with temporary app settings and `StartHiddenToTray=true`:
  process started hidden/tray-backed and bridge health returned the same shape.

### Daily-Use Polish Follow-up

- Added `BridgeRuntimeState` to track whether speech is currently playing and
  the last bridge request status.
- Expanded `/health` to return `status`, `bridge`, and `speaking`.
- Changed `/speak` to return `409 Conflict` with `{"error":"busy"}` when a
  speech request arrives while another speech request is already playing.
- Added tray actions for Bridge Status and Copy Bridge Token.
- Added a Refresh Status button in the Local Bridge UI.
- Renamed the window button to Hide to Tray.
- Updated README bridge health, busy, and tray action notes.

### Daily-Use Polish Verification

- `dotnet build CodeCompanionDesktop.sln`
- WSL reached `http://<windows-host-ip>:47321/health` and received
  `{"status":"ok","bridge":"listening","speaking":false}`.
- Windows PowerShell reached `http://127.0.0.1:47321/health` and received the
  same health shape.
- WSL unauthenticated `POST /speak` still returned `401 Unauthorized`.

### Changed

- Created branch `feature/local-bridge-endpoint` from the verified ElevenLabs
  TTS test playback baseline.
- Added a local HTTP bridge on Windows port `47321`.
- Added unauthenticated `GET /health` and bearer-token-protected `POST /speak`.
- Added one-time bridge token generation and storage in Windows Credential
  Manager under `CodeCompanionDesktop/BridgeToken`.
- Added a Local Bridge section to the WPF window with bridge status and Copy
  Token.
- Changed the bridge listener from Windows loopback-only to a TCP listener bound
  on port `47321`, because the WSL extension cannot reach a Windows process
  bound only to `127.0.0.1`.
- Updated README bridge setup and next milestones.

### Verified

- `dotnet build CodeCompanionDesktop.sln`
- XML parse check passed for `MainWindow.xaml` and `CodeCompanionDesktop.csproj`.
- `git diff --check`
- Launch smoke test started `CodeCompanionDesktop.exe`.
- Windows PowerShell reached `http://127.0.0.1:47321/health`.
- WSL reached `http://<windows-host-ip>:47321/health`.
- WSL unauthenticated `POST /speak` returned `401 Unauthorized`.
- User copied the bridge token from the desktop app, stored it in the VS Code
  extension, and confirmed `Code Companion Voice: Test Voice` speaks through the
  desktop bridge.

### Decisions

- Keep `/health` unauthenticated for diagnostics.
- Require `Authorization: Bearer <token>` for `/speak`.
- Bind the listener on port `47321` for WSL reachability and rely on the bridge
  token to authorize speech requests.

### Next

- Keep the desktop app running when VS Code voice playback should use the bridge.
- Add startup/minimize behavior and bridge health indicators suitable for daily
  use.

## 2026-05-12 ElevenLabs TTS Test Playback

### Changed

- Created branch `feature/elevenlabs-tts-test` from
  `feature/windows-credential-manager`.
- Added `ElevenLabsTextToSpeechClient` for the ElevenLabs create-speech REST
  endpoint.
- Added `AudioFilePlayer` for MP3 playback through WPF `MediaPlayer`.
- Added a Play ElevenLabs Test button to the main window and tray menu.
- Kept the generated WAV button as a separate offline audio test.
- Added a graceful missing-key path: if no desktop Credential Manager key is
  saved, the UI reports that an ElevenLabs API key must be saved first.
- Updated the README with the hardcoded test phrase, voice ID, model, output
  format, temp-file behavior, and next milestone.

### Verified

- `dotnet build CodeCompanionDesktop.sln`
- XML parse check passed for `MainWindow.xaml` and `CodeCompanionDesktop.csproj`.
- `git diff --check`
- Launch smoke test started `CodeCompanionDesktop.exe`, confirmed the process was
  running, then stopped it.
- User saved the real ElevenLabs API key into the desktop app's Windows
  Credential Manager entry.
- User manually verified Play ElevenLabs Test generated and played live
  ElevenLabs speech from the main window.

### Decisions

- Use the documented non-streaming ElevenLabs create-speech endpoint for the
  first integration test before introducing streaming or queue behavior.
- Use the sample documented voice ID `JBFqnCBsd6RMkjVDRZzb` for the first
  hardcoded test.
- Save generated test MP3 files under the existing `CodeCompanionDesktop` temp
  directory.

### Next

- Start the local authenticated bridge endpoint for VS Code extension requests.

## 2026-05-12 Windows Credential Manager Storage

### Changed

- Created branch `feature/windows-credential-manager` from the verified WPF tray
  proof-of-concept baseline.
- Added `WindowsCredentialStore`, a small P/Invoke wrapper over Windows
  Credential Manager `CredWrite`, `CredRead`, and `CredDelete`.
- Added an ElevenLabs API key panel to the main WPF window with Save, Load, and
  Clear actions.
- Stored the key under generic credential target
  `CodeCompanionDesktop/ElevenLabsApiKey` with user name `ElevenLabs`.
- Kept UI status messages from displaying the key value; they only report
  presence and character count.
- Updated the README credential storage notes and next milestone list.

### Verified

- `dotnet build CodeCompanionDesktop.sln`
- XML parse check passed for `MainWindow.xaml` and `CodeCompanionDesktop.csproj`.
- `git diff --check`
- Temporary Windows Credential Manager round-trip with a test target:
  save, read, delete, and confirm missing after delete.
- Launch smoke test started `CodeCompanionDesktop.exe`, confirmed the process was
  running, then stopped it.
- User manually verified the WPF credential UI Save, Load, and Clear flow.

### Decisions

- Use direct Windows Credential Manager APIs for now instead of adding an
  external package.
- Keep the first credential UI in the main proof-of-concept window until the app
  needs a separate settings surface.

### Next

- Start Milestone 3: generate ElevenLabs TTS from a hardcoded test phrase using
  the saved key, then play the returned audio.

## 2026-05-12 WPF Tray Proof Of Concept

### Manual Verification Follow-up

- User ran `dotnet clean .\CodeCompanionDesktop.sln` from PowerShell in
  `D:\Development\CodeCompanionDesktop`; clean completed successfully.
- User ran `dotnet build .\CodeCompanionDesktop.sln`; build succeeded with 0
  warnings and 0 errors.
- User ran
  `dotnet run --project .\src\CodeCompanionDesktop\CodeCompanionDesktop.csproj`.
- User confirmed the app opened and Play Test Sound produced audible audio.
- Determined the remaining `PlayButton` and related red underscores in
  `MainWindow.xaml.cs` are editor-only C# Dev Kit state, not compiler errors.

### Repository Direction Follow-up

- Confirmed `D:\Development\CodeCompanionDesktop` is the primary starting point
  for desktop-app sessions.
- Documented that the desktop app should be opened in local Windows VS Code, not
  a VS Code WSL Remote window, so C# Dev Kit can load WPF design-time builds.
- Confirmed `/var/www/CodeCompanion` remains the existing VS Code extension
  codebase and will still be needed for bridge integration.
- Documented that playback work is moving out of the WSL fallback path and into
  the .NET desktop app.
- Recorded the current recommendation to keep the desktop app and extension in
  separate repositories until the bridge protocol and release process are stable.
- Added folder-level VS Code settings so opening the desktop folder directly
  still selects `CodeCompanionDesktop.sln` as the default solution.

### Follow-up

- Confirmed Windows `dotnet.exe` can build the project from WSL using the
  installed Windows .NET SDK.
- Confirmed `dotnet run` from the repository root fails unless the nested WPF
  project is passed with `--project`.
- Added the root solution file to source control and configured the tracked VS
  Code workspace to use it as the default solution for C# Dev Kit.
- Updated the README run instructions to call out the required `--project`
  argument.

### Follow-up Verification

- `/mnt/c/Program Files/dotnet/dotnet.exe build CodeCompanionDesktop.sln`
- `/mnt/c/Program Files/dotnet/dotnet.exe build src/CodeCompanionDesktop/CodeCompanionDesktop.csproj /v:minimal`
- `/mnt/c/Program Files/dotnet/dotnet.exe run --project src/CodeCompanionDesktop/CodeCompanionDesktop.csproj`

### Follow-up Next

- Reopen `CodeCompanionDesktop.code-workspace` in VS Code so C# Dev Kit loads
  `CodeCompanionDesktop.sln`.
- From PowerShell in `D:\Development\CodeCompanionDesktop`, run
  `dotnet run --project .\src\CodeCompanionDesktop\CodeCompanionDesktop.csproj`
  and manually verify the tray window and test sound.

### Changed

- Initialized the `CodeCompanionDesktop` git repository.
- Created branch `feature/wpf-tray-poc`.
- Added the handover document and initial README.
- Scaffolded a minimal .NET 8 WPF Windows tray app.
- Added a status window with Play Test Sound, Hide, and Exit controls.
- Added a tray icon with Show, Play Test Sound, and Exit menu items.
- Added generated local WAV test tone playback through `System.Media.SoundPlayer`.
- Configured `origin` as `git@github.com:davidmjackson/codecompaniondesktop.git`.
- Generated and configured a repo-specific SSH key for this checkout.

### Verified

- Windows .NET SDK is available at `C:\Program Files\dotnet\dotnet.exe`.
- `/mnt/c/Program Files/dotnet/dotnet.exe --info` reports .NET SDK `8.0.420`.
- `dotnet build src/CodeCompanionDesktop/CodeCompanionDesktop.csproj` succeeds with 0 warnings and 0 errors.
- XML parse check passed for `.xaml` and `.csproj` files.
- `git diff --check HEAD` passed.
- Launch smoke test started `CodeCompanionDesktop.exe`, confirmed the process was running, then stopped it.
- Branch `feature/wpf-tray-poc` was pushed to GitHub at commit `83b638d592c416fa12cee2fd8627de794a7c3d4e`.

### Current State

- Latest local commit before this log entry: `83b638d Fix WPF application namespace ambiguity`.
- Remote branch: `origin/feature/wpf-tray-poc`.
- GitHub currently reports `feature/wpf-tray-poc` as repository HEAD because it was the first branch pushed to the empty repo.
- No `CodeCompanionDesktop`, `dotnet`, or project server process should be left running.
- Manual audio verification passed on 2026-05-12.

### Next

- Start Milestone 2: Windows Credential Manager storage for the ElevenLabs API key.
