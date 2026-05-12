# Session Log

Use this log to preserve project context between work sessions. Keep entries
concise: what changed, what was verified, decisions made, and the next useful
options.

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
