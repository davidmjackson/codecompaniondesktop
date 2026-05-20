# Code Companion Environment-Agnostic Speech Architecture

## Purpose

Code Companion speech must work the same way whether the active project is a
Windows application, a WSL/Linux application, or a mixed repository opened from
either side of VS Code.

The target product has two installed components:

- Code Companion Desktop, a Windows app.
- Code Companion Voice, a thin VS Code extension client.

The Windows app is the speech authority. The VS Code extension observes Codex
activity and forwards structured speech events to the Windows app. The extension
must not own provider credentials, provider calls, audio playback, speech
queueing, or environment-specific speech behavior.

This document is the architecture source of truth for the environment-agnostic
speech work. At the start of every development session, read this document with
`README.md` and `docs/session-log.md`, then state the current milestone and
next step before implementation.

## Current Problem

The prototype grew from VS Code webview playback and later added the Windows
desktop bridge. That left responsibilities split across environments:

- WSL VS Code uses a WSL extension host under `~/.vscode-server`.
- Windows VS Code uses a Windows extension host under `%USERPROFILE%\.vscode`.
- Each extension host has separate settings, SecretStorage, logs, and filesystem
  path semantics.
- Codex session logs can record project roots as `/mnt/d/...`, `/var/www/...`,
  or `D:\...` depending on how the session was launched.
- The extension currently tries to infer project identity from paths.
- Webview audio unlock is still part of the extension because the original
  playback path used browser audio.

This is fragile. Speech should not depend on which extension host is active.

## Target Principle

VS Code is a client. Windows Desktop is the service.

The speech flow is:

```text
Codex activity
  -> Code Companion Voice thin client
  -> local authenticated bridge
  -> Code Companion Desktop
  -> provider request
  -> native Windows audio playback
```

The VS Code client may run in Windows or WSL. In both cases it forwards the
same structured event shape to the Windows app.

The target architecture must not depend on `\\wsl.localhost` paths, WSL home
directories, or per-environment Codex log root settings. Those paths are a
prototype discovery mechanism, not a product boundary. When the active work is
running in Linux/WSL, any speech event that needs Windows playback must cross
into Windows through the local desktop bridge or a Windows-owned inbox owned by
Code Companion Desktop.

Practical rule:

- VS Code may observe the current editor/workspace context.
- VS Code may send structured candidate events to Code Companion Desktop.
- VS Code should not require direct access to another environment's filesystem
  to make speech work.
- Code Companion Desktop owns the Windows-side ingress, policy, diagnostics,
  and history.

## Responsibility Boundaries

### Code Companion Desktop Owns

- Provider API keys and secrets.
- Windows Credential Manager storage.
- Provider selection.
- Voice, model, and provider settings.
- Speech policy decisions.
- Privacy filtering and rewrite.
- TTS provider calls.
- Queueing, cancellation, and retry policy.
- Native Windows audio playback.
- Speech history.
- Diagnostics.
- Bridge pairing and client trust state.
- Project identity mapping.
- Startup behavior and tray UX.
- Installer, update, and daily-use Windows app lifecycle.

### Code Companion Voice Extension Owns

- Detecting Codex activity in the current VS Code context.
- Parsing local Codex session events defensively.
- Building a structured speech candidate event.
- Sending that event to Code Companion Desktop.
- Showing minimal bridge status and setup commands.

The extension does not own:

- Provider API keys.
- Provider API calls.
- TTS generation.
- Audio playback.
- Webview audio unlock.
- Speech queueing.
- Long-term speech history.
- Provider diagnostics.

### Shared Contract Owns

- Bridge request and response schema.
- Error codes.
- Version negotiation.
- Project identity shape.
- Client identity shape.
- Compatibility rules.

The shared contract should start as documentation and duplicated TypeScript/C#
DTOs. Extract a shared package only after the protocol stabilizes.

## Audio Authorization Model

No VS Code audio unlock is needed in the target architecture.

The unlock button exists only because the prototype used VS Code webview audio.
Once the Windows app owns native playback, Windows user login/session provides
the user authority to play audio. The desktop app can expose its own mute,
volume, queue, and provider controls.

The VS Code extension should not create a webview for audio playback in the
target architecture.

## Secrets And Configuration Model

Provider keys and provider tokens must be stored only by Code Companion Desktop.

Storage:

