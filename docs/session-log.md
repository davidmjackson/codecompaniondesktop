# Session Log

Use this log to preserve project context between work sessions. Keep entries
concise: what changed, what was verified, decisions made, and the next useful
options.

## 2026-05-14 Communication Preference Clarified

### Current Milestone

- Milestone 9: Public Release Packaging.

### Changed

- Updated `AGENTS.md` so spoken/text summaries are the default after every
  major milestone slice, important decision, or blocking issue.

### Verified

- User confirmed Desktop Test speech was working before this preference update.

### Next

- Continue using concise progress updates while working.
- For significant milestones, decisions, or issues, send a short spoken summary
  through Code Companion Desktop when the app is running and include the same
  point in the final text summary.

## 2026-05-14 Post-Reboot CLR Recovery Verified

### Current Milestone

- Milestone 9: Public Release Packaging.

### Current State

- Branch: `feature/environment-agnostic-speech-architecture`.
- Current commit before this entry: `36f2a1e`.
- Working tree was clean before this log update.
- Code Companion Desktop was running from the Debug build as PID `29680`:
  `D:\Development\CodeCompanionDesktop\src\CodeCompanionDesktop\bin\Debug\net8.0-windows\CodeCompanionDesktop.exe`.

### Verified

- After reboot, Windows PowerShell 5.1 starts successfully again.
- `powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -Command '$PSVersionTable.PSVersion.ToString()'`
  returns `5.1.26100.8457`.
- This confirms the earlier `.NET Framework 4.x` / CLR startup failure
  `HRESULT 80004005` cleared after reboot.

### Notes

- Treat the CLR issue as resolved unless the same startup error reappears.
- Follow-up correction: the Desktop bridge port is `47321`, not `5138`.
  `http://127.0.0.1:47321/health` later returned healthy bridge status from
  the same running Debug process.

### Next

- Continue Milestone 9 with Code Companion Voice Marketplace/package hardening
  and fresh-install verification when explicitly switching to the Voice
  repository.
- If Desktop bridge testing is needed, restart Code Companion Desktop and
  recheck `/health`.

## 2026-05-14 Windows PowerShell CLR Failure

### Current Milestone

- Milestone 9: Public Release Packaging.

### Current State

- Branch: `feature/environment-agnostic-speech-architecture`.
- Working tree was clean before this troubleshooting entry.
- Code Companion Desktop was running as `CodeCompanionDesktop.exe` PID `31460`.

### Diagnosed

- Windows PowerShell 5.1 fails immediately with:
  `Starting the CLR failed with HRESULT 80004005`.
- Windows Application logs show multiple .NET Framework 4.x startup crashes
  against `C:\Windows\Microsoft.NET\Framework64\v4.0.30319\clr.dll`.
- Affected startup-time processes included Windows PowerShell, ASUS/ROG
  services, Intel XTU, and `taskhostw.exe`.
- Windows `.NET 8` SDK/runtime is healthy; the issue appears isolated to
  Windows-owned .NET Framework 4.x/CLR startup.

### Changed

- Installed PowerShell 7.6.1 with `winget` as a working `pwsh.exe` fallback.

### Verified

- `pwsh.exe -NoLogo -NoProfile -Command '$PSVersionTable.PSVersion.ToString()'`
  returns `7.6.1`.
- `DISM /Online /Cleanup-Image /RestoreHealth` completed successfully.
- `sfc /scannow` completed with:
  `Windows Resource Protection did not find any integrity violations.`
- Windows PowerShell 5.1 still fails before reboot with CLR error `80004005`.

### Next

- Reboot Windows.
- After reboot, retest Windows PowerShell 5.1:
  `powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "$PSVersionTable.PSVersion.ToString()"`
- If Windows PowerShell still fails, continue with official .NET Framework
  repair/reinstall or Windows feature servicing for .NET Framework 4.x.

## 2026-05-13 End Of Day Handover

### Current Milestone

- Milestone 9: Public Release Packaging.

### Current State

- Branch: `feature/environment-agnostic-speech-architecture`.
- Current commit before this handover entry: `04747fb`.
- Working tree was clean before this handover entry.
- Code Companion Desktop `0.1.1` is installed and running from:
  `C:\Users\User\AppData\Local\Programs\Code Companion Desktop\CodeCompanionDesktop.exe`.
- Running Desktop process observed as PID `17900`.
- Windows GitHub CLI is installed, authenticated as `davidmjackson`, and has
  the required `repo` and `workflow` scopes.

### Release State

- Desktop draft GitHub Release exists for `v0.1.1`.
- Release is still a draft. Do not publish it yet.
- Uploaded draft release assets:
  - `CodeCompanionDesktopSetup-0.1.1.exe`
  - `CodeCompanionDesktopSetup-0.1.1.exe.sha256`
- Installer SHA256:
  `38f09b026fa40907b2d79e8840be7b40a5e80c420f08feb304846dea1d1530d7`.
- Local release package artifacts are under:
  - `D:\Development\CodeCompanionDesktop\artifacts\installer`
  - `D:\Development\CodeCompanionDesktop\artifacts\checksums`
  - `D:\Development\CodeCompanionDesktop\artifacts\release-notes`

### Verified Today

- Desktop `0.1.1` installer was built, installed, and health-checked.
- `GET /health` reported `appVersion: 0.1.1`.
- Code Companion Voice `0.0.50` Desktop Test produced audible speech through
  the installed Desktop app.
- `scripts/build-release-package.ps1` ran `dotnet build`, `dotnet test`, built
  the installer, generated checksum, and generated draft release notes.
- `dotnet test` passed 27 tests.
- `scripts/draft-github-release.ps1` created the Desktop draft release after
  its argument quoting was fixed.

### Important Decisions

- Desktop owns provider configuration, ElevenLabs calls, policy, queueing,
  native Windows audio, pairing, and release packaging.
- Code Companion Voice remains a thin VS Code client and should not own API
  keys, WebView audio unlock, provider calls, or long-lived bridge tokens.
- The legacy Desktop `/speak` endpoint and bridge-token compatibility have
  been removed.
- Do not publish the Desktop GitHub Release until Voice Marketplace/package
  hardening and fresh-install verification are complete.

### Tomorrow Starting Point

- Continue Milestone 9 with Code Companion Voice Marketplace/package hardening.
- Work in the Voice repository only when explicitly switching to
  `D:\Development\CodeCompanionVoice` / `/mnt/d/Development/CodeCompanionVoice`.
- Check Voice package metadata, publisher/private flags, Marketplace readiness,
  install URL defaults, and VSIX packaging.
