# Desktop UX Refresh Notes

These notes capture the proposed Code Companion Desktop app display refresh.
They are product notes, not final UI specifications. The goal is to make the
desktop app easier for a normal user to understand while keeping development
diagnostics available when needed.

## Goals

- Make the first screen answer: is Code Companion ready to speak?
- Hide development and diagnostic surfaces from the normal daily-use path.
- Explain every visible section in plain language.
- Keep pairing, startup, and provider configuration discoverable.
- Provide built-in install and troubleshooting notes for Desktop and the VS Code
  thin client.
- Keep the app compact, readable, and predictable.

## Proposed Window Structure

Use a tabbed main window for the first implementation slice:

- `Status`
- `Advanced`
- `Notes`

Normal setup controls can initially live in `Status`. If that becomes too
dense, split a dedicated `Settings` tab out later.

The app should use fixed normal dimensions and should not allow full-screen or
maximize behavior. Resizing can stay disabled unless a specific accessibility or
text-scaling need appears.

## Status Tab

This should be the default first screen.

Show a clear readiness summary:

- Green tick: all required systems are configured and healthy.
- Red error state: one or more required systems are missing, unavailable, or
  misconfigured.

The readiness summary should include:

- Desktop bridge listening.
- Provider key saved.
- Provider configuration valid enough to make a request.
- Last provider/playback error, if any.
- VS Code client pairing state when a recent client is pending or denied.
- Startup registration status if startup is enabled.

Recommended copy:

- Healthy: `All systems are working and APIs are configured.`
- Not healthy: list the concrete fix, for example `Save an ElevenLabs API key`
  or `Approve the pending VS Code client`.

## Settings Area

For the first implementation slice, keep normal setup controls in the `Status`
tab beneath the readiness summary. Later, split them into a dedicated
accordion-style `Settings` tab if the Status tab becomes too dense.

Recommended default order:

1. `Startup`
2. `Speech Provider`
3. `Client Pairing`
4. `Local Bridge`

Rationale:

- Startup determines whether the service is available before VS Code opens.
- Speech Provider is a core user setting.
- Client Pairing is normal first-run setup, not a development-only tool.
- Local Bridge is useful for troubleshooting but still part of normal operation.

### Startup

Keep:

- `Start with Windows sign-in`
- `Start hidden to tray`

Move startup diagnostic log/details to `Advanced > Logs` unless the Startup
section needs to show a specific warning.

### Speech Provider

Keep the existing provider fields.

Improve key status:

- If a key is saved, show `Key loaded` with a green tick.
- If no key is saved, show `No key loaded` with a red or neutral warning state.
- Avoid implying failure when the app is already speaking successfully.

The key controls can remain:

- `Save Key`
- `Load Key`
- `Clear Key`

### Client Pairing

Keep this visible in Settings because it is part of first-run setup.

Recommended behavior:

- Show pending clients in a more user-friendly list.
- Keep `Approve Pending`.
- Keep manual Client ID approve/deny controls behind an advanced/fallback
  affordance or secondary row.
- Explain that pairing approves VS Code clients so they can send speech
  candidates to Desktop.

### Local Bridge

Keep bridge status visible enough for setup troubleshooting.

Recommended behavior:

- Show bridge endpoint and listening state.
- Keep queue settings if users need them.
- Move low-level bridge diagnostics to `Advanced > Logs`.

## Advanced Tab

Use this for details most average users do not need.

Show a clear message at the top:

`This section is for development and troubleshooting. You usually do not need to change anything here.`

Include:

- Project Registry.
- Project Speech History.
- Speech Diagnostics.
- Startup Diagnostics.
- Logs or copied diagnostic text.
- Manual/fallback client ID tools if they remain too detailed for normal
  Settings.

### Project Registry

Add explanatory text:

`Tracks known Code Companion projects and their Windows/WSL path aliases so speech history and diagnostics can be grouped correctly.`

This should be hidden under Advanced unless a user-facing project management
workflow is added.

### Project Speech History

Add explanatory text:

`Shows recent speech decisions grouped by project for troubleshooting.`

This should stay under Advanced unless it becomes a normal history feature.

### Speech Diagnostics

Decision needed:

- Current-session diagnostics should clear after app restart.
- Longer-term speech history can remain persisted separately if useful for
  troubleshooting.

Recommendation:

- Keep live diagnostics in memory for the current Desktop process.
- Keep persisted `speech-history.json` as Advanced history for recent speech
  results, with a user-visible clear action later if needed.

## Notes Tab

Add a documentation tab containing setup and usage instructions.

Content should include:

- How to install and start Code Companion Desktop.
- How to save provider settings and API key.
- How to enable Windows startup and hidden-to-tray behavior.
- How to install Code Companion Voice in VS Code.
- How to run `Code Companion Voice: Send Desktop Candidate Test`.
- How to approve VS Code clients.
- What every visible control/button does.
- Basic troubleshooting steps.

The user requested text and images. Implementation options:

- Start with structured in-app text for the first version.
- Add images/screenshots later when the UI stabilizes.

## Recommendations

- Do not put Client Pairing entirely under Advanced. It is a normal first-run
  flow.
- Keep the first UX refresh focused on layout, labels, status, and help text
  before adding screenshots.
- Treat full-session diagnostic persistence as an explicit data-retention
  decision. Users should understand what is remembered across restarts.
- Consider a `Copy Support Bundle` action later that gathers bridge health,
  provider status without secrets, pairing state, and recent errors.
- Consider a first-run checklist on the Status tab:
  provider key, Desktop bridge, VS Code extension, pairing, test speech.
- Keep development-only controls available but visually separate from normal
  user controls.

## Open Decisions

- Resolved: this UX refresh is pulled forward before completing Milestone 9.
- Should app resizing be completely disabled, or should resizing be constrained
  to a fixed minimum/maximum for accessibility?
- Should persisted speech history be retained by default, shortened, or made
  user-clearable?
- Should the Notes tab support images in the first version or defer images until
  after the layout stabilizes?
