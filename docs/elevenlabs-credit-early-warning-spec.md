# ElevenLabs credit early warning and usage graph — spec

Status: designed, not implemented. Written 2026-08-06.

Extends `elevenlabs-quota-meter-spec.md`. The meter shipped on 2026-07-17 and
deliberately excluded a chart as YAGNI; this revisits that exclusion, and adds the
early warning that was requested at the same time.

## Purpose

**The warning is a purchasing trigger.** When it fires, the action is "buy credits
now", not "speak less".

This matters because the account cannot extend its own limit. Measured on
2026-08-06 from `/v1/user/subscription`:

```
can_extend_character_limit        = false
allowed_to_extend_character_limit = false
max_character_limit_extension     = 0
max_credit_limit_extension        = 0
current_overage                   = 0
```

At the limit, **TTS stops dead** — ElevenLabs does not bill overage and carry on.
So the failure mode is silence in the middle of a working day, and the only remedy
takes a manual purchase. A warning that arrives with a few days' notice is the
whole point of the feature.

## What already exists — do not rebuild

- `ElevenLabsUsageClient` calls `/v1/usage/character-stats`, which returns daily
  buckets shaped `{"time":[unix_ms,...],"usage":{"All":[chars,...]}}`. `SumUsage`
  reduces them to a single total and **discards the buckets**.
- `QuotaTracker` computes `Remaining`, `FractionUsed`, `PercentUsed`.
- `MainWindow.QuotaMeter.cs` renders three states (Full, Usage-only, Unavailable)
  and colours the bar by threshold: >=90% red, >=70% amber.
- `App.xaml.cs` owns a `Forms.NotifyIcon` and already calls `ShowBalloonTip`.

## Verified facts this design depends on

Established by probing the live API on 2026-08-06, not from documentation.

- **The daily buckets are authoritative.** Summed over the current period they
  equal `/v1/user/subscription`'s `character_count` exactly (157,404).
- **`character_limit` moves between billing periods** — 322,001 on 2026-07-17,
  195,494 on 2026-08-06. Read it live every time. Never cache it across periods,
  and never infer it from `tier`.
- **The window is unix milliseconds.** Seconds return HTTP 200 with an empty
  `usage` object: a silent zero, not an error.
- **All spend is one consumer** — TTS, voice *George*, `eleven_multilingual_v2`.
- **The key lacks `history_read`**, so `/v1/history` returns 401. Per-day
  attribution must come from `character-stats`, not the history endpoint.

## Forecast core

### `UsageDay`

`record UsageDay(DateOnly Date, long Characters)`.

### `ElevenLabsUsageClient.GetDailyUsageAsync`

Returns `IReadOnlyList<UsageDay>` parsed from the `time[]` and `usage{}` arrays.

`SumUsage` is left exactly as it is. The usage-only fallback still depends on it,
and its error handling was hard-won: `System.Text.Json` signals failure through
`JsonException`, `InvalidOperationException` **and** `ArgumentException`, and the
existing filter catches the category deliberately.

`GetDailyUsageAsync` inherits the same never-throw contract. Where `time[]` and the
usage array differ in length it reads the shorter of the two rather than throwing —
a length mismatch must degrade to fewer days, never to an exception.

### `QuotaForecast`

A pure class. Input: the usage days, the limit/used/reset values from
`QuotaSnapshot`, and the current instant passed in. No WPF, no HTTP, no ambient
clock — that is what makes it testable, and it answers the standing audit finding
that policy currently lives in the untestable UI layer.

Output: burn rate, projected dry date, survival budget, and whether to warn.

**Rules, and the reason for each:**