- After Voice hardening, perform fresh-install verification from the draft
  Desktop release asset and the intended Voice package source in both:
  - normal Windows VS Code
  - WSL Remote VS Code

## 2026-05-13 Desktop Draft GitHub Release Created

### Current Milestone

- Milestone 9: Public Release Packaging.

### Changed

- Created a draft GitHub Release for Code Companion Desktop `v0.1.1`.
- Uploaded release assets:
  - `CodeCompanionDesktopSetup-0.1.1.exe`
  - `CodeCompanionDesktopSetup-0.1.1.exe.sha256`
- Fixed `scripts/draft-github-release.ps1` argument quoting so release titles
  containing spaces are passed correctly to `gh`.

### Verified

- `gh release view v0.1.1 --repo davidmjackson/codecompaniondesktop` reports
  `isDraft: true` and `tagName: v0.1.1`.
- The installer asset digest reported by GitHub matches the generated SHA256:
  `38f09b026fa40907b2d79e8840be7b40a5e80c420f08feb304846dea1d1530d7`.

### Next

- Do not publish the draft release yet.
- Continue Milestone 9 with Voice Marketplace/package hardening and fresh
  install verification from the draft Desktop release asset.

## 2026-05-13 Windows GitHub CLI Path

### Current Milestone

- Milestone 9: Public Release Packaging.

### Changed

- Confirmed Windows GitHub CLI is installed at
  `C:\Program Files\GitHub CLI\gh.exe`.
- Added `C:\Program Files\GitHub CLI` to the Windows user PATH.
- Updated `scripts/draft-github-release.ps1` to fall back to standard GitHub CLI
  install locations when the current process PATH has not refreshed yet.

### Verified

- `C:\Program Files\GitHub CLI\gh.exe --version` reports `2.92.0`.
- Windows GitHub CLI is authenticated as `davidmjackson` with `repo` and
  `workflow` scopes.

### Next

- Use `scripts\draft-github-release.ps1 -AppVersion <version> -Create` when
  ready to create the draft Desktop GitHub Release.

## 2026-05-13 Desktop Release Package Script

### Current Milestone

- Milestone 9: Public Release Packaging.

### Changed

- Added `scripts/build-release-package.ps1`.
- The script runs Desktop build/test checks, builds the installer, writes a
  SHA256 checksum file, and writes draft Desktop GitHub release notes.
- Updated `scripts/publish-release.ps1` to launch `dotnet publish` with
  checked native process execution.
- Updated README and `docs/release-checklist.md` to use the release-package
  script for release candidates.
- Updated `docs/architecture.md` so Milestone 9 is now in progress.

### Verified

- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'D:\Development\CodeCompanionDesktop\scripts\build-release-package.ps1' -AppVersion 0.1.1 ...`
- Release package script ran `dotnet build`, `dotnet test`; 27 tests passed.
- Generated installer:
  `D:\Development\CodeCompanionDesktop\artifacts\installer\CodeCompanionDesktopSetup-0.1.1.exe`
- Generated checksum:
  `D:\Development\CodeCompanionDesktop\artifacts\checksums\CodeCompanionDesktopSetup-0.1.1.exe.sha256`
- Generated release notes:
  `D:\Development\CodeCompanionDesktop\artifacts\release-notes\desktop-0.1.1.md`

### Next

- Decide whether to draft the GitHub Release from the generated installer,
  checksum, and notes, or continue hardening Marketplace metadata first.

## 2026-05-13 Desktop Draft GitHub Release Script

### Current Milestone

- Milestone 9: Public Release Packaging.

### Changed

- Added `scripts/draft-github-release.ps1`.
- The script validates the installer, checksum, and release notes generated by
  `scripts/build-release-package.ps1`.
- The script is dry-run by default and only creates a draft GitHub Release when
  `-Create` is passed.
- Updated README, architecture, and release checklist documentation.

### Verified

- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'D:\Development\CodeCompanionDesktop\scripts\draft-github-release.ps1' -AppVersion 0.1.1`
- Dry run validated the `0.1.1` installer, checksum, and release notes, then
  printed the draft `gh release create` command.
- Dry run no longer requires Windows PowerShell to have `gh` on PATH; `gh` is
  required only when `-Create` is passed.

### Next

- Decide whether to install `gh` into Windows PowerShell before using `-Create`,
  create the draft release from WSL manually, or continue Voice Marketplace
  hardening first.

## 2026-05-13 Desktop 0.1.1 Installer Build

### Current Milestone

- Milestone 9: Public Release Packaging.

### Changed

- Built local Desktop installer `CodeCompanionDesktopSetup-0.1.1.exe` from
  commit `6d92c93`.

### Verified

- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'D:\Development\CodeCompanionDesktop\scripts\build-installer.ps1' -AppVersion 0.1.1`
- Installer output:
  `D:\Development\CodeCompanionDesktop\artifacts\installer\CodeCompanionDesktopSetup-0.1.1.exe`
- Installed app health check returned `appVersion: 0.1.1`.
- Installed executable product version includes commit `6d92c93`.
- Code Companion Voice `0.0.50` Desktop Test produced audible speech through
  the installed Desktop app.

### Next

- Continue Milestone 9 release hardening: decide whether the public release
  package needs signing, GitHub Release automation, or Marketplace packaging
  before publishing.

## 2026-05-13 Remove Desktop Legacy Bridge Token

### Current Milestone

- Milestone 8: Cleanup And Compatibility Removal.

### Changed

- Removed Desktop `POST /speak` compatibility endpoint.
- Removed legacy bridge-token authorization from `/v1/speech/candidates`.
- Removed `BridgeTokenStore` and no longer creates
  `CodeCompanionDesktop/BridgeToken` in Windows Credential Manager.
- Removed `Copy Legacy Token` from the main window and
  `Copy Legacy Bridge Token` from the tray menu.
- Updated bridge tests to authorize speech candidates with `/v1/client/hello`
  session tokens.
- Updated README and architecture docs to describe Client Pairing plus
  short-lived session authorization as the only HTTP speech path.

### Decision

- Milestone 8 compatibility removal is complete for the current Desktop and
  Voice builds. Older token-based extension builds are no longer supported by
  the current Desktop bridge.

### Verified

- `/mnt/c/Program\ Files/dotnet/dotnet.exe build CodeCompanionDesktop.sln`
- `/mnt/c/Program\ Files/dotnet/dotnet.exe test CodeCompanionDesktop.sln --no-build`; 27 tests passed.
- `git diff --check`

### Next

- Package and smoke test the updated Desktop app with Code Companion Voice
  `0.0.50`.

## 2026-05-13 Compatibility Audit

### Current Milestone

- Milestone 8: Cleanup And Compatibility Removal.

### Changed

- Audited Desktop and Voice repositories for legacy speech surfaces.
- Updated `docs/architecture.md` with the remaining compatibility surface.
- Updated Desktop install and release-checklist expectations for the new thin
  Code Companion Voice panel.

### Findings

- Code Companion Voice `0.0.50` no longer stores provider keys, long-lived
  bridge tokens, or VS Code-owned playback/provider code.
- Code Companion Voice still supports Desktop-owned candidate inbox fallback.
- Code Companion Desktop still has the compatibility-only `/speak` endpoint,
  legacy bridge token authorization on `/v1/speech/candidates`, and legacy token
  copy actions.

### Decision

- Candidate inbox fallback remains because it is a Windows Desktop-owned
  ingress path.
- Desktop `/speak` and legacy token authorization are the last Milestone 8
  compatibility items. Remove them after confirming older installed extension
  builds no longer need support.

### Verified

- Documentation-only Desktop change.
- `git diff --check`

### Next

- Remove Desktop `/speak` and legacy token authorization, or explicitly defer
  them until the first public release boundary.

## 2026-05-13 VS Code Panel Control Cleanup Decision

### Current Milestone

- Milestone 8: Cleanup And Compatibility Removal.

### Decision

- Add removal of normal VS Code panel audio controls to Milestone 8.
- `Enable Voice` and `Mute` should move out of the normal Code Companion Voice
  panel because Code Companion Desktop owns voice enablement, mute, playback,
  queueing, and provider state.
- Keep `Desktop Test` in the VS Code panel for now as a bridge diagnostic.

### Changed

- Updated `docs/architecture.md` Milestone 8 scope, acceptance criteria, and
  status to include the VS Code panel audio-control cleanup.

### Verified

- Documentation-only change.
- `git diff --check`

### Next

- Implement the Code Companion Voice panel cleanup in the Voice repository,
  then package a new VSIX and smoke test `Desktop Test`.

## 2026-05-13 Legacy Token Wording Cleanup

### Current Milestone

- Milestone 8: Cleanup And Compatibility Removal.

### Changed

- Relabelled Desktop `Copy Token` UI as `Copy Legacy Token`.
- Relabelled the tray token action as `Copy Legacy Bridge Token`.
- Updated Desktop install and release-checklist steps to use Client Pairing as
  the normal setup path.
- Updated bridge-token documentation to describe token use as older-build and
  compatibility fallback only.

### Decision

- The legacy token code remains for compatibility, but the normal UX and docs no
  longer present copied tokens as the recommended pairing path.

### Verified

- `dotnet build CodeCompanionDesktop.sln`
- `dotnet test CodeCompanionDesktop.sln --no-build`; 27 tests passed.
- `git diff --check`

### Next

- Continue Milestone 8 cleanup in Code Companion Voice by removing or hiding
  remaining VS Code-owned provider/audio/webview paths.

## 2026-05-13 Pairing Smoke Test

### Current Milestone

- Milestone 6: Pairing Without Persistent VS Code Secrets.

### Verified

- Built and installed the updated Desktop installer from
  `artifacts\installer\CodeCompanionDesktopSetup-0.1.0.exe`.
- Installed Code Companion Voice `0.0.46` into both Windows and WSL VS Code
  extension hosts.
- Confirmed Desktop bridge health at `http://127.0.0.1:47321/health`.
- Opened a new VS Code window to avoid losing the active Codex chat.
- Ran `Code Companion Voice: Open Panel` and confirmed the thin-client buttons.
- Clicked `Desktop Test`; Desktop recorded the WSL VS Code client as pending in
  the Client Pairing panel.
- Approved client `31b62747-aac7-42f8-b0a0-22568dcb33e4` in Desktop.
- Clicked `Desktop Test` again and heard speech through Code Companion Desktop.

### Decision

- Milestone 6 is verified end to end for WSL-hosted VS Code client pairing and
  Desktop-owned speech playback.

### Next

- Continue to Milestone 8 cleanup unless another pairing polish item is needed.

## 2026-05-13 Voice Pairing Migration

### Current Milestone

- Milestone 6: Pairing Without Persistent VS Code Secrets.

### Changed

- Updated Code Companion Voice on branch `feature/desktop-bridge-client` to use
  `/v1/client/hello` and short-lived Desktop session authorization for normal
  speech candidate delivery.
- Voice now blocks delivery when Desktop pairing is pending or denied instead
  of bypassing approval through the candidate inbox.
- The legacy bridge token command remains only as a migration fallback.
- Bumped Code Companion Voice to `0.0.46` and packaged
  `code-companion-voice-0.0.46.vsix`.
- Updated the architecture status to mark Milestone 6 complete.

### Verified

- In the Voice repository:
  - `npm run compile`
  - `npm test -- test/desktopBridge.test.ts`; 11 tests passed.
  - `npm test`; 57 tests passed.
  - `git diff --check`
  - `find dist -name '*.js' -print0 | xargs -0 -n1 node --check`
  - `npm audit --omit=dev`; 0 vulnerabilities.
  - `npm run package:vsix`

### Next

- Install the updated Desktop build and Code Companion Voice `0.0.46`, then
  smoke test the pending-client approval flow.

## 2026-05-13 Short-Lived Session Authorization

### Current Milestone

- Milestone 6: Pairing Without Persistent VS Code Secrets.

### Changed

- Approved clients now receive an 8-hour in-memory session token from
  `/v1/client/hello`.
- `/v1/speech/candidates` accepts approved-client session tokens bound to the
  candidate `clientId`.
- Session tokens are rejected when used by a different client.
- The legacy bridge token remains accepted for speech candidates during
  migration.
- Added protocol fields `sessionToken` and `sessionExpiresAtUtc` to
  `ClientHelloResponse`.

### Decision

- `/speak` remains legacy-token only because it has no client identity payload
  to bind to a Desktop-approved client.

### Verified

- `dotnet build CodeCompanionDesktop.sln`
- `dotnet test CodeCompanionDesktop.sln --no-build`; 27 tests passed.
- `git diff --check`

### Next

- Migrate the Code Companion Voice extension to use Desktop pairing/session
  authorization instead of persistent bridge token storage.

## 2026-05-13 Client Pairing UI

### Current Milestone

- Milestone 6: Pairing Without Persistent VS Code Secrets.

### Changed

- Added a Client Pairing panel to the Desktop window.
- The panel displays observed bridge clients from `client-trust.json`.
- Added Refresh, Copy, Approve, and Deny actions for bridge client trust.
- Added formatted client trust diagnostics to `ClientTrustStore`.
- Added tests for client trust authorization changes and diagnostic formatting.

