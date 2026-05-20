Code Companion — Reliability and Observability Spec

Status: Draft, ready for Claude Code to ingest and convert into To-Dos.
Owner: David Jackson.
Created: 2026-05-20.
Architectural context: docs/architecture.md ADR 0001 (voice + bridge + pack split). This spec does not change the architecture. It hardens the existing one by adding observability, eager pairing, and a reliable fallback.


Why This Exists
The current architecture is correct: voice (workspace host) observes assistant transcripts, bridge (UI host) talks to Desktop on 127.0.0.1:47321, and Desktop owns TTS. But every failure mode is silent: "no TTS happens" is the only symptom for at least five different root causes (bridge missing in UI host, Desktop not running, client not paired, candidate not yet produced, stale session token after Desktop restart).
This spec adds the missing diagnostic and self-healing layer so failures explain themselves and most recover automatically.
Guiding Principles

Do not change the architecture. voice + bridge + pack stays. HTTP on 47321 stays. Cross-host command relay stays.
Every failure mode must be observable without reading source code.
Prefer self-healing over user prompts. Use one-shot alerts only when user action is genuinely required (e.g. approve pairing in Desktop).
Each task in this spec is independently shippable. Do not bundle. Each gets its own branch, PR, and verification.

Tasks Overview
#TaskRepo(s)RiskOrder1Install Verifier commandVoiceLowFirst2Honest status barVoiceLowSecond3Eager pairing on activationVoice (bridge)LowThird4Pipeline self-testVoice + DesktopMediumFourth5Promote candidate inbox to first-class fallbackVoice + DesktopMediumFifth6Diagnostic guardrail in AGENTS.mdVoice + DesktopNoneLast
Work them in this order. Do not skip ahead, later tasks assume earlier ones are merged.

Task 1 — Install Verifier Command
Goal
A single VS Code command that answers, in plain language, the question: "Is Code Companion installed and working in this window right now?"
Where
Code Companion Voice repository (D:\Development\CodeCompanionVoice).
Command
Add command: codeCompanionVoice.verifyInstall.
Title: Code Companion: Verify Install.
Behaviour
When run, the command:

Opens the Code Companion Voice output channel.
Runs each of these checks in order and writes a clearly labelled result line for each:
CheckPass criteriaFail message guidanceVoice extension active in workspace hostThis command is running, so yesn/aWorkspace host environmentPrint process.platform and WSL_DISTRO_NAME if presentn/aProject identity file present.code-companion/project.json exists and parses with schemaVersion: 1, non-empty projectId, non-empty displayName"Missing or invalid project identity. Add .code-companion/project.json per README."Bridge command registered in UI hostvscode.commands.getCommands(true) includes codeCompanion.bridge.deliverCandidate"Code Companion Bridge is not installed or not active on the local Windows machine. Install the bridge VSIX into the Windows UI extension host and reload."Desktop reachable from bridgeInvoke new bridge command codeCompanion.bridge.checkHealth (already exists) and report reachable, baseUrl, error"Code Companion Desktop is not running on Windows, or not listening on 127.0.0.1:47321. Start Desktop and re-run Verify Install."Client pairing stateInvoke a new bridge command codeCompanion.bridge.getPairingState (Task 1b below) returning { authorization, clientId, expiresAtUtc }Print state. If pending or denied, instruct user to open Desktop's Client Pairing panel.Last candidate delivery (if any)Read from in-memory ring buffer maintained by voice (last 5 deliveries), see Task 1cIf none yet, say "No candidates delivered yet in this session."

After all checks, prints a summary line:

[verify] PASS, Code Companion is ready. (all checks green)
[verify] PARTIAL, N issues found. See above. (one or more amber/red)


Returns a structured result object so it is callable from tests:

tsinterface VerifyInstallResult {
  ok: boolean;
  checks: Array<{
    name: string;
    status: "pass" | "warn" | "fail";
    detail: string;
  }>;
}
Sub-task 1b — New bridge command: getPairingState
In the bridge extension, register codeCompanion.bridge.getPairingState. It returns:
ts{
  authorization: "allowed" | "pending" | "denied" | "unknown",
  clientId: string | undefined,
  expiresAtUtc: number | undefined
}
Read from the existing session variable in bridge/src/extension.ts. If no session yet, authorization: "unknown".
Sub-task 1c — Delivery ring buffer
In voice/src/extension.ts, maintain an in-memory array of the last 5 candidate deliveries with shape:
ts{
  timestamp: string;
  source: "Codex" | "Claude Code";
  candidateId: string;
  outcome: "delivered" | "failed" | "bridge-missing" | "pairing-pending";
  detail?: string;
}
Push on every handleCandidate outcome. Expose via the verify command.
Acceptance Criteria

Running Code Companion: Verify Install from the command palette produces a clearly formatted, plain-English report in the output channel.
The report identifies exactly which layer is broken when something is broken.
If everything works, the final line reads PASS.
Unit tests in extensions/voice/test/ cover at least: missing project identity, missing bridge command, healthy state.

Definition of Done

Compiles cleanly: npm run compile.
Tests pass: npm test.
VSIX rebuilt: npm run package:vsix (or per-extension vsce package).
Manual run in fresh project, three states tested:

Desktop running, paired, PASS.
Desktop not running, PARTIAL, correct fail message on the Desktop reachability check.
Bridge uninstalled from UI host, PARTIAL, correct fail message.


Commit message: feat(voice): add Verify Install command.
Push branch and open PR.


Task 2 — Honest Status Bar
Goal
The status bar entry in voice currently always shows $(megaphone) Code Companion. It must reflect actual pipeline state.
Where
Code Companion Voice repository, file extensions/voice/src/extension.ts.
Behaviour
The status bar item updates whenever pipeline state changes. States and their presentation:
StateIconTextTooltipHealthy, paired, recent successful delivery$(check)Code Companion"Connected to Code Companion Desktop. Last delivery: [time]."Active, paired, no candidates yet$(megaphone)Code Companion"Connected to Code Companion Desktop. Waiting for assistant activity."Pairing pending$(warning)Code Companion: approve"Approve this VS Code client in Code Companion Desktop's Client Pairing panel."Pairing denied$(error)Code Companion: denied"Code Companion Desktop has denied this client. Approve it in Desktop to enable spoken updates."Bridge missing in UI host$(error)Code Companion: no bridge"Code Companion Bridge is not active on the local Windows machine. Install the bridge VSIX and reload."Desktop unreachable$(error)Code Companion: Desktop down"Code Companion Desktop is not running on 127.0.0.1:47321. Start Desktop."
Implementation Notes

The status bar command should remain codeCompanionVoice.showStatus so clicking still opens the output channel.
State transitions happen in:

restartWatchers start/finish.
Each handleCandidate outcome.
On a 30-second timer that re-checks bridge health and pairing state when no candidates have arrived (so a Desktop restart is reflected without waiting for the next assistant message).


Extract status logic to a new file voice/src/statusController.ts so it is independently testable.

Acceptance Criteria

Status bar reflects every state listed above.
A Desktop restart, with no other action, is reflected in the status bar within 30 seconds.
Clicking the status bar item still opens the output channel.
Unit tests for the state machine in statusController.ts.

Definition of Done
Same checklist as Task 1: compile, test, VSIX, manual verification across all states, commit, push, PR.
Commit message: feat(voice): show real pipeline state in status bar.

Task 3 — Eager Pairing on Activation
Goal
Today, pairing only happens when the first candidate arrives. If Claude Code or Codex don't produce any assistant messages for 20 minutes, the user never sees the "approve in Desktop" prompt. Pair immediately when the project opens.
Where
Code Companion Voice repository, primarily extensions/bridge/src/extension.ts and extensions/voice/src/extension.ts.
Behaviour
On voice extension activation, after restartWatchers returns and after the bridge health check succeeds:

