# Code Companion Desktop — Smoke Checklist

A structured manual pass that exercises every user-visible flow end-to-end.
Run it before any field-test build and after any change to the speech
pipeline, bridge, pairing logic, or Status tab.

**Intended use:** tick boxes as you go. Anything that fails becomes a session
log entry (`session-log.md`) and ideally an automated test added to the
suite.

**Build under test:** _record commit SHA and version here before starting_

- Commit: `__________`
- App version (shown in `/health`): `__________`
- Tester: `__________`
- Date: `__________`

---

## 0. Preconditions

- [ ] Clean state: no stale candidates in `%APPDATA%\CodeCompanionDesktop\candidate-inbox\`
- [ ] `speech-history.json` mtime noted (for later before/after checks)
- [ ] VS Code closed (so a clean pairing exercise can run from cold)
- [ ] Code Companion Voice VSIX available at `D:\Development\CodeCompanionVoice` (built via `npm run package:vsix` if needed)
- [ ] Audio output device working — play any system sound to confirm
- [ ] Solution builds clean:
  - [ ] `dotnet build CodeCompanionDesktop.sln` exits 0
  - [ ] `dotnet test CodeCompanionDesktop.sln --no-build` reports all 47+ tests passing

---

## 1. Cold start

- [ ] Launch Desktop from a fresh process (no existing instance running)
- [ ] Status tab opens by default and shows initial readiness text
- [ ] `Get-Process CodeCompanionDesktop` returns a single PID
- [ ] Bridge health responds within 5 seconds of launch:
  - `Invoke-RestMethod http://127.0.0.1:47321/health`
  - [ ] `status: ok`
  - [ ] `bridge: listening`
  - [ ] `speaking: false`
  - [ ] `speechProfile: "Standard"`
  - [ ] `queued: 0`
- [ ] No crash dialog, no red Status tab error banner
- [ ] First-run notes / pairing prompt visible if no prior paired client

---

## 2. Voice extension pairing