### Decision

- Approval/deny changes are Desktop-owned registry decisions. Speech candidate
  authorization still uses the compatibility token until the short-lived session
  authorization slice is implemented.

### Verified

- `dotnet build CodeCompanionDesktop.sln`
- `dotnet test CodeCompanionDesktop.sln --no-build`; 25 tests passed.
- `git diff --check`

### Next

- Add short-lived session authorization so trusted clients can speak without
  storing a long-lived token in VS Code.

## 2026-05-13 Client Trust Registry

### Current Milestone

- Milestone 6: Pairing Without Persistent VS Code Secrets.

### Changed

- Added `ClientTrustStore` for Desktop-owned bridge client trust state.
- Production Desktop now supplies the trust store to `LocalBridgeServer`.
- `/v1/client/hello` records unknown clients as `pending` in
  `%APPDATA%\CodeCompanionDesktop\client-trust.json`.
- Approved clients return `allowed` from `/v1/client/hello`.
- The bridge keeps compatibility-token behavior when no trust store is supplied,
  preserving the migration fallback.

### Decision

- This is the pairing registry baseline only. User approval UI and short-lived
  session authorization remain separate Milestone 6 slices.

### Verified

- `dotnet build CodeCompanionDesktop.sln`
- `dotnet test CodeCompanionDesktop.sln --no-build`; 23 tests passed.
- `git diff --check`

### Next

- Add Desktop UI for pending clients with approve and deny actions.

## 2026-05-13 Project Speech History

### Current Milestone

- Milestone 5: Project Identity.

### Changed

- Added structured project speech history records to `speech-history.json`.
- Added Desktop runtime grouping for recent speech candidate decisions by stable
  `projectId`.
- Added a Project Speech History panel with Refresh and Copy actions.
- Updated tests for persisted project speech records and grouped history output.
- Updated the architecture status to mark Milestone 5 complete.

### Decision

- Project speech history records store candidate decision metadata and a short
  preview, not provider secrets or full provider responses.

### Verified

- `dotnet build CodeCompanionDesktop.sln`
- `dotnet test CodeCompanionDesktop.sln --no-build`; 21 tests passed.
- `git diff --check`

### Next

- Start Milestone 6: Pairing Without Persistent VS Code Secrets.

## 2026-05-13 Project Registry Alias Management

### Current Milestone

- Milestone 5: Project Identity.

### Changed

- Added Project Registry controls for adding and removing root aliases by
  stable `projectId`.
- Added Desktop runtime methods to update aliases and refresh recent project
  diagnostics after registry edits.
- Added `ProjectRegistryStore` support for explicit root alias add/remove.
- Added test coverage for adding and removing an alias without splitting the
  project identity.

### Decision

- Alias management is scoped to observed root aliases for existing projects.
  Creating and merging project identities remains out of scope for this slice.

### Verified

- `dotnet build CodeCompanionDesktop.sln`
- `dotnet test CodeCompanionDesktop.sln --no-build`; 21 tests passed.
- `git diff --check`

### Next

- Add history views grouped by project ID to close Milestone 5 acceptance.

## 2026-05-13 Project Registry UI

### Current Milestone

- Milestone 5: Project Identity.

### Changed

- Added a dedicated Project Registry panel to the Desktop window.
- Added Refresh and Copy actions for project registry diagnostics.
- Added detailed project registry formatting with project ID, display name,
  environments, client names, first/last seen timestamps, and observed roots.
- Added coverage that registry details include merged Windows and WSL roots for
  the same stable project ID.

### Decision

- The Project Registry panel is read-only for this slice. Explicit alias
  editing remains a later Milestone 5 step.

### Verified

- `dotnet build CodeCompanionDesktop.sln`
- `dotnet test CodeCompanionDesktop.sln --no-build`; 20 tests passed.
- `git diff --check`

### Next

- Add explicit alias management and history views grouped by project ID.

## 2026-05-13 Spoken Update Convention

### Current Milestone

- Milestone 5: Project Identity.

### Changed

- Added `scripts/send-speech-candidate.ps1` for reliable spoken progress
  updates through the Desktop candidate inbox.
- Documented the spoken update convention in `AGENTS.md`, `README.md`, and
  `docs/architecture.md`.

### Decision

- Longer work should include occasional spoken progress updates when Code
  Companion Desktop is running.
- End-of-milestone summaries should be spoken before the final written summary.
- Spoken updates must use the Desktop-owned candidate path, not VS Code webview
  audio.

### Verified

- Ran `scripts/send-speech-candidate.ps1` and wrote a valid candidate file to
  `%APPDATA%\CodeCompanionDesktop\candidate-inbox`.
- `git diff --check`

### Next

- Use the helper script for future milestone summaries and longer-running work
  updates.

## 2026-05-13 Desktop Project Registry

### Current Milestone

- Milestone 5: Project Identity.

### Changed

- Added `ProjectRegistryStore` for Desktop-owned project identity persistence.
- Desktop now records observed project IDs, display names, roots, environments,
  client names, and last-seen times from bridge client hello and speech
  candidate payloads.
- Added recent project diagnostics to the Desktop diagnostics text.
- Documented `%APPDATA%\CodeCompanionDesktop\project-registry.json`.

### Decision

- The registry is keyed by stable `projectId`; Windows and WSL roots are stored
  as observed aliases for that project.

### Verified

- `dotnet build CodeCompanionDesktop.sln`
- `dotnet test CodeCompanionDesktop.sln --no-build`; 20 tests passed.
- `git diff --check`

### Next

- Add richer project registry UI and history views grouped by project ID.

## 2026-05-13 Project Identity Baseline

### Current Milestone

- Milestone 5: Project Identity.

### Changed

- Added `.code-companion/project.json` for Code Companion Desktop with stable
  project ID `codecompaniondesktop`.
- Documented the Desktop project identity in the README.
- Updated the architecture Milestone 5 status to record that project identity
  is now in progress.

### Decision

- Stable project IDs are the product boundary. Windows and WSL path strings are
  diagnostic aliases, not the long-term identity.

### Verified

- Parsed `.code-companion/project.json` as valid JSON.
- `git diff --check`

### Next

- Add Desktop-owned project registry and alias/history grouping.

## 2026-05-13 Public Release Deferred To Final Milestone

### Current Milestone

- Milestone 7: Local Packaging And First Run.

### Changed

- Updated `docs/architecture.md` so Milestone 7 covers local installer, local
  VSIX, and first-run verification only.
- Added Milestone 9 for public release packaging through GitHub Releases and the
  VS Code Marketplace.
