# Code Companion Desktop Instructions

These instructions apply to the Code Companion Desktop repository:

```text
D:\Development\CodeCompanionDesktop
/mnt/d/Development/CodeCompanionDesktop
```

This is not the Retrospective app. Do not work in `/var/www/retrospective` for
this project unless the user explicitly asks to switch projects.

## Session Start

- Work in `/mnt/d/Development/CodeCompanionDesktop`.
- Read `README.md`, `docs/architecture.md`, and `docs/session-log.md` before
  planning or coding.
- Check the current git branch, working tree status, latest commits, and whether
  any Code Companion Desktop process is already running.
- Summarize the current state, current architecture milestone, last verified
  baseline, and recommended next steps before making substantial changes.

## Project Context

- This is a Windows WPF/.NET desktop companion app for Code Companion Voice.
- The desktop app owns native Windows audio, Windows Credential Manager storage,
  ElevenLabs provider calls, tray behavior, startup behavior, release publishing,
  and the local authenticated bridge.
- `docs/architecture.md` is the source of truth for the environment-agnostic
  speech architecture and milestone plan.
- The related VS Code extension now lives in
  `/mnt/d/Development/CodeCompanionVoice` and should only be edited when the
  user explicitly asks for extension work.
- Keep the desktop app and extension repositories separate unless the user
  explicitly asks to coordinate changes across both.

## Git Workflow

- For coding work, create or use an appropriate feature branch.
- Do not work directly on `main` unless the user explicitly asks.
- Do not overwrite or revert user changes unless explicitly requested.
- This GitHub repository is private. Use authenticated `gh` as the primary tool
  for GitHub repository, PR, issue, and metadata operations.
- The local `gh` session is expected to be authorized for this repo; verify with
  `gh auth status` when GitHub access is needed.
- After implementation, run relevant checks/tests, commit with a clear message,
  and push the branch.

## Build And Test

- Prefer running Windows/.NET commands from PowerShell in the Windows checkout
  when WPF or publish behavior matters.
- Standard checks for code changes:
  - `dotnet build CodeCompanionDesktop.sln`
  - `dotnet test CodeCompanionDesktop.sln --no-build`
  - `git diff --check`
- For docs-only changes, `git diff --check` is enough unless the change affects
  commands, build scripts, or release behavior.
- For release/publish changes, verify `scripts/publish-release.ps1` from
  PowerShell and confirm the published executable path.

## App Process Workflow

- You may stop and start Code Companion Desktop processes when needed for build
  or smoke testing.
- Before building, check for running `CodeCompanionDesktop.exe` or relevant
  `dotnet` processes to avoid locked output files.
- When working from WSL, do not rely on Linux `pgrep` or WSL
  `http://127.0.0.1:47321/health` to decide whether the Windows desktop app is
  running. Check the Windows process and bridge from PowerShell instead:
  `powershell.exe -NoProfile -Command 'Get-Process CodeCompanionDesktop -ErrorAction SilentlyContinue'`
  and
  `powershell.exe -NoProfile -Command 'Invoke-RestMethod -Uri "http://127.0.0.1:47321/health" -TimeoutSec 2'`.
- At the end of a development piece, report whether the app is running.

## Session Log

- Keep `docs/session-log.md` updated after meaningful work sessions.
- Log what changed, what was verified, important decisions, branch/commit
  context, and useful next steps.
- Treat git history as the technical source of truth and the session log as the
  human-readable project memory.

## Diagnose Before Modifying

Before adding any new file, extension, transport, or workaround in response to
a "Code Companion is not working" report:

1. Ask the user to run `Code Companion: Verify Install` from the command
   palette and paste the output channel contents.
2. Ask the user to run `Code Companion: Run Pipeline Self-Test` and report the
   result.
3. If both pass, the bug is not in the install or transport layer. Look at
   candidate selection, speech policy, or Desktop playback configuration.
4. If either fails, the failing check identifies the layer. Fix that layer.
   Do not add new extensions, new transports, new bridge variants, or new
   activation hacks.
5. Adding a new VS Code extension is a last resort and must be approved by the
   user in the chat before any code is written. The architecture has exactly
   three extensions: voice, bridge, pack. ADR 0001.

## Communication

- Keep updates concise and practical.
- Explain what you are doing and why while working.
- For longer work, send occasional spoken progress updates through Code
  Companion Desktop when the app is running.
- At the end of every major milestone slice, important decision, or blocking
  issue, send a short spoken summary through Code Companion Desktop when the app
  is running, then include the same point in the final text summary.
- Use `scripts/send-speech-candidate.ps1 -Text "..."` for spoken updates. This
  writes a valid Desktop candidate inbox file and avoids VS Code webview audio.
- When invoking the speech script from WSL, use PowerShell with the Windows path
  to the script, for example
  `powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'D:\Development\CodeCompanionDesktop\scripts\send-speech-candidate.ps1' -Text '...'`.
- At the end, report branch, commit hash, tests run, push status, and app
  process status.
- When giving the user manual steps to run, provide one step at a time.