```text
Provider API keys: Windows Credential Manager
Desktop settings: %APPDATA%\CodeCompanionDesktop
Speech history: %APPDATA%\CodeCompanionDesktop
VS Code client settings: minimal non-secret bridge location and status only
```

The extension should not store provider keys. The long-term goal is also to
remove manually copied bridge tokens from VS Code.

Bridge security still matters because WSL and local processes can reach the
desktop bridge. The target pairing model is:

1. The VS Code client discovers the desktop bridge.
2. The client sends a non-secret client identity and project identity.
3. Code Companion Desktop prompts the user to allow or deny that client.
4. Code Companion Desktop stores the allow-list decision.
5. The desktop app issues short-lived in-memory session authorization for that
   client connection.

This keeps durable trust decisions in the Windows app instead of making every
VS Code environment a credential store. During migration, the existing copied
bridge token can remain as a temporary compatibility mechanism.

## Project Identity

Path comparison is not a stable product boundary. Windows and WSL can represent
the same project with different paths.

Every project should have a stable project identity. Preferred source:

```text
.code-companion/project.json
```

Example:

```json
{
  "schemaVersion": 1,
  "projectId": "codecompaniondesktop",
  "displayName": "Code Companion Desktop"
}
```

Rules:

- `projectId` is stable across Windows, WSL, and future machines.
- If no file exists, the extension may derive a temporary project identity from
  workspace metadata and send both the derived ID and observed roots.
- Code Companion Desktop owns the project registry and can merge aliases.
- Bridge requests include the project ID, observed project roots, and
  environment name for diagnostics.

## Bridge Protocol

Current bridge endpoints:

```text
GET  /health
POST /v1/client/hello
POST /v1/speech/candidates
```

### Health

`GET /health` returns service status without requiring provider credentials:

```json
{
  "status": "ok",
  "bridge": "listening",
  "version": "0.2.0",
  "speaking": false,
  "queued": 0,
  "queueLimit": 3
}
```

### Client Hello