- Updated README and release checklist references to point public release work
  at Milestone 9.

### Decision

- Public release packaging happens at the end, after project identity, desktop
  owned pairing, and legacy compatibility cleanup.

### Verified

- `git diff --check`

### Next

- Continue with Milestone 5: Project Identity, unless a packaging blocker needs
  to be resolved first.

## 2026-05-13 Desktop Version Metadata Alignment

### Current Milestone

- Milestone 7: Packaging And First Run.

### Changed

- Added explicit Desktop assembly, file, package, and informational version
  metadata to `CodeCompanionDesktop.csproj`.
- Added `-AppVersion` support to `scripts/publish-release.ps1`.
- Updated `scripts/build-installer.ps1` so the installer `AppVersion` also
  drives the .NET publish metadata.
- Updated release documentation now that Desktop version metadata exists.

### Verified

- `dotnet build CodeCompanionDesktop.sln` using the Windows .NET SDK path.
- `dotnet test CodeCompanionDesktop.sln --no-build`; 19 tests passed.
- `git diff --check`
- `scripts\build-installer.ps1 -AppVersion 0.1.0` rebuilt
  `artifacts\installer\CodeCompanionDesktopSetup-0.1.0.exe`.
- Ran the freshly published executable and confirmed
  `GET http://127.0.0.1:47321/health` reports `appVersion: 0.1.0`.
- Restarted the installed app from
  `%LOCALAPPDATA%\Programs\Code Companion Desktop\CodeCompanionDesktop.exe`.

### Next

- Continue Milestone 7 release publishing work.

## 2026-05-13 Paired Release Checklist

### Current Milestone

- Milestone 7: Packaging And First Run.

### Changed

- Added `docs/release-checklist.md` covering the paired Desktop and Voice
  release process.
- Linked the release checklist from the Desktop README and Milestone 7
  architecture status.

### Decision

- The Desktop repository owns the cross-component release checklist because it
  already owns the architecture source of truth.
- Voice release sessions should reference the shared checklist rather than
  duplicating release rules.

### Verified

- `git diff --check`

### Next

- Use the checklist to close version metadata gaps before public release work.

## 2026-05-13 Inno Setup Architecture Identifier Cleanup

### Current Milestone

- Milestone 7: Packaging And First Run.

### Changed

- Updated the Inno Setup installer script to use `x64compatible` instead of the
  deprecated `x64` architecture identifier.

### Verified

- Rebuilt with
  `powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'D:\Development\CodeCompanionDesktop\scripts\build-installer.ps1' -AppVersion 0.1.0`.
- Installer output was created at
  `artifacts\installer\CodeCompanionDesktopSetup-0.1.0.exe`.
- The deprecated `x64` architecture identifier warning no longer appeared.

### Next

- Continue Milestone 7 release publishing work.

## 2026-05-13 Local Fresh Install Smoke Test

### Current Milestone

- Milestone 7: Packaging And First Run.

### Verified

- Built `artifacts\installer\CodeCompanionDesktopSetup-0.1.0.exe` with
  `.\scripts\build-installer.ps1 -AppVersion 0.1.0` using a process-scoped
  PowerShell execution-policy bypass.
- Inno Setup completed successfully. It emitted a non-blocking warning that the
  `x64` architecture identifier is deprecated.
- Stopped the debug desktop app before installer testing.
- Installed and launched Code Companion Desktop from:
  `%LOCALAPPDATA%\Programs\Code Companion Desktop\CodeCompanionDesktop.exe`.
- Confirmed the installed app was running as process ID `17500`.
- Confirmed `GET http://127.0.0.1:47321/health` returned `status: ok`,
  `bridge: listening`, `version: 0.2.0`, and `protocolVersion: 1`.
- Confirmed the ElevenLabs key was saved in the desktop app.
- Copied the bridge token from Desktop, saved it in VS Code, opened a new
  VS Code window to avoid losing chat context, opened the Code Companion Voice
  panel, and confirmed the panel showed `Enable Voice`, `Mute`, and
  `Desktop Test`.
- Clicked `Desktop Test` and heard speech through Code Companion Desktop.
- Extension output showed
  `candidate spoken/manual-desktop-candidate-test at http://192.168.16.1:47321`.

### Decision

- The current local installer plus local VSIX path is verified for the basic
  fresh-install pairing and Desktop bridge speech test.

### Next

- Clean up the Inno Setup architecture warning by replacing deprecated `x64`
  usage with the current recommended architecture identifier.
- Continue Milestone 7 release work for GitHub Releases and Marketplace
  publication.

## 2026-05-13 Fresh Install Documentation Refresh

### Current Milestone

- Milestone 4A: Windows-Owned Candidate Ingress.

### Changed

- Updated the Desktop README install-for-daily-use flow to include the current
  VS Code thin-client panel verification and `Desktop Test` bridge check.

### Decision

- A fresh install is not considered paired until the extension panel shows the
  Desktop bridge thin-client controls and Desktop speaks a candidate test.

### Verified

- `git diff --check`

### Next

- Use the install checklist to validate the Windows app installer path and the
  VS Code extension install path from scratch.

## 2026-05-13 Milestone 7 Download Source Clarification

### Current Milestone

- Milestone 7: Packaging And First Run.

### Changed

- Expanded `docs/architecture.md` Milestone 7 with target production download
  sources and current development install artifacts.
- Updated the Desktop README distribution model to distinguish GitHub Releases
  and VS Code Marketplace targets from today's local installer and VSIX
  artifacts.

### Decision

- Production Desktop downloads should come from GitHub Releases.
- Production Voice extension installs should come from the VS Code Marketplace.
- Until those publishing steps are complete, clean-install testing uses local
  development artifacts.

### Verified

- `git diff --check`

### Next

- Validate the local installer and local VSIX flow, then add release publishing
  automation when ready.

## 2026-05-13 Voice Check-In Speech Hint Contract

### Current Milestone

- Milestone 4A: Windows-Owned Candidate Ingress.

### Changed

- Added optional `candidate.speechHint` to the Desktop speech candidate
  contract.
- Desktop speech candidate policy now accepts explicit speech requests with
  known hints such as `voice-check-in` even when the Codex phase is not `final`.
- Added contract tests proving `voice-check-in` commentary candidates speak and
  ordinary non-final candidates are still ignored.

### Decision

- Non-final Codex candidates remain silent by default.
- Explicit speech hints are the contract-level way for the thin client to carry
  user intent, such as a voice check-in request, to Desktop.

### Verified

