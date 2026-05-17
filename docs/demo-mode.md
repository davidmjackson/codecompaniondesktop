# Demo Mode Plan

Demo Mode is a temporary session-level speech profile for showing Code
Companion Desktop to other people. It makes Codex more vocal during a live
session so the audience can hear progress, decisions, recommendations, and
short capability summaries without changing normal speech behavior permanently.

## Product Goal

- Let the user say or type `Demo Mode` to begin a more talkative session.
- Let the user say or type `end demo` to return to the standard speech policy.
- Reset Demo Mode automatically when the Desktop app exits or the active
  session ends.
- Keep the feature Desktop-owned: Code Companion Desktop decides the speech
  policy, diagnostics, and state; Code Companion Voice only forwards observed
  candidate events.
- Preserve normal safety, authorization, pairing, privacy filtering, provider
  settings, queue limits, mute behavior, and user controls.

## Non-Goals

- Demo Mode does not bypass pairing or bridge authorization.
- Demo Mode does not store a new provider key or change provider settings.
- Demo Mode does not make every assistant token audible.
- Demo Mode does not persist across app restart by default.
- Demo Mode does not create a separate AI personality contract. It only changes
  speech frequency and spoken-summary style.

## Proposed User Experience

Activation:

- User sends `Demo Mode` in a Codex conversation.
- Desktop recognizes the phrase after normal candidate validation and privacy
  filtering.
- Desktop speaks a short acknowledgement such as `Demo Mode is on. I will speak
  more often during this session.`
- The Status tab shows a visible `Demo Mode on` state with an `End Demo` action.

During Demo Mode:

- Important progress updates are spoken more often.
- Short implementation decisions and tradeoffs are eligible for speech.
- Explicit requests for app recommendations are eligible for speech.
- Final answers are still concise enough for live listening.
- Diagnostics/history mark decisions with a `demo-mode` reason or profile.

Exit:

- User sends `end demo`, clicks `End Demo`, closes the app, or starts a new
  Desktop process.
- Desktop speaks a short acknowledgement for explicit exits.
- Standard speech policy resumes immediately.

## Speech Policy Shape

Add a small speech profile concept:

```text
Standard
Demo
Quiet
```

`Standard` remains the current default. `Demo` increases the set of eligible
assistant candidates and progress summaries. `Quiet` is optional future work,
but designing the profile enum now avoids a one-off boolean that will be harder
to extend.

Candidate handling should stay deterministic:

- Detect exact mode commands with normalized text, case-insensitive.
- Prefer explicit command candidates over heuristic intent detection.
- Keep duplicate detection active so repeated log events do not repeat speech.
- Keep the existing maximum spoken text length guard.
- Keep queue limits and cancellation behavior unchanged unless a later UX
  decision explicitly changes them.

## Desktop Implementation Notes

Likely Desktop changes:

- Add a session-only speech profile service or state object.
- Extend `SpeechCandidatePipeline` to recognize profile commands and apply the
  active profile to policy decisions.
- Add diagnostics fields for active profile and last profile change.
- Add a visible Status tab indicator and an `End Demo` button.
- Include profile information in persisted speech history records only as
  diagnostic metadata, not as a startup setting.
- Avoid changing Code Companion Voice unless the current candidate shape cannot
  represent explicit user phrases clearly enough.

Potential candidate decision reasons:

- `demo-mode-enabled`
- `demo-mode-ended`
- `demo-mode-progress`
- `demo-mode-recommendation`
- `standard-policy`

## Acceptance Criteria

- `Demo Mode` enables Demo profile for the current Desktop process/session.
- `end demo` disables Demo profile and restores Standard policy.
- Closing and reopening Desktop starts in Standard policy.
- The Status tab clearly shows whether Demo Mode is active.
- Demo Mode increases spoken updates for assistant progress and explicit
  recommendation requests.
- Demo Mode still respects mute, queue limits, privacy filtering, duplicate
  detection, provider errors, playback errors, pairing, and authorization.
- Desktop speech history and diagnostics show when Demo Mode affected a
  decision.
- Standard policy behavior remains unchanged when Demo Mode is inactive.

## Test Plan

### Unit Tests

- Command parsing:
  - `Demo Mode`, `demo mode`, and whitespace-wrapped variants enable Demo.
  - `end demo`, `End Demo`, and whitespace-wrapped variants disable Demo.
  - Similar phrases such as `demo this mode` do not toggle the profile.
- Session profile state:
  - Default state is Standard.
  - Enabling Demo is idempotent.
  - Ending Demo is idempotent.
  - A new service/app instance starts in Standard.
- Speech policy:
  - Standard policy decisions match current fixtures when no Demo command has
    been seen.
  - Demo command returns a spoken or queued acknowledgement decision.
  - End command returns a spoken or queued acknowledgement decision.
  - Demo profile accepts eligible progress/update candidates that Standard would
    ignore.
  - Demo profile accepts explicit future-feature recommendation responses.
  - Privacy-filtered candidates stay rejected or ignored in Demo profile.
  - Duplicate message IDs and normalized duplicate text still suppress repeats.
  - Maximum spoken text truncation still applies in Demo profile.
- History and diagnostics:
  - Profile changes are recorded with reason metadata.
  - Spoken Demo decisions include active profile metadata.
  - Provider keys and secrets are never written to history.

### Integration Tests

- Local bridge `POST /v1/speech/candidates`:
  - A paired/authorized client can enable Demo Mode with a valid candidate.
  - An unauthorized client cannot enable Demo Mode.
  - Demo profile affects later candidates from the authorized session.
  - `end demo` from the authorized session restores Standard policy.
- Candidate inbox:
  - Inbox-delivered `Demo Mode` and `end demo` candidates behave the same as
    HTTP candidates.
  - Malformed inbox files cannot change the profile.
- Runtime reset:
  - Recreating the app service graph starts in Standard and does not load Demo
    from `settings.json` or `speech-history.json`.
- UI state:
  - Status tab renders the active profile.
  - `End Demo` button is visible/enabled only while Demo is active.
  - Diagnostics copy text includes active profile without secrets.

### Manual Smoke Tests

Run from PowerShell in the Windows checkout:

1. Start Desktop with
   `dotnet run --project .\src\CodeCompanionDesktop\CodeCompanionDesktop.csproj`.
2. Confirm bridge health reports OK and the Status tab shows Standard policy.
3. Send a Desktop candidate with text `Demo Mode`.
4. Confirm Desktop speaks the acknowledgement and Status shows Demo Mode active.
5. Ask Codex for future app recommendations and confirm a concise spoken
   recommendation is produced.
6. Send a normal progress-style candidate and confirm it is spoken more readily
   than under Standard policy.
7. Send `end demo`.
8. Confirm Desktop speaks the exit acknowledgement and Status returns to
   Standard policy.
9. Restart Desktop and confirm Demo Mode is off.

### Regression Checks

For the implementation slice, run:

```powershell
dotnet build CodeCompanionDesktop.sln
dotnet test CodeCompanionDesktop.sln --no-build
git diff --check
```

If bridge or inbox behavior changes, add focused bridge/inbox tests and include
manual verification from both a Windows VS Code workspace and a WSL Remote
workspace.