Voice builds a minimal "hello-only" payload (the same client and workspace blocks it would build for a candidate, no codex or candidate blocks).
Voice invokes a new bridge command codeCompanion.bridge.pairNow with that payload.
Bridge calls /v1/client/hello on Desktop.
If allowed: cache session, status bar goes green, no notification.
If pending or denied: bridge shows the existing pairing notification (re-use notifyPairingState logic).
If Desktop unreachable: bridge logs and updates status; no notification (the activation health check already covers this).

Implementation Notes

Add a new bridge command codeCompanion.bridge.pairNow(helloPayload) that wraps the existing performHello function. It must not depend on lastHello being a candidate-shaped object.
Refactor performHello to accept a DesktopClientHelloRequest directly (it already does).
Make eager pairing idempotent: if a valid session already exists, the command returns the existing session state without re-calling Desktop.
Eager pairing must not block voice activation. Fire-and-forget with logging.

Acceptance Criteria

Open a project with Desktop running but client not yet approved, pairing notification appears within 5 seconds, no need to wait for an assistant message.
Open a project with Desktop running and client already approved, status bar goes green within 5 seconds, no notification.
Open a project with Desktop not running, no spurious notification (the existing activation alert handles it).
Restart Desktop while a project is open, next eager re-pair on the 30-second status timer succeeds and status bar goes green.

Definition of Done
Compile, test, VSIX, three-state manual verification, commit, push, PR.
Commit message: feat(bridge): eager pairing on activation.

Task 4 — Pipeline Self-Test
Goal
A single user action that proves the entire pipeline end-to-end without waiting for Codex or Claude Code to produce a message.
Where
Both repositories: Voice (new command) and Desktop (new bridge endpoint behaviour).
Behaviour
Voice side
Add command codeCompanionVoice.runSelfTest.
Title: Code Companion: Run Pipeline Self-Test.
When run:

Voice builds a candidate with candidate.kind = "self-test", a fixed text like "Code Companion pipeline self-test.", and phase = "final".
Voice sends it via the existing bridge command.
Voice waits for the bridge result.
Voice prints a single result line to the output channel:

[self-test] PASS, Desktop spoke the test phrase. (if Desktop reports decision: spoken)
[self-test] PASS, Desktop accepted the test (silent test mode). (if Desktop is configured silent)
[self-test] FAIL, <reason>. (any failure)



Desktop side
In Desktop, the bridge handler for /v1/speech/candidates must recognise candidate.kind === "self-test" and:

Apply normal policy and queueing.
Speak the phrase unless the user has disabled audible self-tests in Desktop settings.
Add a new Desktop setting: Pipeline self-test playback with values Speak (default) and Silent.
Always log the self-test in the Speech History panel with a distinct tag so the user can confirm it arrived.

Implementation Notes

The self-test must use the same code path as a real candidate. No "test bypass" routes. This is the point, it proves the real path works.
Reject self-test candidates with empty or oversize text the same as real ones.
Self-test does not require a codex.sessionId. Use "self-test" for sessionId, messageId as a fresh UUID, timestamp as now.

Acceptance Criteria

Running the self-test with a healthy pipeline plays the test phrase through Windows audio.
Running with Desktop in silent self-test mode logs in Speech History without playing.
Running with bridge missing, Desktop down, or client not paired returns a precise FAIL reason matching what Verify Install would say.

Definition of Done
Both repos: compile, test, package, manual verification.
Voice commit: feat(voice): pipeline self-test command.
Desktop commit: feat(desktop): accept self-test candidate kind.