`POST /v1/client/hello` announces a VS Code client:

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
    "roots": [
      "D:\\Development\\CodeCompanionDesktop"
    ]
  }
}
```

The desktop app responds with allowed, pending, or denied.

### Speech Candidate

`POST /v1/speech/candidates` sends observed text to the desktop app:

```json
{
  "schemaVersion": 1,
  "client": {
    "clientId": "generated-non-secret-id",
    "host": "wsl",
    "environment": "wsl:Ubuntu-24.04"
  },
  "workspace": {
    "projectId": "codecompaniondesktop",
    "displayName": "Code Companion Desktop",
    "roots": [
      "/mnt/d/Development/CodeCompanionDesktop"
    ]
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

The desktop app decides whether the candidate is spoken.

Example response:

```json
{
  "status": "accepted",
  "decision": "queued",
  "reason": "milestone",
  "queuePosition": 1
}
```

Possible decisions:

- `queued`
- `spoken`
- `ignored`
- `duplicate`
- `rejected`
- `unauthorized`

## Diagnostics

Diagnostics must be desktop-centered. The Windows app should show:

- Bridge listening address.
- Last client seen.
- Client authorization state.
- Current project ID.
- Last speech candidate.
- Last policy decision.
- Queue length.
- Provider key status.
- Last provider error.
- Last playback error.
- Recent speech history.

The VS Code extension can show a minimal status, but the source of truth is the
desktop app.

## Repository Strategy

The existing VS Code extension should be refactored, not rewritten from
scratch.

Reasons:

- It already has Codex JSONL parsing.
- It already has session watching behavior.
- It already has speech policy tests that can be repurposed or moved.
- It already has packaging scripts.
- The target thin client can be reached by deleting responsibilities, not by
  rebuilding all extension mechanics.

The extension repository now lives in the Windows checkout:

```text
D:\Development\CodeCompanionVoice
/mnt/d/Development/CodeCompanionVoice
```

Recommendation:

1. Keep the current repository and git history.
2. Refactor the extension into a thin client on its current branch.
3. Preserve git history. Do not start a new repository unless the existing
   repository becomes unrecoverable.

The extension can still run in WSL or Windows. Moving the checkout to Windows is
a developer workflow choice, not a runtime dependency.

## Architecture Decision Records

Architecture Decision Records capture significant, hard-to-reverse design
choices and the reasoning behind them. They are append-only: when a later
decision changes course, add a new record and mark the old one superseded
rather than rewriting an existing record.

### ADR 0001: Environment-Agnostic Speech Transport

Status: Accepted (2026-05-20).

#### Context

Speech must work identically whether the active project runs in Windows,
WSL, a Dev Container, or an SSH remote. Two prototype transports carry a
speech candidate from where the work happens into Code Companion Desktop:

- The authenticated HTTP bridge (`POST /v1/speech/candidates`).
- The Windows-owned candidate inbox directory.

Both assume the sender can reach the Windows loopback bridge or write
files into a Windows-owned directory. Neither assumption holds from WSL:

- WSL2 runs in a separate network namespace. From inside WSL, `127.0.0.1`
  is WSL's own loopback, not Windows'. Reaching the Windows bridge needs
  host-IP discovery or WSL mirrored networking — fragile or
  environment-dependent.
- Driving speech from assistant hooks (Codex or Claude Code) requires
  registering a hook in that environment's home directory, for example
  `~/.claude/settings.json`. That is per-environment configuration the
  product would have to plant and maintain.

A field incident made this concrete: a Claude Code review run inside WSL
produced no speech. The Stop and UserPromptSubmit hooks that enforce
spoken updates exist only in the Windows `~/.claude/`, and even when
invoked from WSL they cannot reach the Windows bridge on `127.0.0.1`.

An earlier prototype relayed speech through a VS Code webview, because a
webview always renders on the local Windows side. That was abandoned: a
webview only runs while its panel is open, so the transport dies whenever
the user closes the panel.

Product constraint: a market build must not require the app to plant
configuration or state files into each environment in order to function.
That rules out per-environment hook config, per-environment inbox
directories, and `\\wsl.localhost` filesystem scraping as normal product
paths.

#### Decision

Adopt the VS Code extension-host topology as the environment-agnostic
transport. VS Code is the only component that already spans both the work
environment and the local Windows machine, and it carries data across
that boundary as a built-in capability. The webview was never the bridge;
it was merely code that happened to run on the Windows side. A `ui`-kind
extension host provides that same Windows-resident code as a persistent
process, with no panel lifecycle.

Ship Code Companion Voice as a VS Code Extension Pack of two thin
extensions:

- `code-companion-voice` — `extensionKind: ["workspace"]`. Runs in the
  work environment (WSL, container, SSH host, or Windows). Observes the
  assistant locally and builds the structured speech candidate.
- `code-companion-bridge` — `extensionKind: ["ui"]`. Always runs on the
  local Windows machine. The sole component that talks to the Code
  Companion Desktop bridge: `POST /v1/client/hello` and
  `POST /v1/speech/candidates`.

The workspace extension forwards each candidate to the UI extension
through the VS Code command registry, which is shared across extension
hosts:

```text
const decision = await vscode.commands.executeCommand(
  'codeCompanion.deliverCandidate', candidate);
```

VS Code marshals the JSON arguments across the host boundary. The UI
extension delivers to the Desktop bridge over Windows loopback. When the
workspace is plain Windows, both extensions run locally and the same code
path applies; only VS Code's transport underneath differs, transparently.

#### Consequences

Positive:

- The UI extension is a persistent extension-host process, not a webview.
  There is no "panel must be open" dependency.
- One mechanism covers WSL, Dev Containers, and SSH remotes, because the
  UI host is always local.
- No app-authored files in any work environment. VS Code's own extension
  manager installs the workspace extension into the remote host — the
  standard, user-consented mechanism every Remote-capable extension uses.
- The Desktop bridge no longer needs to be reachable off-box. It can bind
  loopback-only and drop the `IPAddress.Any` listener — a security
  improvement.
- The candidate inbox and per-environment assistant hooks are no longer
  required for the normal speech path and can be retired.

Negative, and limits:

- Speech outside VS Code is out of scope. Observing an assistant in a bare
  terminal still requires hook config in that environment, which the
  product constraint forbids. The supported product boundary is: speech
  works through the Code Companion Voice extension, in any environment VS
  Code supports.
- Two extension packages instead of one. Mitigated by shipping them as a
  single Extension Pack, so the user installs one Marketplace item.
- The UI extension must be activated for its command to exist. Its
  activation events must cover the relay command or use
  `onStartupFinished`.

#### Alternatives considered

- Single `workspace` extension using WSL interop: the extension spawns
  `powershell.exe` through WSL interop so the HTTP call runs as a Windows
  process. Rejected as the product path — WSL-only, since there is no
  `powershell.exe` in a Dev Container or SSH Linux host, and it spawns a
  process per candidate.
- WSL mirrored networking (`networkingMode=mirrored` in `.wslconfig`) so
  `127.0.0.1` resolves bidirectionally. Rejected as the primary mechanism:
  it requires Windows 11 22H2 or later plus a host configuration
  prerequisite, and it does not address observation outside VS Code.
  Acceptable only as an optional power-user convenience.
- Desktop reads WSL session logs via `\\wsl.localhost`. Rejected: it
  reintroduces distro and path discovery, and file-watching over the 9p
  mount is unreliable — the exact fragility this document exists to
  remove.
- VS Code webview relay. Rejected: the webview runs only while its panel
  is open, so the transport is not durable.

#### Supersedes

- The HTTP bridge and candidate inbox as cross-environment transports
  (Milestone 4A). Both remain valid as local Windows-side ingress and as
  developer tools; they are no longer the normal cross-environment path.
- The per-environment assistant hook approach for driving spoken updates.

Implementation against the Code Companion Voice repository is tracked as
follow-up work and is not yet scheduled into a milestone.

## Milestone Plan

### Milestone 0: Architecture Baseline

Goal:

- Establish this document as the architecture source of truth.
- Update session-start instructions to require reading this document.

Acceptance criteria:

- `docs/architecture.md` exists and is linked from `README.md`.
- `AGENTS.md` says to read `docs/architecture.md` at session start.
- `docs/session-log.md` records the architecture decision.
- Future sessions report current milestone status from this document.

Status:

- Complete in commit `28504ce`.

### Milestone 1: Desktop Bridge Contract

Goal:

- Define versioned bridge DTOs and endpoint behavior in the desktop app.

Scope:

- Add C# request/response types for client hello and speech candidates.
- Keep `/speak` as a temporary compatibility endpoint.
- Add protocol version to `/health`.
- Add structured error responses.

Acceptance criteria:

- `GET /health` reports bridge version and queue state.
- `POST /v1/client/hello` accepts client metadata and returns authorization
  state.
- `POST /v1/speech/candidates` validates payload shape and returns a policy
  placeholder decision.
- Unit tests cover validation and error responses.

Status:

- Complete in commit `d417206` plus follow-up bridge contract test coverage.

### Milestone 2: Desktop Speech Pipeline

Goal:

- Move speech decision-making and provider calls into Code Companion Desktop.

Scope:

- Port or reimplement privacy filtering in C#.
- Port or reimplement deterministic speech rewriting in C#.
- Add duplicate detection by message ID and normalized text hash.
- Process speech candidates through the existing ElevenLabs client.
- Queue speech inside the desktop app.

Acceptance criteria:

- A valid speech candidate can be accepted, rewritten, queued, generated, and
  played by the Windows app.
- Duplicate candidates are ignored.
- Provider keys remain in Windows Credential Manager.
- No provider key is needed in VS Code.

Status:

- Complete in commit `cbad66d` plus follow-up live smoke verification.
- Automated tests cover accepted/spoken, queued, duplicate, and privacy-filtered
  candidate decisions.
- Live `/v1/speech/candidates` smoke test returned `decision: "spoken"` through
  the desktop bridge and ElevenLabs playback path.

### Milestone 3: Desktop Configuration And Diagnostics

Goal:

- Make the Windows app the complete configuration and diagnostics surface.

Scope:

- Provider selection.
- Voice/model settings.
- Provider key status and save/clear.
- Queue settings.
- Bridge status.
- Recent client list.
- Recent speech history.
- Last ignored/rejected reason.

Acceptance criteria:

- A user can configure speech fully in the Windows app.
- A user can troubleshoot why a candidate did or did not speak from the Windows
  app.
- VS Code webview audio controls are no longer needed for normal operation.

Status:

- Complete.
- Initial desktop speech diagnostics panel added for bridge state, provider key
  state, last client, last speech candidate, last policy decision, provider and
  playback errors, and recent speech results.
- Initial provider configuration added for ElevenLabs voice ID, model ID, and
  output format.
- Persisted recent bridge client and speech result history added under
  `%APPDATA%\CodeCompanionDesktop`.
- Broader provider selection is deferred until a second provider exists.

### Milestone 4: Thin VS Code Client

Goal:

- Reduce Code Companion Voice to event observation and forwarding.

Scope:

- Keep Codex session watching.
- Keep command palette commands for status and bridge setup.
- Remove provider key storage.
- Remove TTS provider calls.
- Remove webview audio playback and unlock.
- Remove desktop-audio fallback playback.
- Forward structured candidates to `/v1/speech/candidates`.

Acceptance criteria:

- The extension sends candidates to the desktop app from Windows VS Code.
- The extension sends candidates to the desktop app from WSL VS Code.
- No provider key or provider token is stored in VS Code.
- No audio unlock is required in VS Code.

Status:

- Desktop ingress implemented; extension migration still in progress.
- Extension commit `95fd7f4` sends structured Codex assistant candidates to
  desktop `POST /v1/speech/candidates` before VS Code filtering, rewrite,
  provider calls, or playback.
- VS Code-owned provider/webview/desktop-audio paths still exist as migration
  fallback and need removal after manual verification.
- Manual verification exposed that using `\\wsl.localhost` as a Codex log root
  from a normal Windows VS Code extension host is brittle and violates the
  target environment-agnostic boundary. Do not build more product behavior on
  UNC log scraping.
- Desktop commit pending for Milestone 4A adds a Windows-owned
  `%APPDATA%\CodeCompanionDesktop\candidate-inbox` watched folder. Inbox files
  use the same speech candidate contract as `POST /v1/speech/candidates` and
  flow through the same desktop policy/provider/audio pipeline.

### Milestone 4A: Windows-Owned Candidate Ingress

Goal:

- Remove `\\wsl.localhost` and per-environment Codex log roots from the normal
  speech path.

Scope:

- Add a Windows-owned candidate ingress in Code Companion Desktop. This can be
  the existing authenticated HTTP bridge, a desktop-owned inbox under
  `%APPDATA%\CodeCompanionDesktop`, or both during migration.
- Make Code Companion Voice send explicit structured events to Desktop instead
  of asking Windows VS Code to enumerate WSL Codex session logs.
- Keep Codex log scraping only as a temporary developer fallback until a direct
  event source exists.
- Move all user-facing diagnostics for candidate receipt and policy decisions
  into the Windows app.

Acceptance criteria:

- Code Companion Desktop creates and watches
  `%APPDATA%\CodeCompanionDesktop\candidate-inbox`.
- Candidate inbox files use the same structured payload shape as
  `POST /v1/speech/candidates`.
- Inbox candidates flow through the same desktop policy, dedupe, queueing,
  provider, playback, diagnostics, and history path as HTTP candidates.
- A Windows project can produce speech without configuring a Codex log root in
  VS Code once the extension writes explicit candidate events.
- A WSL/Linux project can produce speech without configuring
  `\\wsl.localhost\...` in VS Code once the extension writes explicit candidate
  events.
- VS Code stores no provider keys and does not read another environment's
  Codex log directory for normal operation.
- Code Companion Desktop diagnostics show received candidates with project ID,
  client ID, environment, decision, and reason.

Status:

- Desktop side complete for Milestone 4A. The candidate inbox exists, is
  covered by focused tests, and feeds the same desktop policy/provider/audio
  pipeline as `POST /v1/speech/candidates`.
- Extension `0.0.39` manually verified the Desktop candidate path with
  `Code Companion Voice: Send Desktop Candidate Test`.
- Extension `0.0.40` separates the normal local Codex event source from the
  legacy manual Codex log root, skips startup webview audio unlock in Desktop
  bridge mode, and prevents automatic candidates from falling back to
  VS Code-owned provider calls or audio playback when Desktop delivery fails.
- Remaining migration gap: normal candidate detection still tails a local Codex
  JSONL event source until a direct Codex event API or managed session source is
  available. It must remain local to the active VS Code extension host and must
  not use `\\wsl.localhost` as a normal product path.
- Superseded as the cross-environment transport by ADR 0001. The HTTP
  bridge and candidate inbox remain valid local Windows ingress; the
  normal cross-environment path is now the VS Code extension pack.

### Milestone 5: Project Identity

Goal:

- Stop relying on path equivalence as the product identity boundary.

Scope:

- Add `.code-companion/project.json` support.
- Add derived project identity fallback.
- Send project identity in every bridge request.
- Store project registry and aliases in the desktop app.

Acceptance criteria:

- Windows and WSL views of the same project share one project ID.
- Speech history can be grouped by project ID.
- The desktop app can display observed roots and aliases for troubleshooting.

Status:

- Complete.
- Code Companion Desktop now has `.code-companion/project.json` with project ID
  `codecompaniondesktop`.
- Code Companion Voice already reads `.code-companion/project.json` when present
  and sends that identity in Desktop bridge speech candidate payloads.
- Desktop now persists observed project identities and path aliases in
  `%APPDATA%\CodeCompanionDesktop\project-registry.json`.
- Desktop diagnostics show recent observed projects and roots, and the main
  window has a Project Registry panel with Refresh, Copy, Add Alias, and Remove
  Alias actions.
- Desktop stores structured speech history records by project ID and the main
  window has a Project Speech History panel grouped by project.

### Milestone 6: Pairing Without Persistent VS Code Secrets

Goal:

- Remove copied long-lived bridge token storage from VS Code.

Scope:

- Add user-approved client pairing in the desktop app.
- Add allowed-client registry in the desktop app.
- Use short-lived in-memory bridge authorization for active extension sessions.
- Remove copied bridge-token setup after migration completes.

Acceptance criteria:

- A fresh Windows or WSL VS Code client can request pairing.
- The Windows app prompts the user to approve or deny.
- Durable trust lives in the Windows app.
- VS Code stores no long-lived secret token.

Status:

- Complete.
- Desktop now stores bridge client trust state in
  `%APPDATA%\CodeCompanionDesktop\client-trust.json`.
- `/v1/client/hello` records unknown clients as `pending` when Desktop-owned
  pairing is enabled.
- Previously approved clients return `allowed` from `/v1/client/hello`.
- The main window has a Client Pairing panel with Refresh, Copy, Approve, and
  Deny actions for observed bridge clients.
- Approved clients receive short-lived in-memory session authorization from
  `/v1/client/hello`.
- `/v1/speech/candidates` accepts short-lived session authorization.
- Code Companion Voice `0.0.50` uses Desktop pairing/session authorization for
  normal candidate delivery and no longer stores long-lived bridge tokens.

### Milestone 7: Local Packaging And First Run

Goal:

- Make installation and first-run behavior predictable.
- Make the local installer, local VSIX, and fresh-install verification path
  explicit.

Scope:

- Windows app installer starts or offers to start the bridge.
- VS Code extension detects whether the desktop app is reachable.
- VS Code extension shows only setup guidance and bridge status.
- README documents the two-install flow.
- README documents current development install sources.
- Fresh-install verification is documented as a required local packaging check.

Local install model:

- Current development source for Code Companion Desktop:
  - Local installer artifact created by
    `.\scripts\build-installer.ps1 -AppVersion <version>`.
  - Local portable publish artifact created by `.\scripts\publish-release.ps1`.
- Current development source for Code Companion Voice:
  - Local VSIX artifact created by `npm run package:vsix`.
  - Installed into the Windows VS Code profile with Windows `code.cmd`.
  - Installed into the WSL VS Code server with WSL `code` when testing WSL
    workspaces.
- Public GitHub Release and VS Code Marketplace publication are explicitly
  deferred to Milestone 9.

Acceptance criteria:

- Install Windows app.
- Install VS Code extension.
- Configure provider in Windows app.
- Pair the extension with the desktop app through Desktop-owned client approval.
- Reload VS Code.
- Open the Code Companion Voice panel.
- Confirm the normal Desktop bridge panel shows `Desktop Test` and bridge
  diagnostics only.
- Click `Desktop Test`.
- Confirm Code Companion Desktop speaks and the extension output includes
  `candidate spoken`.
- Speech works from Windows and WSL projects without provider keys, provider
  calls, or audio unlock in VS Code.

Status:

- Complete for the local development install path.
- Fresh-install verification is documented in both repository READMEs.
- Local installer plus local VSIX fresh-install smoke testing passed.
- Public release publication is deferred to Milestone 9.

### Milestone 8: Cleanup And Compatibility Removal

Goal:

- Remove obsolete prototype paths.

Scope:

- Remove VS Code webview audio unlock.
- Remove normal VS Code panel audio controls such as `Enable Voice` and `Mute`;
  the Windows desktop app owns voice enablement, mute, playback, queueing, and
  provider state.
- Keep `Desktop Test` in the VS Code panel as a bridge diagnostic until there
  is an equivalent first-run diagnostic path in Desktop.
- Remove VS Code provider key commands.
- Remove VS Code provider calls.
- Remove VS Code desktop-audio playback fallback.
- Remove `/speak` compatibility after consumers migrate.

Acceptance criteria:

- The only normal speech path is VS Code candidate forwarding to the Windows
  desktop app.
- The VS Code panel presents bridge status, pairing state, project identity,
  last decision diagnostics, and `Desktop Test`, not audio ownership controls.
- Tests reflect the new responsibility boundaries.

Status:

- Complete.
- Desktop setup documentation now uses Client Pairing as the normal path.
- Desktop token UI and tray token copy actions have been removed.
- Documentation no longer describes VS Code-owned TTS.
- Code Companion Voice `0.0.50` removes VS Code-owned webview playback,
  provider calls, provider/key settings, desktop-audio fallback, speech queue,
  `/speak` client compatibility, and long-lived bridge token storage.
- Code Companion Voice now uses Desktop pairing with short-lived session
  authorization for HTTP candidate delivery.
- Candidate inbox fallback remains because it is a Desktop-owned ingress path.
- Code Companion Desktop no longer accepts `POST /speak`.
- Code Companion Desktop no longer accepts legacy bridge-token authorization on
  `/v1/speech/candidates`.
- Code Companion Desktop no longer creates or stores `CodeCompanionDesktop/BridgeToken`.
- ADR 0001 supersedes the candidate-inbox fallback and per-environment
  hook approaches as cross-environment speech transports.

### Milestone 9: Public Release Packaging

Goal:

- Publish the finished product through public distribution channels only after
  the architecture cleanup is complete.

Scope:

- Build the final Code Companion Desktop installer.
- Generate checksum and release notes.
- Publish Code Companion Desktop through GitHub Releases.
- Prepare Code Companion Voice Marketplace metadata.
- Remove development-only extension package metadata.
- Improve Desktop Client Pairing UX so a pending client can be approved without
  manually copying its client ID.
- Publish Code Companion Voice through the VS Code Marketplace.
- Run fresh-install verification from the published sources, not local
  development artifacts.

Download model:

- Code Companion Desktop:
  - GitHub Releases for the Code Companion Desktop repository.
  - Primary asset: signed or checksummed Windows installer,
    `CodeCompanionDesktopSetup-<version>.exe`.
- Code Companion Voice:
  - VS Code Marketplace.
  - The same extension must be installable in both Windows and WSL extension
    hosts when a workspace requires the WSL host.

Acceptance criteria:

- Desktop installer is attached to a GitHub Release with release notes and a
  SHA256 checksum.
- Voice extension is published under the real Marketplace publisher.
- Fresh install from the GitHub Release and Marketplace passes in a normal
  Windows VS Code window.
- Fresh install from the GitHub Release and Marketplace passes in a WSL Remote
  VS Code window.
- No release instructions require local repository artifacts.

Status:

- In progress.
- Resumed after accepting the first Milestone 10 Desktop UX slice.
- `docs/release-checklist.md` documents the release gates for this milestone.
- `scripts/build-release-package.ps1` builds the Desktop installer, SHA256
  checksum, and draft GitHub release notes from one command.
- `scripts/draft-github-release.ps1` validates the generated artifacts and
  previews or creates the draft Desktop GitHub Release.

### Milestone 10: Desktop UX And Onboarding Refresh

Goal:

- Make Code Companion Desktop easier for normal users to read, configure, and
  troubleshoot without exposing development diagnostics as the primary
  experience.

Scope:

- Rework the main window around clear `Status`, `Settings`, `Advanced`, and
  `Notes` areas.
- Add a top-level readiness summary with healthy and unhealthy states.
- Use Settings accordions for normal user configuration.
- Move development and diagnostic details into Advanced.
- Add plain-language explanatory text for every visible section.
- Add an in-app Notes surface for install, setup, pairing, testing, and control
  explanations.
- Clarify current-session diagnostics versus persisted project speech history.
- Improve provider key status wording so saved/working keys read as healthy.
- Keep the window compact and prevent full-screen/maximized presentation.

Reference:

- Detailed notes are maintained in `docs/desktop-ux-refresh.md`.
- Demo Mode planning and test coverage notes are maintained in
  `docs/demo-mode.md`.

Status:

- First slice accepted.
- Pulled forward before completing Milestone 9 because the Desktop app needs a
  clearer first-run/status experience before release.
- Status, Advanced, and Notes tabs passed manual review.

Implemented slice:

- Add Demo Mode as a temporary Desktop-owned speech profile for live
  demonstrations. The user can enter `Demo Mode` to make Codex speak more often
  during the current session and `end demo` to restore the standard speech
  policy. The mode must reset when the Desktop app closes and must preserve
  pairing, privacy filtering, mute, queueing, provider settings, and normal
  authorization behavior.
- First implementation is complete on the Desktop side: profile commands flow
  through the existing speech candidate pipeline, health and diagnostics expose
  the active profile, the Status tab shows Demo Mode state, and focused tests
  cover command handling, bridge behavior, inbox behavior, diagnostics, and
  session-only reset.

### Milestone 11: Environment-Agnostic Speech Delivery

Goal:

- Implement ADR 0001. Deliver TTS for every project, in any environment VS
  Code supports, with no per-environment files and no webview.

Scope:

- Restructure Code Companion Voice into a VS Code Extension Pack: a `ui`-kind
  bridge extension that is Windows-resident, and a `workspace`-kind observer
  extension that runs in the work environment.
- Relay speech candidates from the observer to the bridge extension through
  the VS Code cross-host command registry.
- Observe both Codex and Claude Code session activity locally in the workspace
  extension host.
- Fix active-session selection so the observed session follows the active
  project rather than path-matching.
- On activation, health-check the Desktop bridge and show a native VS Code
  alert with fix instructions when Desktop is unreachable.
- Remove the remaining webview panel; present status through the status bar,
  commands, and notifications.
- Retire the candidate inbox and per-environment hooks as product transports.

Acceptance criteria:

- Installing the extension pack and Desktop, then opening any project on
  Windows or WSL Remote, produces TTS with no per-project setup.
- Both Claude Code and Codex sessions produce speech.
- When Desktop is unreachable, VS Code shows an actionable alert.
- The extension contains no webview.
- The product writes no files into the work environment.

Status:

- Implementation complete on branch `feature/extension-pack-split` of the
  Code Companion Voice repository. Commits cover the two-extension pack
  restructure, the cross-host relay, the Codex and Claude Code observation
  layer, the connectivity alert, the pairing UX, the transport-code
  cleanup, and local VSIX packaging.
- Compiles clean; automated unit tests pass.
- Runtime fresh-install verification on Windows VS Code and a WSL Remote
  window - both assistants, plus the Desktop-unreachable alert path - is
  the remaining step before the milestone is closed.
- Supersedes the cross-environment transport described in Milestones 4A
  and 8.

Reference:

- ADR 0001: Environment-Agnostic Speech Transport.

## Session Checklist

At the start of each session:

1. Read `README.md`.
2. Read this architecture document.
3. Read `docs/session-log.md`.
4. Check branch, status, latest commits, and running app process.
   - From WSL, check the Windows process and bridge with PowerShell. Do not use
     Linux `pgrep` or WSL `localhost` as the source of truth for the Windows
     Desktop app.
5. State the current milestone from this document.
6. State the last verified baseline from the session log.
7. State the recommended next step.

At the end of each session:

1. Update `docs/session-log.md`.
2. Record milestone progress.
3. Record branch, commit, tests, push status, and app process status.

## Spoken Update Convention

For long-running development work, use Code Companion Desktop for short spoken
progress updates when the app is running. At the end of each milestone slice,
send a concise spoken summary before the final written summary.

Use:

```powershell
.\scripts\send-speech-candidate.ps1 -Text "Milestone summary text."
```

The script writes an `assistant-message` candidate with `speechHint:
manual-speak-last` into the Desktop candidate inbox. This keeps spoken updates
on the Desktop-owned audio path and does not depend on VS Code webview audio.

When Codex is running from WSL, invoke the script through PowerShell with the
Windows path. This avoids false negatives where WSL cannot see the Windows
Desktop process or `127.0.0.1` bridge even though Desktop is running and
speaking.

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'D:\Development\CodeCompanionDesktop\scripts\send-speech-candidate.ps1' -Text 'Milestone summary text.'
```

If the next operation will stop or restart Code Companion Desktop, call the
script with `-WaitForPlayback` and wait for the command to return before
stopping the process. This prevents the spoken warning from being cut off by the
stop operation.
