# ElevenLabs Quota Meter — Completion Spec

Date: 2026-07-17
Branch: `feature/elevenlabs-quota-meter`
Status: design approved, implementation pending

## Purpose

Finish and merge the ElevenLabs quota meter, which shows how much of the
ElevenLabs character allowance the current billing period has consumed.

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

## Prerequisite (user action)

The `user_read` permission must be granted to the ElevenLabs API key. The
feature cannot show real numbers without it. If the permission cannot be
granted, the meter will correctly and permanently show the explanation described
below instead of numbers.

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

### 2. UI shows the provider's message plus the fix

In `MainWindow.QuotaMeter.cs`, `RefreshQuotaAsync` catches
`ElevenLabsAccountAccessDeniedException` separately and renders:

- the provider's message, verbatim; and
- one short line stating the fix: the API key needs the named permission added
  in the ElevenLabs dashboard.

No invented scope labels, and no raw `Refresh failed: ...401...`.

### 3. Hide rather than mislead

On access-denied, the compact meter is hidden even when
`settings.ShowElevenLabsQuotaMeter` is true. A blank or stale meter is worse
than no meter. The explanation lives in the Speech Provider detail area.

The user's toggle setting is not overwritten — only the visibility is
suppressed while access is denied. If the permission is later granted, the next
successful refresh restores the meter with no settings change.

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
`ElevenLabsAccountClientTests`:

- 401 throws `ElevenLabsAccountAccessDeniedException`.
- 403 throws `ElevenLabsAccountAccessDeniedException`.
- The exception message contains the provider's `detail.message` text.
- A 401 with an unparseable body still throws the typed exception and does not
  throw while building the message.
- A non-401/403 failure (e.g. 500) still throws `InvalidOperationException`.
- The existing `GetSubscriptionAsyncThrowsOnUnauthorized` test must be updated:
  it asserts `InvalidOperationException`, and xUnit's `Assert.ThrowsAsync<T>`
  matches the exact type, so it will fail against the new exception.

`QuotaTracker` is untouched; its existing tests stand.

Live verification, once `user_read` is granted:

- Manual **Refresh** populates tier, used, limit, and reset date.
- Speaking decrements the meter, and every fifth speech reconciles against the
  server.
- Revoking the permission again shows the explanation rather than a raw error.

## Out of scope (YAGNI)

- No retry or backoff on quota refresh.
- No caching layer beyond the existing persisted snapshot.
- No changes to `QuotaTracker` or its computed fields.
- No handling for `voices_read`; the app calls no other account endpoint.
  `/v1/user/subscription` is the only blocked endpoint the app touches.

## Definition of done

- `dotnet build CodeCompanionDesktop.sln` — 0 errors, 0 warnings.
- `dotnet test CodeCompanionDesktop.sln` — all green, including new tests.
- With a TTS-only key: the meter hides and shows the provider's explanation; no
  raw 401 text; no error splash on startup.
- With a `user_read` key: the meter shows real numbers and decrements on speech.
- Branch merged to `main`.
