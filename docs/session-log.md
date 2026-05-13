# Session Log

Use this log to preserve project context between work sessions. Keep entries
concise: what changed, what was verified, decisions made, and the next useful
options.

## 2026-05-13 MainWindow IDE Error Triage

### Changed

- No application code changed.
- Reproduced the desktop build after a VS Code restart and checked the
  `MainWindow.xaml.cs` diagnostics context.
- Stopped the running debug `CodeCompanionDesktop.exe` process because it was
  locking the debug output executable during build.

### Verified

- `dotnet build CodeCompanionDesktop.sln` using
  `C:\Program Files\dotnet\dotnet.exe` completed with `0 Error(s)`.
- Confirmed the XAML generated members used by `MainWindow.xaml.cs` exist in
  `src/CodeCompanionDesktop/obj/Debug/net8.0-windows/MainWindow.g.cs`.

### Decision

- The reported `MainWindow.xaml.cs` errors are design-time IDE diagnostics, not
  compiler errors. This matches the README warning that opening the WPF project
  through WSL can show false errors for XAML-generated members.

### Next

- Open this repository in a normal Windows VS Code window, not WSL Remote, for
  WPF editing and C# Dev Kit diagnostics.
- Continue with VS Code extension first-run desktop bridge detection and
  installer-link guidance if that is still the priority.

## 2026-05-13 Installer PR Merge

### Changed

- Marked PR #2, `Add Windows installer packaging`, ready for review.
- Merged PR #2 from `feature/windows-installer` into
  `feature/release-publish-path`.
- Fast-forwarded the local `feature/release-publish-path` branch to merge
  commit `3f92cc2`.

### Verified

- `git diff --check`
- Windows PowerShell bridge health check:
  `{"status":"ok","bridge":"listening","speaking":false,"queueEnabled":false,"queued":0,"queueLimit":3}`
- Confirmed the running desktop process is the debug build at
  `D:\Development\CodeCompanionDesktop\src\CodeCompanionDesktop\bin\Debug\net8.0-windows\CodeCompanionDesktop.exe`.

### Next

- Decide whether to continue desktop-app work in this repository or switch to
  `/var/www/CodeCompanion` for VS Code extension first-run desktop bridge
  detection and installer-link guidance.

## 2026-05-12 Stop Point

### Current State

- Current branch: `feature/windows-installer`.
- Latest commit: `7e2a45f Allow installer terms in README spellcheck`.
- PR #2 is open as a draft from `feature/windows-installer` into
  `feature/release-publish-path`.
- The Inno Setup installer was built and installed successfully.
- The installed app is running and bridge health is:
  `{"status":"ok","bridge":"listening","speaking":false,"queueEnabled":false,"queued":0,"queueLimit":3}`

### Next

- Merge PR #2 into `feature/release-publish-path`.
- Then start the VS Code extension first-run desktop bridge detection and
  installer-link guidance work in `/var/www/CodeCompanion` if that is still the
  priority.

## 2026-05-12 Windows Installer Packaging

### Changed

- Created branch `feature/windows-installer` from `feature/release-publish-path`.
- Added `installer/CodeCompanionDesktop.iss` for a per-user Inno Setup installer.
- Added `scripts/build-installer.ps1` to publish the self-contained app and
  compile the installer with `ISCC.exe`.
- The installer targets
  `%LOCALAPPDATA%\Programs\Code Companion Desktop`, adds a Start Menu shortcut,
  offers an optional desktop shortcut, and can launch the app after install.
- Updated README with the two-install distribution model: VS Code extension from
  the Marketplace, Windows desktop app from the installer, paired by bridge
  token.
- Updated README installer build and daily install steps.

### Verified

- `git diff --check`
- PowerShell parser check for `scripts/build-installer.ps1`
- `dotnet build CodeCompanionDesktop.sln --no-incremental` using
  `C:\Program Files\dotnet\dotnet.exe`
- `dotnet test CodeCompanionDesktop.sln --no-build` using
  `C:\Program Files\dotnet\dotnet.exe`
- `scripts\build-installer.ps1 -AppVersion 0.1.0` published the app, then
  stopped with the expected message because Inno Setup `ISCC.exe` is not
  installed on this machine.