| Rule | Why |
| --- | --- |
| Burn rate is the mean of the **trailing 7 complete days** | Seven spans a whole week, so quiet weekend days are represented in proportion instead of skewing a shorter window. Daily spend has ranged 0 to 16,216 in one period; a shorter window flaps. |
| **Today's bucket is excluded** | It is a partial day. On 2026-08-06 it read 1,806 at 10:30 against a 9,919 mean. Including it drags the mean down and fires the warning late. |
| **Zero days are included** | 2026-07-22 was 0. Future quiet days are equally real; dropping zeros overstates burn. |
| Warn when the **projected dry date falls before the reset date** | Nothing else. A percentage is blind to rate: 85% with 20 days left is fine, 85% with 2 days left is an emergency. |

Survival budget, shown alongside, is `remaining / days-until-reset`.

**Worked example — real figures from 2026-08-06:**

```
trailing 7 complete days (30 Jul - 5 Aug):
  10,201  13,435  6,267  8,394  7,383  7,535  16,216   -> mean 9,919/day
remaining 38,090 / 9,919 = 3.8 days runway   -> dry ~10 Aug
reset 17 Aug -> dry falls before reset       -> WARN
survival budget = 38,090 / 11.1 days = 3,432/day
```

### Cases that yield no warning rather than a wrong one

- **Limit unreadable** (the `user_read` access-denied state). No denominator, so no
  projection. A projection without a denominator is the same lie as a bar without
  one, and the meter already refuses to draw that bar.
- **Burn rate zero.** No dry date exists.
- **Fewer than 3 complete days of data**, as immediately after a reset. A one- or
  two-day mean is precisely the flapping this design avoids.
- **Reset date already in the past.** The snapshot is stale; refresh before
  trusting it.

## Warning behaviour

### Fire once per billing period

`AppSettings` gains a persisted record holding the reset timestamp the warning last
fired for. The decision lives in a pure `QuotaWarningPolicy`.

Keying on the **period's reset unix** means a new billing period re-arms the
warning automatically: no expiry logic, no manual reset, and a restart mid-period
does not re-nag.

### Surfaces, in order

**1. Tray balloon.** Reuses `NotifyIcon.ShowBalloonTip` with `ToolTipIcon.Warning`.
It goes first because it is free and cannot fail for want of credits:

> Credits run out ~10 Aug, before your 17 Aug reset. Top up to avoid TTS stopping.

**2. Spoken warning.** One utterance, ~120 characters, headline front-loaded
because the engine speaks the opening of a message. Three constraints the ordinary
speech path does not provide:

- It **bypasses `SpeechCandidatePipeline`**. It is not an assistant message and
  must not be discarded by text-hash dedupe.
- If the busy latch is engaged it **retries once** when speech frees up, then gives
  up. The balloon has already landed, so a dropped utterance is not a lost warning.
- It speaks **only if `Remaining` is at least the utterance length plus 250
  characters** — one full `MaxSpeechTextLength` of headroom, so a normal spoken
  update can still follow the warning rather than the warning itself being the
  event that exhausts the quota. Below that threshold the balloon fires alone.
  Otherwise the app would try to spend credits it does not have and surface an
  error in place of a warning.

The warning costs one ~120-character utterance per billing period — about 0.06% of
the limit.

## Usage graph

`OxyPlot.Wpf`, pinned exactly at `2.2.0`.

Chosen over LiveCharts2 on measured dependency footprint, not preference. Restoring
each into a throwaway `net8.0-windows` WPF project on 2026-08-06:

| Package | Packages pulled | Native asset packages |
| --- | --- | --- |
| `OxyPlot.Wpf` 2.2.0 | 3 | 0 — fully managed |
| `LiveChartsCore.SkiaSharpView.WPF` 2.0.5 | 14 | 4 (SkiaSharp + HarfBuzz for Win32 *and* macOS, plus OpenTK/OpenGL) |

This is the application's first third-party dependency. For a Windows-only
distributed binary, LiveCharts2 drags in macOS native assets and an OpenGL control
to draw roughly thirty bars.

**Form.** A `ColumnSeries` of one bar per day for the current billing period, with
a `LineAnnotation` at the survival budget. Bars reuse the meter's existing
threshold palette so the chart and the bar speak the same colour language.

**Placement.** In the Quota Details block on the Status tab, below the existing
figures and above the status line.