- `dotnet build CodeCompanionDesktop.sln`
- `dotnet test CodeCompanionDesktop.sln --no-build`; 19 tests passed.
- `git diff --check`
- Desktop debug app restarted from
  `src\CodeCompanionDesktop\bin\Debug\net8.0-windows\CodeCompanionDesktop.exe`.
- User reloaded the WSL VS Code window, repeated the real voice check-in, and
  heard voice through Code Companion Desktop.

### Next

- Continue cleanup toward the final thin-client shape by removing or hiding
  legacy VS Code-owned provider/audio controls from the normal UX.

## 2026-05-13 Thin Client Normal Candidate Source

### Current Milestone

- Milestone 4A: Windows-Owned Candidate Ingress.

### Changed

- Updated `docs/architecture.md` to record the extension `0.0.40` thin-client
  behavior.
- The normal extension path now has a separate local Codex event source setting
  and keeps the old Codex log root as legacy compatibility only.
- Automatic assistant candidates now deliver to Code Companion Desktop only and
  no longer fall back to VS Code-owned provider calls or audio playback when
  Desktop delivery fails.
- Startup VS Code webview audio unlock is skipped when Desktop bridge mode is
  enabled.

### Decision

- The remaining JSONL detection is only a local event source until a direct
  Codex event API or managed session source exists. `\\wsl.localhost` remains
  out of the normal product path.

### Verified

- Voice extension automated checks and packaging were run in
  `/mnt/d/Development/CodeCompanionVoice`; see that repository session log for
  exact commands.
- User reloaded the Windows Voice project window and confirmed Output showed
  `[startup-unlock] skipped: desktop-bridge-enabled` and
  `[desktop-bridge] startup-health: reachable http://127.0.0.1:47321`.
- A real Codex chat test in the Voice window produced no extension output. Local
  inspection showed the Windows Codex session root had no recent
  `D:\Development\CodeCompanionVoice` session; the visible sessions were for
  `d:\Development\Projects\Game\Automata`.
- A controlled matching JSONL smoke session for
  `d:\Development\CodeCompanionVoice` was appended under the Windows Codex
  session root. The extension forwarded the assistant candidate to Desktop and
  Desktop history recorded `Candidate spoken (accepted)` and
  `Playback completed from bridge request`.
- The temporary smoke-test JSONL file was removed after verification.
- Desktop documentation-only change; `git diff --check`.

### Next

- Investigate why the Codex UI test in the Voice window did not create or append
  a matching Windows Codex session file for `D:\Development\CodeCompanionVoice`.

## 2026-05-13 UNC Log Root Architecture Correction

### Current Milestone

- Milestone 4A: Windows-Owned Candidate Ingress.

### Changed

- Updated `docs/architecture.md` to make `\\wsl.localhost` Codex log scraping a
  prototype discovery mechanism, not the product direction.
- Added Milestone 4A for replacing per-environment Codex log roots with a
  Windows-owned candidate ingress through Code Companion Desktop.

### Decision

- Do not deepen the normal speech path around
  `\\wsl.localhost\Ubuntu-24.04\home\davidj\.codex\sessions`.
- The extension should remain a thin client that sends structured events to the
  desktop bridge. It should not require direct filesystem access to another
  environment's Codex logs.

### Verified

- Documentation-only change; `git diff --check` pending.

### Next

- Design and implement Milestone 4A before continuing more UNC watcher work.

## 2026-05-13 Windows-Owned Candidate Inbox

### Current Milestone

- Milestone 4A: Windows-Owned Candidate Ingress.

### Changed

- Added public bridge contract DTOs and shared validation for client hello and
  speech candidate payloads.
- Extracted `SpeechCandidateProcessor` so HTTP bridge requests and inbox files
  share the same desktop-owned policy, dedupe, queueing, provider, playback,
  diagnostics, and history path.
- Added `SpeechCandidateInboxWatcher`, which watches:
  `%APPDATA%\CodeCompanionDesktop\candidate-inbox`.
- Valid `*.json` inbox files use the same payload shape as
  `POST /v1/speech/candidates`, are processed through the desktop speech
  pipeline, and are deleted after acceptance.
- Invalid or rejected inbox files are moved under `candidate-inbox\rejected`.
- The app starts the inbox watcher alongside the local HTTP bridge.
- Updated `README.md` and `docs/architecture.md` for Milestone 4A.

### Verified

- `dotnet build CodeCompanionDesktop.sln` using
  `C:\Program Files\dotnet\dotnet.exe`.
- `dotnet test CodeCompanionDesktop.sln --no-build` using
  `C:\Program Files\dotnet\dotnet.exe`; 17 tests passed.
- `git diff --check` pending.

### Next

- Update Code Companion Voice so the normal path writes explicit structured
  candidate events to Desktop instead of depending on `\\wsl.localhost` Codex
  log scraping.

## 2026-05-13 Extension Checkout Moved To Windows

### Current Milestone

- Milestone 4: Thin VS Code Client.

### Changed

- Moved the Code Companion Voice extension working copy from
  `/var/www/CodeCompanion` to `D:\Development\CodeCompanionVoice`.
- Preserved git history, branch `feature/desktop-bridge-client`, origin remote,
  the untracked workspace file, and the current `/v1/speech/candidates`
  Milestone 4 work-in-progress edit.
- Installed extension dependencies in the Windows checkout.
- Updated desktop repository docs and instructions to point future extension
  work at `/mnt/d/Development/CodeCompanionVoice`.

### Verified

- In `/mnt/d/Development/CodeCompanionVoice`: `npm install`.
- In `/mnt/d/Development/CodeCompanionVoice`: `npm run compile`.
- Extension relocation commit `57fb928` was pushed to
  `feature/desktop-bridge-client`.

### Next

- Continue Milestone 4 in `/mnt/d/Development/CodeCompanionVoice`.
- Finish forwarding structured candidates to desktop
  `/v1/speech/candidates`.

## 2026-05-13 Thin Client Candidate Forwarding

### Current Milestone

- Milestone 4: Thin VS Code Client.

### Changed

- In the extension repository, added typed `POST /v1/speech/candidates`
  support for the desktop bridge.
- Changed the normal Codex assistant candidate path to send structured raw
  candidate events to Code Companion Desktop before VS Code filtering, rewrite,
  provider calls, or playback.
- Added client identity, environment metadata, workspace identity, Codex session
  metadata, and candidate text to the bridge payload.
- Added `.code-companion/project.json` support when present, with workspace
  metadata fallback.
- Kept legacy VS Code provider/webview/desktop-audio paths as migration
  fallback until manual verification is complete.
- Packaged and installed `code-companion-voice-0.0.33.vsix`.

### Verified