- Launched the published executable and confirmed bridge health:
  `{"status":"ok","bridge":"listening","speaking":false,"queueEnabled":false,"queued":0,"queueLimit":3}`
- Stopped the published app after smoke testing.

### Next

- Install Inno Setup 6 on the Windows side and rerun
  `scripts\build-installer.ps1 -AppVersion 0.1.0` to compile the `.exe`
  installer.
- Add VS Code extension first-run checks for the desktop bridge and installer
  link.

### Installer Follow-up Verification

- User installed Inno Setup 6 and ran the installer build from PowerShell with
  execution-policy bypass.
- User ran the generated installer successfully.
- Installer created the installed executable at
  `%LOCALAPPDATA%\Programs\Code Companion Desktop\CodeCompanionDesktop.exe`.
- User selected the optional desktop shortcut; verified
  `Code Companion Desktop.lnk` points to the installed executable.
- Confirmed the installed app bridge health:
  `{"status":"ok","bridge":"listening","speaking":false,"queueEnabled":false,"queued":0,"queueLimit":3}`

## 2026-05-12 Bridge Queue Publish

### Changed

- Marked PR #1 ready and merged `feature/bridge-speech-queue` into
  `feature/release-publish-path`.
- Pulled the merged `feature/release-publish-path` branch locally.
- Published a fresh daily build to
  `artifacts\publish\CodeCompanionDesktop-win-x64`.

### Verified

- `scripts\publish-release.ps1` completed successfully from PowerShell.
- Launched the published executable from
  `artifacts\publish\CodeCompanionDesktop-win-x64\CodeCompanionDesktop.exe`.
- Confirmed published bridge health includes queue fields:
  `{"status":"ok","bridge":"listening","speaking":false,"queueEnabled":false,"queued":0,"queueLimit":3}`
- Stopped the published app after smoke testing.

### Next

- Use the published daily build normally with the VS Code extension.
- Defer bridge request history/logging until troubleshooting visibility is
  actually needed.

## 2026-05-12 Bridge Queue Local Smoke Test

### Changed

- No application code changed.
- Tested the `feature/bridge-speech-queue` branch in the Code Companion Desktop
  checkout.

### Verified

- `git diff --check`
- `dotnet build CodeCompanionDesktop.sln --no-incremental` using
  `C:\Program Files\dotnet\dotnet.exe`
- `dotnet test CodeCompanionDesktop.sln --no-build` using
  `C:\Program Files\dotnet\dotnet.exe`
- Launched the debug app and confirmed default bridge health:
  `{"status":"ok","bridge":"listening","speaking":false,"queueEnabled":false,"queued":0,"queueLimit":3}`
- Backed up `%APPDATA%\CodeCompanionDesktop\settings.json`, temporarily enabled
  bridge queueing with a limit of 5, launched the debug app, and confirmed
  bridge health:
  `{"status":"ok","bridge":"listening","speaking":false,"queueEnabled":true,"queued":0,"queueLimit":5}`
- Stopped the debug app and restored the original settings file.

### Next

- Review and merge PR #1, then publish the updated daily build.

## 2026-05-12 Private GitHub Workflow Note

### Changed

- Updated `AGENTS.md` to note that the Code Companion Desktop GitHub repository
  is private.
- Documented that authenticated `gh` should be the primary tool for GitHub repo,
  PR, issue, and metadata operations.
- Added a reminder to verify GitHub access with `gh auth status` when needed.

### Verified

- `git diff --check`

### Next

- Continue using `gh` for private-repo GitHub operations.

## 2026-05-12 Bridge Speech Queue Settings

### Changed

- Created branch `feature/bridge-speech-queue`.
- Added persisted bridge speech queue settings:
  `QueueBridgeSpeechRequests` and `MaxQueuedBridgeSpeechRequests`.
- Added a Local Bridge UI surface to enable queueing and choose a queue limit of
  1, 3, 5, or 10 pending requests.
- Added `BridgeSpeechQueue` to process queued bridge speech requests serially in
  the background.
- Kept existing default behavior: when queueing is disabled, busy bridge speech
  still returns `409 {"error":"busy"}`.
- Expanded bridge `/health` with `queueEnabled`, `queued`, and `queueLimit`.
- Updated README bridge docs and next milestones.

