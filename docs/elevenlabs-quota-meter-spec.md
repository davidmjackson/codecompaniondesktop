# ElevenLabs Quota Meter — Completion Spec

Date: 2026-07-17
Branch: `feature/elevenlabs-quota-meter`
Status: design approved, implementation pending

## Purpose

Finish and merge the ElevenLabs quota meter, which shows how much of the
ElevenLabs character allowance has been consumed — as a percentage of the plan
limit when the API key permits reading it, and as a plain characters-used figure
for the last 30 days when it does not.

The feature was built in May 2026 and parked. This spec records why it was
parked, and what must change before it can ship.

## Why it was parked (verified root cause)

The code was never the problem. The API key was.

Probing the ElevenLabs API with the key stored in Windows Credential Manager
(`CodeCompanionDesktop/ElevenLabsApiKey`) on 2026-07-17:

| Request | Result |
| --- | --- |
| `POST /v1/text-to-speech/{voice_id}` | **200** — returns audio |
| `GET /v1/user/subscription` | **401** |
| `GET /v1/user` | **401** |
| `GET /v1/voices` | **401** |
| `GET /v1/models` | **401** |

The 401 body states the cause exactly:

```json
{"detail":{"type":"authentication_error","code":"unauthorized",
 "message":"The API key you used is missing the permission user_read to execute this operation."}}
```

`/v1/voices` fails the same way, naming `voices_read`.

So the key is **scoped to text-to-speech only**. It speaks correctly and can
never read the account. The quota meter's single dependency,
`GET /v1/user/subscription`, requires the `user_read` permission.

Two findings that cost time and are worth recording:

- **`Invoke-WebRequest` hides the error body.** Its exception path had already
  consumed the response stream, so the 401 appeared to have an empty body, which
  pointed away from a permissions cause. Reading the body with `HttpClient`
  revealed the real message. Diagnose ElevenLabs errors with `HttpClient`.
- **`xi-api-key` is the correct header.** `Authorization: Bearer <key>` is
  rejected with `invalid_authorization_header`. The client's auth is correct and
  must not be "fixed".

## Health of the branch

Verified on 2026-07-17 after merging `main` (13 commits of drift) into the
branch:

- Merge is clean apart from one trivial `.gitignore` conflict (both entries kept).
- `dotnet build CodeCompanionDesktop.sln` — 0 errors, 0 warnings.
- `dotnet test CodeCompanionDesktop.sln` — **87/87 passing** (`main`'s 72 plus
  the branch's 15 quota tests).

Two months of drift broke nothing. `QuotaTracker` and the wiring into the speech
path are correct: `OnSpeechProduced(text.Length)` is called with the same `text`
passed to `CreateSpeechAsync`, and only after that call succeeds, so failed
provider calls do not consume quota.

## Two data sources, and why both are needed

The meter's four fields do not come from one place. This is structural, not a
quirk, and it drives the whole design.

| Field | `GET /v1/usage/character-stats` | `GET /v1/user/subscription` |
| --- | --- | --- |
| characters used | **yes** | yes |
| character limit | no | **yes** |
| tier | no | **yes** |
| next reset | no | **yes** |
| scope required | none beyond the TTS key | `user_read` |

Verified on 2026-07-17 with the current TTS-only key:

- `GET /v1/usage/character-stats?start_unix={ms}&end_unix={ms}` → **200**, with
  real daily usage. Over 31 days it returned 247,631 characters across 22 active
  days. **No `user_read` needed.**
- The window parameters are **unix milliseconds**. Passing seconds returns
  `{"time":[...],"usage":{}}` — a silent empty result, not an error. This is a
  trap: seconds look valid and fail quietly.
- Response shape: `{"time":[unix_ms,...],"usage":{"All":[chars,...]}}` — daily
  buckets, parallel arrays.

`character_limit` has exactly one source in the API. These all return **404**;
there is no workspace-level quota endpoint:

```text
/v1/workspace/subscription   404
/v1/workspace/usage          404
/v1/usage/subscription       404
/v1/subscription/info        404
```

Service accounts do not solve this: they are documented as multi-seat-only
(Scale/Business/Enterprise), and the docs are silent on what the user-scoped
`/v1/user/subscription` returns for a non-user identity. Scope configuration is
available on personal keys too, so a personal key with `user_read` is the
supported route.

## Prerequisite (user action, optional)

Granting `user_read` unlocks the limit, tier, and reset date — the percentage.
Simplest route: create a new personal API key with `user_read` **and**
`text_to_speech` selected, then save it in Speech Provider. The existing key
shows no scope toggle, which the ElevenLabs docs do not explain (possibly a
legacy key predating their August 2024 permissions feature).

The feature ships useful **without** this: it falls back to real usage from
`character-stats`. The prerequisite buys the denominator, not the feature.

## Design

### 1. Typed failure carrying the provider's message

`ElevenLabsAccountClient.GetSubscriptionAsync` currently throws
`InvalidOperationException` for every non-success status, so callers cannot tell
"this key is not permitted" from "the network is down".

Add `ElevenLabsAccountAccessDeniedException`, thrown for **401** and **403**.
It carries the provider's own `detail.message` text when the body parses.

Rationale: ElevenLabs' message already names the exact missing permission
(`user_read`). Surfacing the provider's wording is more precise than inventing
our own, and does not rot if ElevenLabs renames a scope. Inventing a label is
the specific mistake that sent the user hunting for a "User: Read" toggle that
does not exist under that name.

Rules:

- Parse `detail.message` from the body; fall back to the raw body, then to the
  status code, if it is missing or unparseable.
- Building the error must never throw, whatever the body contains.
- Other non-success statuses keep the current `InvalidOperationException`.
- `ElevenLabsAccountAccessDeniedException` derives from `Exception`, not
  `InvalidOperationException`, so the two cases cannot be confused by a
  `catch (InvalidOperationException)`.
- The client remains the only component that knows about HTTP status codes.

### 2. Degrade to usage-only instead of failing

On `ElevenLabsAccountAccessDeniedException`, do **not** give up. Fall back to
`GET /v1/usage/character-stats` for the current billing window and show real
characters used, with no percentage.

A new `ElevenLabsUsageClient` owns that endpoint:

- `GetCharactersUsedAsync(apiKey, startUtc, endUtc)` returns the summed `usage.All`.
- It converts the window to **unix milliseconds**. Seconds return an empty
  `usage` object with a 200, so a seconds bug would silently report zero usage —
  the client must never pass seconds.
- It sums `usage.All` defensively: missing `usage`, missing `All`, a
  `time`/`usage` length mismatch, or non-numeric entries must yield a usable
  number rather than throw.

Without `user_read` there is no reset date, so the fallback window is the
trailing 30 days and must be labelled as such ("last 30 days") — never implied
to be a billing period.

`QuotaTracker` is unchanged. The fallback does not produce a `QuotaSnapshot`
(there is no limit, so `FractionUsed` would be meaningless); it is presented as
a separate usage-only state.

### 3. Show the state honestly

Three display states, chosen by what the data supports:

1. **Full** — `/v1/user/subscription` succeeded: meter with percentage, tier,
   reset date. Current behaviour.
2. **Usage-only** — access denied but `character-stats` succeeded: characters
   used over the last 30 days, no bar, plus the provider's message and one line
   on granting `user_read` to unlock the percentage.
3. **Unavailable** — both failed: the provider's message only.

In usage-only and unavailable states the compact meter (a percentage bar) stays
hidden even when `settings.ShowElevenLabsQuotaMeter` is true, because there is
no denominator to draw. A bar with no limit is a lie. The usage number lives in
the Speech Provider detail area.

The user's toggle setting is never overwritten — only visibility is suppressed.
If `user_read` is later granted, the next successful refresh restores the full
meter with no settings change.

### 4. Fix the quiet-flag bug

`WireQuotaMeter` calls `RefreshQuotaAsync(quiet: true)` at startup. The `quiet`
flag is honoured for the "Refreshing quota..." and API-key-read messages, but
the final `catch` ignores it, so a background refresh still writes
`Refresh failed: ...` into the UI.

Fix: background refreshes report nothing on failure; only a manual **Refresh**
reports. Access-denied is recorded regardless of `quiet`, because the meter must
hide itself in both paths.

## Testing

Unit tests, using the existing `StubHandler` pattern in
`ElevenLabsAccountClientTests`.

`ElevenLabsAccountClient`:

- 401 throws `ElevenLabsAccountAccessDeniedException`.
- 403 throws `ElevenLabsAccountAccessDeniedException`.
- The exception message contains the provider's `detail.message` text.
- A 401 with an unparseable body still throws the typed exception and does not
  throw while building the message.
- A non-401/403 failure (e.g. 500) still throws `InvalidOperationException`.
- The existing `GetSubscriptionAsyncThrowsOnUnauthorized` test must be updated:
  it asserts `InvalidOperationException`, and xUnit's `Assert.ThrowsAsync<T>`
  matches the exact type, so it will fail against the new exception.

`ElevenLabsUsageClient`:

- Sends `xi-api-key`, hits `/v1/usage/character-stats`, and passes the window as
  **milliseconds** — assert the actual query values, since seconds fail silently
  rather than erroring. This is the highest-value test here.
- Sums `usage.All` across buckets.
- Tolerates missing `usage`, missing `All`, non-numeric entries, and a
  `time`/`usage` length mismatch without throwing.
- Returns zero for an empty `usage` object (the seconds-window response shape).

`QuotaTracker` is untouched; its existing 9 tests stand.

Live verification with the current TTS-only key (possible today):

- Startup shows usage-only: real characters for the last 30 days, no bar, no
  error splash.
- Manual **Refresh** shows the provider's message plus the unlock line.

Live verification once `user_read` is granted:

- Manual **Refresh** populates tier, used, limit, and reset date.
- Speaking decrements the meter, and every fifth speech reconciles against the
  server.
- Reverting to the TTS-only key degrades to usage-only rather than erroring.

## Out of scope (YAGNI)

- No retry or backoff on quota refresh.
- No caching layer beyond the existing persisted snapshot.
- No changes to `QuotaTracker` or its computed fields.
- No persistence of the usage-only figure; it is fetched per refresh.
- No charting of the daily usage series. `character-stats` returns per-day
  buckets and the temptation to draw a graph is real, but the feature is a
  meter. Only the summed total is used.
- No handling for `voices_read`; the app calls no other account endpoint.
- No service-account support: multi-seat plans only, and it would not supply
  `character_limit` anyway.

## Definition of done

- `dotnet build CodeCompanionDesktop.sln` — 0 errors, 0 warnings.
- `dotnet test CodeCompanionDesktop.sln` — all green, including new tests.
- With a TTS-only key: usage-only state shows real characters for the last 30
  days, no bar, no raw 401 text, no error splash on startup.
- With a `user_read` key: the full meter shows real numbers and decrements on
  speech.
- With no network: the unavailable state, no crash.
- Branch merged to `main`.