- In `/mnt/d/Development/CodeCompanionVoice`: `npm run compile`.
- In `/mnt/d/Development/CodeCompanionVoice`: `npm test`; 51 tests passed.
- In `/mnt/d/Development/CodeCompanionVoice`:
  `find dist -name '*.js' -print0 | xargs -0 -n1 node --check`.
- In `/mnt/d/Development/CodeCompanionVoice`: `git diff --check`.
- In `/mnt/d/Development/CodeCompanionVoice`: `npm audit --omit=dev`; 0
  vulnerabilities.
- In `/mnt/d/Development/CodeCompanionVoice`: `npm run package:vsix`.
- Installed extension version `local.code-companion-voice@0.0.33`.
- Extension commit `95fd7f4` pushed to `feature/desktop-bridge-client`.

### Next

- Reload VS Code so the extension host loads `0.0.33`.
- Smoke-test a real Codex candidate and confirm Code Companion Desktop speech
  diagnostics show `/v1/speech/candidates` activity.
- Remove VS Code-owned provider key/audio/webview responsibilities after the
  structured candidate path is manually verified.

## 2026-05-13 Environment-Agnostic Speech Architecture

### Current Milestone

- Milestone 0: Architecture Baseline.

### Changed

- Added `docs/architecture.md` as the source of truth for the target
  environment-agnostic speech architecture.
- Defined Code Companion Desktop as the speech authority and Code Companion
  Voice as a thin VS Code client.
- Documented that the Windows app owns provider credentials, provider calls,
  speech policy, queueing, diagnostics, and native Windows playback.
- Documented that VS Code should forward structured speech candidates to the
  Windows app and should not own TTS playback, webview audio unlock, provider
  keys, or provider calls.
- Added a milestone plan from architecture baseline through bridge contract,
  desktop speech pipeline, diagnostics, thin client migration, project identity,
  pairing, packaging, and cleanup.
- Updated `README.md` to point to the architecture document.
- Updated `AGENTS.md` so future sessions must read `docs/architecture.md` and
  report the current architecture milestone at session start.

### Decisions

- Refactor the existing VS Code extension instead of starting from scratch.
- Preserve the extension repository history and move or clone it to a Windows
  checkout only after WSL-specific prototype responsibilities are removed.
- Treat project identity as a stable ID, not as a path comparison problem.
- Use the current copied bridge token only as a temporary compatibility
  mechanism while moving toward desktop-managed client pairing.

### Verified

- `git diff --check`.

### Next

- Implement Milestone 1: Desktop Bridge Contract.
- Add versioned bridge DTOs and endpoints in the Windows app while keeping
  `/speak` as a temporary compatibility endpoint.

## 2026-05-13 Desktop Bridge Contract Endpoints

### Current Milestone

- Milestone 1: Desktop Bridge Contract.

### Changed

- Expanded `GET /health` with bridge `version`, `protocolVersion`, and
  `appVersion` fields while preserving existing queue and speaking fields.
- Added `POST /v1/client/hello` for VS Code client/workspace metadata.
- Added `POST /v1/speech/candidates` for structured Codex speech candidate
  events.
- Kept existing `POST /speak` behavior as the compatibility speech endpoint.
- Added bridge runtime tracking for the last client hello and last speech
  candidate.
- Documented the versioned bridge endpoints in `README.md`.
- Updated `docs/architecture.md` milestone status.

### Decisions

- `POST /v1/client/hello` is unauthenticated for Milestone 1 and returns
  `authorization: "allowed"` with `mode: "compatibility-token"`.
- `POST /v1/speech/candidates` requires the existing bearer token until the
  desktop-managed pairing milestone replaces copied tokens.
- `POST /v1/speech/candidates` validates the contract but returns
  `decision: "ignored"` and `reason: "speech_pipeline_not_implemented"` until
  Milestone 2 moves policy, rewrite, queueing, provider calls, and playback into
  the desktop pipeline.

### Verified

- `dotnet build CodeCompanionDesktop.sln` using
  `C:\Program Files\dotnet\dotnet.exe`.
- Launched the debug build and verified `GET /health` returns version fields:
  `version`, `protocolVersion`, and `appVersion`.
- Verified `POST /v1/client/hello` returns:
  `{"status":"ok","authorization":"allowed","mode":"compatibility-token","bridgeVersion":"0.2.0","protocolVersion":1}`.
- Verified unauthenticated `POST /v1/speech/candidates` returns
  `401 Unauthorized`.
- Verified authenticated `POST /v1/speech/candidates` returns:
  `{"status":"accepted","decision":"ignored","reason":"speech_pipeline_not_implemented","queuePosition":0}`.

### Remaining Gap

- Milestone 1 automated validation/error-response tests are still pending. The
  repository does not yet have a test project.

### Next

- Add a focused bridge contract test project or move directly into Milestone 2
  with tests included there.

## 2026-05-13 Bridge Contract Test Project

### Current Milestone

- Milestone 1: Desktop Bridge Contract.

### Changed

- Added `tests/CodeCompanionDesktop.Tests` as an xUnit test project.
- Added the test project to `CodeCompanionDesktop.sln`.
- Made `LocalBridgeServer` accept an optional port and expose `LocalBaseUrl` so
  tests can run on an ephemeral port without colliding with the real bridge on
  `47321`.
- Added bridge contract integration tests for:
  - `GET /health` version and queue fields.
  - valid `POST /v1/client/hello`.
  - unsupported schema validation.
  - bearer-token enforcement for `POST /v1/speech/candidates`.
  - invalid speech candidate metadata.
  - valid speech candidate placeholder response.
- Updated `docs/architecture.md` to mark Milestone 1 complete.

### Verified

- `dotnet build CodeCompanionDesktop.sln` using
  `C:\Program Files\dotnet\dotnet.exe`.
- `dotnet test CodeCompanionDesktop.sln --no-build` using
  `C:\Program Files\dotnet\dotnet.exe`; 6 tests passed.
- `git diff --check`.

### Next

- Start Milestone 2: Desktop Speech Pipeline.
- Move speech policy, privacy filtering, rewrite, duplicate detection, queueing,
  provider calls, and playback behind `POST /v1/speech/candidates`.

## 2026-05-13 Desktop Speech Pipeline

### Current Milestone

- Milestone 2: Desktop Speech Pipeline.

### Changed

- Added `SpeechCandidatePipeline` for desktop-owned candidate policy.
- Added deterministic whitespace normalization and 1000-character speech
  rewriting before playback.
- Added privacy filtering for authorization headers, bearer tokens, API-key-like
  assignments, secret-like provider tokens, and email addresses.
- Added duplicate detection by Codex message ID and normalized speech text hash.
- Connected `POST /v1/speech/candidates` to the existing desktop speech queue
  and bridge playback path.
