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
- The related VS Code extension remains in `/var/www/CodeCompanion` and should
  only be edited when the user explicitly asks for extension work.
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
- At the end of a development piece, report whether the app is running.

## Session Log

- Keep `docs/session-log.md` updated after meaningful work sessions.
- Log what changed, what was verified, important decisions, branch/commit
  context, and useful next steps.
- Treat git history as the technical source of truth and the session log as the
  human-readable project memory.

## Communication

- Keep updates concise and practical.
- Explain what you are doing and why while working.
- At the end, report branch, commit hash, tests run, push status, and app
  process status.
- When giving the user manual steps to run, provide one step at a time.