Task 5 — Promote Candidate Inbox to First-Class Fallback
Goal
The candidate inbox (%APPDATA%\CodeCompanionDesktop\candidate-inbox) is currently described as a "migration path". Promote it to a guaranteed-delivery fallback so transient HTTP failures do not lose milestone updates.
Where
Both repositories.
Behaviour
Voice / Bridge side
In extensions/bridge/src/desktopBridge.ts and the bridge extension:

When sendSpeechCandidateToDesktopBridge fails for any reason that is not an auth failure (auth failures already self-heal via re-pair):

Bridge writes the candidate JSON to the inbox path.
Inbox path resolution:

Windows: %APPDATA%\CodeCompanionDesktop\candidate-inbox.
From WSL workspace host: must not write directly to \\wsl.localhost. Instead, voice serialises the candidate and passes it to bridge via the existing cross-host command, and bridge (on Windows) writes the file. This keeps the inbox a purely Windows concern.




Filename: <ISO-timestamp>-<messageId>.json.
Bridge returns a result with decision: "inbox", reason: "http-unavailable", so the voice ring buffer and status bar reflect it.

Desktop side
The inbox watcher already exists. Confirm it:

Handles concurrent writes safely (use FileSystemWatcher with debouncing).
Deletes accepted files, moves invalid ones to candidate-inbox\rejected.
Surfaces inbox-delivered candidates in Speech History with a distinct source tag (source: "inbox").

Implementation Notes

Cap inbox writes to one per candidate. If the file already exists (same messageId), bridge does not overwrite.
Add a bridge setting codeCompanionVoice.desktopBridge.inboxFallback.enabled (default true).
When false, HTTP failures are reported as failures with no inbox fallback. Useful for debugging.

Acceptance Criteria

Stop Desktop, send a candidate, restart Desktop, the candidate is delivered from the inbox shortly after restart.
The output channel and status bar show "inbox" outcomes distinctly from "delivered" outcomes.
Disabling the setting causes HTTP failures to surface as failures with no inbox file written.

Definition of Done
Both repos: compile, test, package, manual verification of stop-Desktop-and-restart scenario.
Voice commit: feat(bridge): write candidates to inbox on HTTP failure.
Desktop commit: feat(desktop): treat inbox as first-class delivery path.

Task 6 — Diagnostic Guardrail in AGENTS.md
Goal
Stop the "patch by adding more things" pattern. Force the agent to diagnose before modifying.
Where
D:\Development\CodeCompanionDesktop\AGENTS.md and D:\Development\CodeCompanionVoice\AGENTS.md.
Change
Add a new section to both files:
markdown## Diagnose Before Modifying

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
Acceptance Criteria

Both AGENTS.md files contain the section.
Section appears before the "Communication" section in each file.

Definition of Done
One commit per repo: docs(agents): require diagnose-before-modify.

Cross-Cutting Notes
Branching
One branch per task. Naming: feat/reliability-taskN-shortname, e.g. feat/reliability-task1-verify-install.
Testing Discipline
Each task requires both unit tests (where applicable) and a manual smoke run on a fresh project window. Add the smoke steps to docs/smoke-checklist.md in the Desktop repo as each task lands.
Release Coordination
Tasks 4 and 5 touch both repos. The Desktop side must be released first (or at least built locally first) so the Voice side has something to talk to. Update docs/release-checklist.md to call this out.
What This Spec Does Not Do

Does not rewrite the architecture. voice + bridge + pack stays.
Does not change the HTTP protocol on 47321 beyond accepting a new candidate kind in Task 4.
Does not change credential storage, audio playback, or ElevenLabs integration.
Does not address packaging, signing, or auto-update.

If during implementation any task feels like it requires changes outside its scope, stop and ask the user before proceeding.

Reference

Architecture source of truth: D:\Development\CodeCompanionDesktop\docs\architecture.md ADR 0001.
Voice extension structure: D:\Development\CodeCompanionVoice\README.md.
Existing bridge contract: D:\Development\CodeCompanionDesktop\README.md (Local Bridge section).