- Updated queued speech requests so candidate reservations can be released if
  queued playback fails.
- Changed bridge-triggered ElevenLabs playback to report provider/key failures
  back to the bridge instead of silently completing.
- Updated README bridge documentation for candidate decisions.

### Verified

- `dotnet build CodeCompanionDesktop.sln` using
  `C:\Program Files\dotnet\dotnet.exe`.
- `dotnet test CodeCompanionDesktop.sln --no-build` using
  `C:\Program Files\dotnet\dotnet.exe`; 10 tests passed.

### Remaining Gap

- None for the initial Milestone 2 desktop pipeline.

### Next

- Start Milestone 3: Desktop Configuration And Diagnostics.
- Add a desktop diagnostics view for last client, last speech candidate, last
  policy decision, provider/key state, and recent speech results.

### Follow-up Verification

- Live `POST /v1/speech/candidates` smoke test against the running debug app
  returned:
  `{"status":"accepted","decision":"spoken","reason":"accepted","queuePosition":0}`.
- The request used the bridge token from Windows Credential Manager and did not
  require any provider key in VS Code.

## 2026-05-13 Desktop Speech Diagnostics

### Current Milestone

- Milestone 3: Desktop Configuration And Diagnostics.

### Changed

- Added a Speech Diagnostics panel to the Windows app.
- Added a bridge diagnostics snapshot with:
  - speaking state,
  - queue state,
  - last bridge status,
  - last client,
  - last speech candidate,
  - last speech decision,
  - last provider error,
  - last playback error,
  - recent speech results.
- Added Refresh and Copy actions for speech diagnostics.
- Updated bridge/provider/playback paths so diagnostics are refreshed after
  candidate decisions and provider/playback outcomes.
- Added runtime-state test coverage for diagnostics snapshots.

### Verified

- `dotnet build CodeCompanionDesktop.sln` using
  `C:\Program Files\dotnet\dotnet.exe`.
- `dotnet test CodeCompanionDesktop.sln --no-build` using
  `C:\Program Files\dotnet\dotnet.exe`; 11 tests passed.

### Remaining Gap

- Provider selection and ElevenLabs voice/model configuration are still pending
  for Milestone 3.

### Next

- Add desktop configuration fields for provider, ElevenLabs voice ID, model ID,
  and output format.
- Move the hardcoded ElevenLabs voice/model constants behind app settings.

## 2026-05-13 Desktop Provider Configuration

### Current Milestone

- Milestone 3: Desktop Configuration And Diagnostics.

### Changed

- Added persisted desktop settings for speech provider, ElevenLabs voice ID,
  ElevenLabs model ID, and ElevenLabs output format.
- Added a Speech Provider UI section in the Windows app.
- Moved live ElevenLabs speech generation to use the configured desktop
  settings instead of hardcoded voice/model/output values.
- Added provider configuration details to the speech diagnostics output.
- Added settings normalization tests for provider defaults and trimming.
- Updated README and architecture notes for the desktop-owned provider
  settings.

### Verified

- `dotnet build CodeCompanionDesktop.sln` using
  `C:\Program Files\dotnet\dotnet.exe`.
- `dotnet test CodeCompanionDesktop.sln --no-build` using
  `C:\Program Files\dotnet\dotnet.exe`; 13 tests passed.

### Remaining Gap

- Milestone 3 can still be expanded with richer recent client/history lists,
  but the current Windows app now owns the provider key, queue settings, bridge
  diagnostics, and ElevenLabs voice/model/output configuration.

### Next

- Decide whether to close Milestone 3 with the current diagnostics/configuration
  surface or add recent client/history persistence before moving to Milestone 4.

## 2026-05-13 Desktop Recent History

### Current Milestone

- Milestone 3: Desktop Configuration And Diagnostics.

### Changed

- Added `SpeechHistoryStore` for desktop-owned recent bridge client and speech
  result history.
- Persisted diagnostics history to
  `%APPDATA%\CodeCompanionDesktop\speech-history.json`.
- Loaded persisted recent bridge clients and speech results into
  `BridgeRuntimeState` during app startup.
- Expanded speech diagnostics output with recent bridge clients.
- Added tests for history store round-tripping and runtime-state loading from
  persisted history.
- Updated README and architecture notes for persisted diagnostic history.

### Verified

- `dotnet build CodeCompanionDesktop.sln` using
  `C:\Program Files\dotnet\dotnet.exe`.
- `dotnet test CodeCompanionDesktop.sln --no-build` using
  `C:\Program Files\dotnet\dotnet.exe`; 15 tests passed.

### Decision

- Milestone 3 is complete for the current architecture. The Windows app now
  owns provider key storage, provider voice/model/output configuration, queue
  settings, bridge status, speech diagnostics, recent clients, and recent
  speech history.
- Broader provider selection remains deferred until a second real provider is
  introduced.

### Next

- Start Milestone 4: Thin VS Code Client.
- Move the extension toward forwarding structured candidates to
  `/v1/speech/candidates` and remove VS Code-owned TTS/audio responsibilities.

## 2026-05-13 MainWindow IDE Error Triage

### Changed

- Updated `CodeCompanionDesktop.sln` to use the SDK-style C# project type GUID
  for `CodeCompanionDesktop.csproj`.
- Normalized `CodeCompanionDesktop.sln` line endings to LF so future solution
  edits pass the repository's standard `git diff --check` workflow.
- Reproduced the desktop build after a VS Code restart and checked the
  `MainWindow.xaml.cs` diagnostics context.
- Stopped the running debug `CodeCompanionDesktop.exe` process because it was
  locking the debug output executable during build.

### Verified

- `dotnet build CodeCompanionDesktop.sln` using
  `C:\Program Files\dotnet\dotnet.exe` completed with `0 Error(s)`.
- Confirmed the XAML generated members used by `MainWindow.xaml.cs` exist in
  `src/CodeCompanionDesktop/obj/Debug/net8.0-windows/MainWindow.g.cs`.
- `dotnet test CodeCompanionDesktop.sln --no-build` using
  `C:\Program Files\dotnet\dotnet.exe`.
- `git diff --check`.

### Decision

- The reported `MainWindow.xaml.cs` errors are design-time IDE diagnostics, not
  compiler errors. Since the errors persisted in a normal Windows VS Code
  window, prefer forcing a clean C# Dev Kit solution reload after the solution
  GUID update.

### Next

- In Windows VS Code, reload the window or reopen
  `CodeCompanionDesktop.code-workspace` so C# Dev Kit reloads
  `CodeCompanionDesktop.sln`.
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
