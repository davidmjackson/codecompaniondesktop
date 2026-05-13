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
POST /speak
```

Target bridge endpoints:

```text
GET  /health
POST /v1/client/hello
POST /v1/speech/candidates
GET  /v1/projects
GET  /v1/history/recent
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

The extension repository currently lives in WSL:

```text
/var/www/CodeCompanion
```

Recommendation:

1. Keep the current repository and git history.
2. Refactor the extension into a thin client on its current branch.
3. After the thin client no longer relies on WSL-specific audio/provider
   behavior, move or clone the repository into a Windows checkout, for example:

   ```text
   D:\Development\CodeCompanionVoice
   ```

4. Preserve git history. Do not start a new repository unless the existing
   repository becomes unrecoverable.

The extension can still run in WSL or Windows. Moving the checkout to Windows is
a developer workflow choice, not a runtime dependency.

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
- Keep `/speak` as a compatibility endpoint.
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

- In progress.
- Initial desktop speech diagnostics panel added for bridge state, provider key
  state, last client, last speech candidate, last policy decision, provider and
  playback errors, and recent speech results.
- Initial provider configuration added for ElevenLabs voice ID, model ID, and
  output format.
- Broader provider selection remains pending until a second provider exists.

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

### Milestone 6: Pairing Without Persistent VS Code Secrets

Goal:

- Remove copied long-lived bridge token storage from VS Code.

Scope:

- Add user-approved client pairing in the desktop app.
- Add allowed-client registry in the desktop app.
- Use short-lived in-memory bridge authorization for active extension sessions.
- Keep old token command only as temporary fallback until migration completes.

Acceptance criteria:

- A fresh Windows or WSL VS Code client can request pairing.
- The Windows app prompts the user to approve or deny.
- Durable trust lives in the Windows app.
- VS Code stores no long-lived secret token.

### Milestone 7: Packaging And First Run

Goal:

- Make installation and first-run behavior predictable.

Scope:

- Windows app installer starts or offers to start the bridge.
- VS Code extension detects whether the desktop app is reachable.
- VS Code extension shows only setup guidance and bridge status.
- README documents the two-install flow.

Acceptance criteria:

- Install Windows app.
- Install VS Code extension.
- Configure provider in Windows app.
- Approve VS Code client in Windows app.
- Speech works from Windows and WSL projects without per-project token copying.

### Milestone 8: Cleanup And Compatibility Removal

Goal:

- Remove obsolete prototype paths.

Scope:

- Remove VS Code webview audio unlock.
- Remove VS Code provider key commands.
- Remove VS Code provider calls.
- Remove VS Code desktop-audio playback fallback.
- Remove `/speak` compatibility after consumers migrate.

Acceptance criteria:

- The only normal speech path is VS Code candidate forwarding to the Windows
  desktop app.
- Tests reflect the new responsibility boundaries.
- Documentation no longer describes VS Code-owned TTS.

## Session Checklist

At the start of each session:

1. Read `README.md`.
2. Read this architecture document.
3. Read `docs/session-log.md`.
4. Check branch, status, latest commits, and running app process.
5. State the current milestone from this document.
6. State the last verified baseline from the session log.
7. State the recommended next step.

At the end of each session:

1. Update `docs/session-log.md`.
2. Record milestone progress.
3. Record branch, commit, tests, push status, and app process status.