**This is not the compact card, and the distinction matters.**
`ApplyQuotaCardVisibility()` governs `QuotaMeterCompactCard` only — the summary
the "Show quota meter" checkbox toggles. The chart lives in the Quota Details
block, which that method does not touch, so the chart manages its own visibility:
collapsed when there are no bars, visible when there are.

The existing rule still stands untouched for the compact card. Setting *that*
card's visibility anywhere other than `ApplyQuotaCardVisibility()` is what
previously let the toggle reveal an empty bar while access was denied. Do not
route the chart through it, and do not let the chart set the card's visibility.

**In the access-denied state the bars still draw and the budget line does not.**
Usage needs only the TTS key; only the line needs a limit. Show what is true, omit
what cannot be known.

**Days shown** are those of the current billing period. Query with `start_unix` set
to the **previous reset instant** (`NextReset` minus one month, the account's
`billing_period` being `monthly_period`) and the API honours the time of day, not
just the date: the first bucket is a partial day containing only post-reset spend.

This is verified, not assumed. Querying from the reset instant on 2026-08-06 summed
to exactly 157,404, matching `character_count`. The same query from midnight on the
reset day returned 10,375 more — the previous period's tail. Get the start instant
wrong and every figure derived from the series is silently inflated.

The bar geometry and colours are computed in `QuotaChartModel`, a plain class, so
they are testable without WPF. `MainWindow.QuotaGraph.cs` only turns that model
into a `PlotModel` and renders it.

## Fetch cadence

Daily buckets refresh on startup, on manual refresh, and **at most hourly**. Daily
totals do not move fast enough to justify more.

The existing every-5-speeches subscription reconcile is untouched.

## File layout

`MainWindow.QuotaMeter.cs` is 334 lines against the 400-line ceiling and must not
grow. New code lands in new files.

| File | Role | Touches WPF |
| --- | --- | --- |
| `ElevenLabs/UsageDay.cs` | `record(DateOnly, long)` | no |
| `ElevenLabs/QuotaForecast.cs` | burn rate, dry date, budget, warn decision | no |
| `ElevenLabs/QuotaWarningPolicy.cs` | fire-once-per-period decision | no |
| `ElevenLabs/QuotaChartModel.cs` | bar values, colours, budget line position | no |
| `MainWindow.QuotaGraph.cs` | builds the `PlotModel`, renders | yes, thin |
| `ElevenLabs/ElevenLabsUsageClient.cs` | extend with `GetDailyUsageAsync` | no |
| `Settings/AppSettings.cs` | persisted last-warned record | no |

## Testing

Every component that decides anything is a plain class, so all of it is reachable.

**`QuotaForecast`** — burn-rate mean; today's partial bucket excluded; zero days
included; fewer than 3 complete days yields no warning; the dry-before-reset
boundary on both sides; zero limit; zero burn; reset date in the past.

**`QuotaWarningPolicy`** — fires once within a period; re-arms when the reset
timestamp changes; survives a restart via the persisted record.

**`QuotaChartModel`** — bar heights and colours against thresholds; budget line
position; empty series; a series longer than the period.

**`ElevenLabsUsageClient.GetDailyUsageAsync`** — well-formed body; malformed body;
empty body; `time[]` longer than the usage array. Same adversarial bodies as
`SumUsage`, because it inherits the same never-throw contract.

The suite is 125/125 as of `99d6f9f`; these are additions to it.

## Out of scope

**The `character-cost` defect.** The meter's local prediction uses `text.Length`,
but the TTS response header `character-cost` is what ElevenLabs actually bills, and
they differ. The forecast reads server buckets, so local prediction accuracy cannot
affect the warning. Fixing it is a separate pass.

**Cutting speech volume.** `SpeechCandidatePipeline` has no volume gate — it speaks
every final assistant message that is not a duplicate. Adding one was considered on
2026-08-06 and rejected in favour of buying credits. This spec reports spend; it
does not change what gets spoken.