### Verified

- `git diff --check`
- `dotnet build CodeCompanionDesktop.sln --no-incremental` using
  `C:\Program Files\dotnet\dotnet.exe`
- `dotnet test CodeCompanionDesktop.sln --no-build` using
  `C:\Program Files\dotnet\dotnet.exe`
- Launched the debug app and confirmed bridge health included queue fields:
  `{"status":"ok","bridge":"listening","speaking":false,"queueEnabled":false,"queued":0,"queueLimit":3}`
- Stopped the debug app after smoke testing.

### Next

- Add an installer or update flow for non-developer daily use.
- Add bridge request history/logging for easier troubleshooting.

## 2026-05-12 Project Instruction Guardrail

### Changed

- Added root `AGENTS.md` with Code Companion Desktop-specific instructions.
- Documented that this repository is not the Retrospective app and that agents
  should not switch to `/var/www/retrospective` unless explicitly asked.
- Captured the expected session-start checks, git workflow, build/test commands,
  app process workflow, session-log expectations, and communication rules.

### Verified

- `git diff --check`

### Next

- Continue with the queue/settings surface for bridge speech behavior.

## 2026-05-12 Daily Install Guidance

### Changed

- Added README guidance for using the published folder as the current portable
  daily build.
- Documented the PowerShell publish command, published executable path,
  startup setting order, diagnostics check, and re-registration requirement if
  the published folder moves.
- Updated the next milestones now that packaged-path autostart guidance exists.

### Verified

- `git diff --check`

### Next

- Add a queue/settings surface for bridge speech behavior.
- Add an installer or update flow for non-developer daily use.

## 2026-05-12 Local Desktop Bridge Endpoint

### Release Publish Path

- Created branch `feature/release-publish-path` from
  `feature/custom-app-icons`.
- Added `scripts/publish-release.ps1`.
- The script publishes a self-contained `win-x64` single-file build to
  `artifacts\publish\CodeCompanionDesktop-win-x64` by default.
- The script resolves `dotnet.exe` from PATH first, then falls back to
  `C:\Program Files\dotnet\dotnet.exe`.
- Added `artifacts/` to `.gitignore`.
- Documented the publish command, output path, daily-use executable, and
  startup re-registration note in the README.

### Release Publish Verification

- `scripts/publish-release.ps1` published a self-contained `win-x64`
  single-file build to
  `artifacts\publish\CodeCompanionDesktop-win-x64`.
- Published output included `CodeCompanionDesktop.exe` and
  `CodeCompanionDesktop.pdb`; `artifacts/` is ignored by git.
- Confirmed Windows can extract an associated icon from the published
  executable.
- Stopped the running debug app before the clean build/test checks.
- `dotnet build CodeCompanionDesktop.sln` completed with 0 warnings and
  0 errors using `C:\Program Files\dotnet\dotnet.exe`.
- `dotnet test CodeCompanionDesktop.sln --no-build`
- `git diff --check`
- Launched the published executable and confirmed bridge health:
  `{"status":"ok","bridge":"listening","speaking":false}`.
- Next useful action: add installer/autostart guidance for the packaged app
  path.

### Custom App And Tray Icons

- Created branch `feature/custom-app-icons` from `feature/app-icon-metadata`.
- Added user-supplied `Assets/app.png` and `Assets/tray.png`.
- Regenerated `Assets/app.ico` from `app.png` with 16, 24, 32, 48, 64, 128,
  and 256 pixel entries.
- Updated the WPF window icon/header image to use `app.png`.
- Updated the WinForms tray icon generation to use `tray.png`.
- Added both PNGs as WPF resources.
- Updated README current scope to note custom app and tray icons.

### Custom Icons Verification

- Stopped the running debug app before building to avoid executable lock
  warnings.
- `dotnet build CodeCompanionDesktop.sln` completed with 0 warnings and
  0 errors using `C:\Program Files\dotnet\dotnet.exe`.
- Confirmed Windows can extract an associated icon from the built
  `CodeCompanionDesktop.exe`.
- `dotnet test CodeCompanionDesktop.sln --no-build`
- `git diff --check`
- Relaunched the debug app and confirmed bridge health:
  `{"status":"ok","bridge":"listening","speaking":false}`.