- [ ] Open VS Code (Windows, not WSL Remote — verify host in title bar)
- [ ] Install Code Companion Voice from local VSIX (`code --install-extension <path>`)
- [ ] Extension activates without error in VS Code → Output → Code Companion Voice
- [ ] Trigger pairing from Voice (command palette: `Code Companion: Pair`)
- [ ] Desktop Status tab shows `Pending pairing request` with the client ID
- [ ] Click `Approve` on the pending pairing
- [ ] Voice extension logs successful pair
- [ ] `health.bridge` continues to report `listening`
- [ ] `client-trust.json` in `%APPDATA%\CodeCompanionDesktop\` lists the new client
- [ ] Restart Desktop — pairing persists, no re-approval required

### WSL Remote variant (separate extension host)

- [ ] Connect VS Code to WSL Remote
- [ ] Voice extension is NOT present in WSL host (separate extension list)
- [ ] Install VSIX into the WSL host (`code --install-extension <path>` from WSL terminal)
- [ ] Pairing flow above repeats successfully from WSL host
- [ ] Both Windows and WSL hosts can pair simultaneously without conflict

### Install Verifier command (Reliability Task 1)

Run `Code Companion: Verify Install` from the command palette in three states.
Each run writes a labelled report to the Code Companion Voice output channel.

- [ ] **Healthy** — Desktop running, client paired:
  - [ ] Every check line reads `PASS`
  - [ ] Final line reads `[verify] PASS, Code Companion is ready.`
  - [ ] `Client pairing state` shows the paired client ID
- [ ] **Desktop not running** — close Desktop, re-run Verify Install:
  - [ ] `Desktop reachable` check reads `FAIL`
  - [ ] Its detail names `127.0.0.1:47321` and says to start Desktop
  - [ ] Final line reads `[verify] PARTIAL, N issues found.`
- [ ] **Bridge uninstalled** — disable/uninstall the bridge from the Windows UI
      extension host, reload, re-run Verify Install:
  - [ ] `Bridge command registered` check reads `FAIL`
  - [ ] Its detail says to install the bridge VSIX and reload
  - [ ] `Desktop reachable` and `Client pairing state` read `WARN` (skipped)
- [ ] After at least one assistant update has been delivered, re-run Verify
      Install — `Last candidate delivery` lists the recent delivery outcomes

### Status bar — honest pipeline state (Reliability Task 2)

The Voice status bar item must reflect actual pipeline state, not a fixed
label. Hover it to read the tooltip in each state.

- [ ] **Healthy** — paired, after a delivery: `$(check) Code Companion`,
      tooltip `Connected to Code Companion Desktop. Last delivery: ...`
- [ ] **Waiting** — paired, no candidates yet: `$(megaphone) Code Companion`,
      tooltip mentions `Waiting for assistant activity`
- [ ] **Pairing pending** — `$(warning) Code Companion: approve`
- [ ] **Pairing denied** — `$(error) Code Companion: denied`
- [ ] **Bridge missing** — disable the bridge on the UI host, reload:
      `$(error) Code Companion: no bridge`
- [ ] **Desktop down** — close Desktop: `$(error) Code Companion: Desktop down`
- [ ] **Desktop restart reflected within 30s** — with Desktop down and the bar
      showing `Desktop down`, start Desktop and take no other action; the bar
      returns to a healthy/waiting state within 30 seconds (the re-probe timer)
- [ ] Clicking the status bar item still opens the Code Companion Voice
      output channel

### Eager pairing on activation (Reliability Task 3)

Pairing must happen at project open, without waiting for an assistant message.

- [ ] **Desktop up, client not yet approved** — open a fresh project window;
      the pairing notification appears within ~5 seconds (no assistant message
      needed). The Voice output channel logs `Eager pairing: pending`.
- [ ] **Desktop up, client already approved** — open a project window; the
      status bar reaches a healthy/waiting state within ~5 seconds with **no**
      notification. Output channel logs `Eager pairing: already paired` or
      `Eager pairing: paired`.
- [ ] **Desktop not running** — open a project window; no spurious pairing
      notification (the activation health alert handles it). Output channel
      logs `Eager pairing skipped: Code Companion Desktop not reachable`.
- [ ] **Desktop restart while project open** — restart Desktop; within 30
      seconds the status timer re-pairs and the status bar returns to green.

### Pipeline self-test (Reliability Task 4)

`Code Companion: Run Pipeline Self-Test` proves the whole pipeline end to end
without waiting for an assistant message.

- [ ] **Healthy, playback Speak** — run the self-test; Windows audio speaks
      "Code Companion pipeline self-test." The Voice output channel reads
      `[self-test] PASS, Desktop spoke the test phrase.`
- [ ] Speech History shows the entry tagged `spoken/self-test`
- [ ] **Silent mode** — in Desktop settings (Speech Behavior) uncheck
      "Speak pipeline self-test phrases", run the self-test again:
  - [ ] No audio plays
  - [ ] Voice output reads `[self-test] PASS, Desktop accepted the test
        (silent test mode).`
  - [ ] Speech History shows the entry tagged `silent/self-test`
- [ ] **Failure paths** — run the self-test with the bridge missing, with
      Desktop down, and with the client unpaired; each prints
      `[self-test] FAIL, <reason>.` with a reason matching Verify Install
- [ ] The self-test setting persists across a Desktop restart (`settings.json`)

### Candidate inbox as a first-class fallback (Reliability Task 5)

When the live HTTP path is down, the bridge saves the candidate to the inbox
so milestone updates are not lost.

- [ ] **Stop-Desktop-then-restart** — close Desktop, trigger an assistant
      update (or run the self-test), then restart Desktop:
  - [ ] The Voice output channel shows an `inbox` outcome for the candidate
  - [ ] A `<timestamp>-<messageId>.json` file appears in
        `%APPDATA%\CodeCompanionDesktop\candidate-inbox\`
  - [ ] On restart, Desktop picks the file up within ~1s and speaks it
  - [ ] Speech History tags the entry `via inbox` (distinct from `via bridge`)
  - [ ] The inbox file is deleted after it is accepted
- [ ] **Distinct from delivered** — the status bar / output `inbox` outcomes
      read differently from live `delivered` outcomes
- [ ] **One write per candidate** — the same candidate is not written to the
      inbox twice (same `messageId`)
- [ ] **Fallback disabled** — set
      `codeCompanionVoice.desktopBridge.inboxFallback.enabled` to `false`;
      with Desktop down, a candidate now surfaces as a plain failure and **no**
      inbox file is written

---

## 3. Speech pipeline — Standard profile

- [ ] Profile is `Standard` (confirm via `/health`)
- [ ] Manual speech via PowerShell script speaks audibly:
  - `& 'D:\Development\CodeCompanionDesktop\scripts\send-speech-candidate.ps1' -Text "Standard profile smoke test" -WaitForPlayback`
  - [ ] Audio plays
  - [ ] `speech-history.json` mtime advances
  - [ ] Status tab shows the line in recent activity
- [ ] Long speech truncation behaves (paste a 1500-char string — `MaxSpeechTextLength` is 1000):
  - [ ] Spoken output is truncated with `...` suffix
  - [ ] `speech-history.json` records `Reason=truncated`
  - [ ] No exception in Desktop logs
- [ ] Non-final assistant progress candidate WITHOUT a speech hint is **ignored** in Standard:
  - Drop a JSON file in `candidate-inbox\` with `"phase": "progress"` and no `speechHint`
  - [ ] `speech-history.json` records `Decision=ignored`, `Reason=non_final_candidate`
  - [ ] No audio played
- [ ] Non-final candidate **with a recognised** `speechHint` speaks regardless of finality. Recognised values (case-insensitive):
  - `voice-check-in`
  - `manual-speak-last`
  - `manual-desktop-candidate-test`
  - Any other hint is treated as no hint and the candidate is ignored when non-final.
- [ ] Inbox processes files within ~1 second of write (file-watcher latency)

---

## 4. Demo Mode

- [ ] From Standard, send a candidate with exact text `Demo Mode`:
  - [ ] Audio speaks the activation line
  - [ ] `/health` now reports `speechProfile: "Demo"`
  - [ ] Status tab readiness text reflects Demo
  - [ ] Diagnostics view shows Demo as active profile
- [ ] In Demo, a non-final no-hint progress candidate **speaks** (Standard would have ignored it):
  - [ ] Audio plays with reason `demo-mode-progress` in history
- [ ] Send `end demo`:
  - [ ] Audio speaks the exit line
  - [ ] `/health` returns to `speechProfile: "Standard"`
  - [ ] Status tab `End Demo` button is disabled or hidden
- [ ] Re-enter Demo, then **close the Desktop app** while in Demo
- [ ] Reopen Desktop
  - [ ] `/health` reports `speechProfile: "Standard"` (session-only reset)
  - [ ] No stale Demo banner

### Status tab End Demo button

- [ ] Enter Demo via candidate, then click `End Demo` on Status tab
- [ ] Profile returns to Standard, audio confirms exit
- [ ] Button disabled while in Standard

---

## 5. Notes tab — Voice extension setup section

- [ ] Notes tab is reachable from the main nav
- [ ] `VS Code Extension Setup` section is present and rendered
- [ ] Section mentions both Windows host and WSL Remote host as separate installs
- [ ] Local VSIX path (`D:\Development\CodeCompanionVoice` + `npm run package:vsix`) is described
- [ ] Marketplace wording is still described as "future" (Milestone 9 parked)
- [ ] All hyperlinks render and click through (or are deliberately plain text)

---

## 6. Bridge resilience

- [ ] **Bridge unreachable while inbox active** — stop the bridge listener (close Desktop), drop a candidate JSON, restart Desktop:
  - [ ] Candidate is picked up on restart and processed correctly
  - [ ] No exception thrown about port binding
- [ ] **Port already in use** — start a second Desktop instance:
  - [ ] Second instance either exits cleanly with a clear error OR hands focus to the first instance (whichever is intended)
  - [ ] No bridge port conflict left dangling
- [ ] **Speech while Desktop closing** — send a long speech, immediately close Desktop:
  - [ ] No crash dialog
  - [ ] `speech-history.json` is left in a valid JSON state (parses successfully)
- [ ] **Non-JSON files left alone** — drop a `.txt` file into `candidate-inbox\`:
  - [ ] File stays in inbox (watcher only acts on `.json` — this is intended)
  - [ ] Bridge stays alive
  - [ ] Clean it up manually after the test

---

## 7. Diagnostics surface

- [ ] Diagnostics view shows:
  - [ ] Active speech profile
  - [ ] Paired client count
  - [ ] Recent candidate reasons (last 10+)
  - [ ] Bridge port and listen state
  - [ ] App version matching `/health`
- [ ] Copy-to-clipboard (if present) produces parseable output
- [ ] No personally identifiable info leaks into diagnostics text

---

## 8. Teardown

- [ ] Close VS Code (both hosts if WSL exercised)
- [ ] Close Desktop via tray icon → Exit (not Task Manager)
- [ ] `Get-Process CodeCompanionDesktop` returns nothing
- [ ] Bridge port 47321 free: `Test-NetConnection -ComputerName 127.0.0.1 -Port 47321` reports filtered/closed
- [ ] `candidate-inbox\` contains only the `rejected/` archive — no unprocessed files
- [ ] `speech-history.json` is valid JSON (parse-check)

---

## 9. Notes for the run

_Use this block during the pass. Anything surprising — even passes that feel
flaky — gets a note here, then promoted to a session log entry plus an issue
or test gap at the end._

```
[time] [section] [note]
```

---

## Promotion to automated tests

After the run, list which steps would benefit most from being converted to
automated tests. Prioritise:

1. Anything that failed.
2. Anything that passed but felt fragile (timing-dependent, only worked on
   the second try, required a manual workaround).
3. Anything that touches the speech-history or candidate-inbox files —
   those are the smallest surfaces to assert against in tests.

Record the chosen items as `next` entries in `session-log.md`.