### Packaged App Icon Metadata

- Created branch `feature/app-icon-metadata` from
  `feature/startup-diagnostics`.
- Generated `Assets/app.ico` from the existing Code Companion PNG icon with
  16, 24, 32, 48, 64, and 128 pixel entries.
- Set `ApplicationIcon` in `CodeCompanionDesktop.csproj` so Windows build
  outputs carry the app icon metadata.
- Kept `Assets/icon-128.png` as the WPF window/tray rendering resource.
- Updated README current scope and next milestones.

### Packaged App Icon Verification

- Stopped the running debug app before building to avoid executable lock
  warnings.
- `dotnet build CodeCompanionDesktop.sln` completed with 0 warnings and
  0 errors using `C:\Program Files\dotnet\dotnet.exe`.
- Confirmed Windows can extract an associated icon from the built
  `CodeCompanionDesktop.exe`.
- `dotnet test CodeCompanionDesktop.sln --no-build`
- `git diff --check`
- Relaunched the debug app and confirmed bridge health:
  `{"status":"ok","bridge":"listening","speaking":false}`.
- Next useful action: add a release packaging path for daily desktop use.

### Startup Diagnostics Surface

- Created branch `feature/startup-diagnostics` from
  `feature/windows-app-polish`.
- Added `StartupRegistrationDiagnostics` to read the current Windows Run value,
  extract the registered executable path, compare it with the running app, and
  check whether the registered target exists.
- Added `Refresh Diagnostics` and `Copy Diagnostics` actions to the Startup UI.
- Startup diagnostics now refresh on app load, after startup preference changes,
  and on demand.
- Updated README startup behavior notes and next milestones.

### Startup Diagnostics Verification

- Stopped the running debug app before building to avoid executable lock
  warnings.
- `dotnet build CodeCompanionDesktop.sln` completed with 0 warnings and
  0 errors using `C:\Program Files\dotnet\dotnet.exe`.
- `dotnet test CodeCompanionDesktop.sln --no-build`
- `git diff --check`
- Relaunched the debug app and confirmed bridge health:
  `{"status":"ok","bridge":"listening","speaking":false}`.
- Next useful action: add packaged app icon metadata for release builds.

### Windows App Styling And Tray Icon

- Created branch `feature/windows-app-polish` from
  `feature/windows-login-startup`.
- User verified after a Windows restart that the app loaded in the system tray
  when `Start with Windows sign-in` and `Start hidden to tray` were enabled.
- Reused the VS Code extension icon from
  `/var/www/CodeCompanion/assets/icon-128.png` as a WPF resource.
- Set the WPF window icon and generated the WinForms tray icon from the same
  resource at runtime.
- Added shared WPF styles for the app background, header, cards, buttons,
  muted text, password box, and checkboxes.
- Updated the README next milestones now that Windows sign-in startup has been
  manually verified.

### Windows App Styling Verification

- Initial build hit retry warnings because the tray app was still running from
  `bin\Debug`; stopped `CodeCompanionDesktop` process `14700` and reran.
- `dotnet build CodeCompanionDesktop.sln` completed with 0 warnings and
  0 errors using `C:\Program Files\dotnet\dotnet.exe`.
- `dotnet test CodeCompanionDesktop.sln --no-build`
- `git diff --check`
- Next useful action: add the startup diagnostics surface for the registered
  Windows Run command.

### Windows Login Startup Follow-up

- Created branch `feature/windows-login-startup` from
  `feature/start-hidden-to-tray`.
- Added `WindowsStartupRegistration`, using the current-user Windows Run key:
  `HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run`.
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
- Manual Windows sign-in verification is still pending. The user may need to
  restart Windows rather than sign out/in because the machine uses the default
  Windows account flow.

### Current Stop Point

- Current branch: `feature/windows-login-startup`.
- Latest implementation commit: `6510f67 Add Windows login startup option`.
- Working tree was clean before this stop-point note.
- No `CodeCompanionDesktop` process was running.
- Next useful action: after Windows restarts and signs in, confirm the app
  starts in the tray when both `Start with Windows sign-in` and
  `Start hidden to tray` are enabled.

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
