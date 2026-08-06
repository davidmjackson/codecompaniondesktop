# Credit Early Warning and Usage Graph Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Warn the user in time to buy ElevenLabs credits before the quota runs
out, and show a daily usage chart that explains why.

**Architecture:** All decision logic lives in plain, WPF-free classes in
`src/CodeCompanionDesktop/ElevenLabs/` so it is unit-testable: `QuotaForecast`
computes the burn rate and projected dry date, `QuotaWarningPolicy` decides
whether to fire, `QuotaChartModel` computes bar geometry. Two thin new WPF partials
(`MainWindow.QuotaGraph.cs` and `MainWindow.QuotaWarning.cs`) render and surface
them; `MainWindow.QuotaMeter.cs` gains only five lines, because it is already at
334 of its 400-line ceiling. Data comes from daily buckets returned by
`/v1/usage/character-stats`, which `ElevenLabsUsageClient` already calls but
currently reduces to a single total.

**Tech Stack:** C# 12, .NET 8 (`net8.0-windows`), WPF, xunit 2.5.3,
OxyPlot.Wpf 2.2.0.

**Spec:** `docs/elevenlabs-credit-early-warning-spec.md`. Read it before starting.

## Global Constraints

- Target framework is `net8.0-windows`. `Nullable` and `ImplicitUsings` are
  enabled in both projects.
- **Build and test from PowerShell in the Windows checkout**, not WSL. WSL's
  dotnet cannot build WPF.
  - `dotnet build CodeCompanionDesktop.sln`
  - `dotnet test CodeCompanionDesktop.sln --no-build`
- **Close Code Companion Desktop before building.** A running
  `CodeCompanionDesktop.exe` locks the output files and the build fails.
- The suite is **125/125 green** at the branch point. It must stay green; every
  task adds tests.
- **Files must stay under 400 lines.** `MainWindow.QuotaMeter.cs` is at 334.
  Functions stay under 30 lines, 4 parameters, cyclomatic 10.
- **Pin package versions exactly.** `OxyPlot.Wpf` is `2.2.0`, not `2.*`.
- `OxyPlot.Wpf 2.2.0` is the only new dependency permitted by this plan. It is the
  application's first third-party package.
- **Do not work on `main`.** Work on `feat/credit-early-warning`, which already
  exists and holds the spec commit.
- The usage window passed to ElevenLabs is **unix milliseconds**. Seconds return
  HTTP 200 with an empty `usage` object — a silent zero, not an error.
- Test class naming follows the existing convention: `<ClassUnderTest>Tests`, in
  namespace `CodeCompanionDesktop.Tests.<Folder>`, methods PascalCase describing
  behaviour, `[Fact]` unless a `[Theory]` genuinely reduces duplication.

---

### Task 1: Daily usage buckets

Parse the daily buckets `ElevenLabsUsageClient` already receives and discards.

**Files:**
- Create: `src/CodeCompanionDesktop/ElevenLabs/UsageDay.cs`
- Modify: `src/CodeCompanionDesktop/ElevenLabs/ElevenLabsUsageClient.cs`
- Test: `tests/CodeCompanionDesktop.Tests/ElevenLabs/ElevenLabsUsageClientTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces:
  - `public sealed record UsageDay(DateOnly Date, long Characters)`
  - `public static IReadOnlyList<UsageDay> ElevenLabsUsageClient.ParseDailyUsage(string json)`
  - `public Task<IReadOnlyList<UsageDay>> ElevenLabsUsageClient.GetDailyUsageAsync(string apiKey, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken cancellationToken = default)`

**Do not change `SumUsage`.** The usage-only fallback path depends on it and its
exception filter is deliberate: `System.Text.Json` signals failure through
`JsonException`, `InvalidOperationException` *and* `ArgumentException`.

- [ ] **Step 1: Write the failing tests**

Insert these methods **inside the `ElevenLabsUsageClientTests` class body** in
`tests/CodeCompanionDesktop.Tests/ElevenLabs/ElevenLabsUsageClientTests.cs` —
after the last existing `[Fact]` method (`SumUsageIgnoresNonFiniteNumbers`) and
**before** the nested `private sealed class StubHandler : HttpMessageHandler`
declaration.

Do not append at the end of the file. The namespace is file-scoped, so members
placed after the test class's closing brace sit directly in the namespace:
`error CS0116: A namespace cannot directly contain members such as fields,
methods or statements`.

```csharp
[Fact]
public void ParseDailyUsagePairsTimestampsWithCharacters()
{
    // 1785974400000 = 2026-08-06T00:00:00Z, 1785888000000 = 2026-08-05T00:00:00Z
    const string json =
        "{\"time\":[1785888000000,1785974400000],\"usage\":{\"All\":[16216.0,1806.0]}}";

    var days = ElevenLabsUsageClient.ParseDailyUsage(json);

    Assert.Equal(2, days.Count);
    Assert.Equal(new DateOnly(2026, 8, 5), days[0].Date);
    Assert.Equal(16216, days[0].Characters);
    Assert.Equal(new DateOnly(2026, 8, 6), days[1].Date);
    Assert.Equal(1806, days[1].Characters);
}

[Fact]
public void ParseDailyUsageReadsShorterOfTheTwoArrays()
{
    // A length mismatch must degrade to fewer days, never throw.
    const string json =
        "{\"time\":[1785888000000,1785974400000,1786060800000],\"usage\":{\"All\":[10.0,20.0]}}";

    var days = ElevenLabsUsageClient.ParseDailyUsage(json);

    Assert.Equal(2, days.Count);
    Assert.Equal(20, days[1].Characters);
}

[Fact]
public void ParseDailyUsageFallsBackToTheFirstSeriesWhenAllIsAbsent()
{
    const string json =
        "{\"time\":[1785888000000],\"usage\":{\"George\":[4242.0]}}";

    var days = ElevenLabsUsageClient.ParseDailyUsage(json);

    Assert.Single(days);
    Assert.Equal(4242, days[0].Characters);
}

[Theory]
[InlineData("")]
[InlineData("   ")]
[InlineData("not json at all")]
[InlineData("[]")]
[InlineData("{\"usage\":{}}")]
[InlineData("{\"time\":[],\"usage\":{\"All\":[]}}")]
[InlineData("{\"time\":\"nonsense\",\"usage\":{\"All\":[1.0]}}")]
public void ParseDailyUsageReturnsEmptyRatherThanThrowing(string json)
{
    var days = ElevenLabsUsageClient.ParseDailyUsage(json);

    Assert.Empty(days);
}

[Fact]
public void ParseDailyUsageSkipsNonFiniteAndNegativeValues()
{
    const string json =
        "{\"time\":[1785888000000,1785974400000],\"usage\":{\"All\":[-5.0,7.0]}}";

    var days = ElevenLabsUsageClient.ParseDailyUsage(json);

    Assert.Equal(2, days.Count);
    Assert.Equal(0, days[0].Characters);
    Assert.Equal(7, days[1].Characters);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

From PowerShell in the Windows checkout:

```powershell
dotnet build CodeCompanionDesktop.sln
```

Expected: FAIL — `ParseDailyUsage` does not exist.

- [ ] **Step 3: Create `UsageDay`**

Create `src/CodeCompanionDesktop/ElevenLabs/UsageDay.cs`:

```csharp
using System;

namespace CodeCompanionDesktop.ElevenLabs;

/// <summary>
/// One day's billed characters, as reported by /v1/usage/character-stats.
/// The date is UTC, matching the bucket boundaries the API returns.
/// </summary>
public sealed record UsageDay(DateOnly Date, long Characters);
```

- [ ] **Step 4: Add the parser and the fetch method**

Add to `ElevenLabsUsageClient`, after `GetCharactersUsedAsync`:

```csharp
public async Task<IReadOnlyList<UsageDay>> GetDailyUsageAsync(
    string apiKey,
    DateTimeOffset startUtc,
    DateTimeOffset endUtc,
    CancellationToken cancellationToken = default)
{
    ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

    // Milliseconds, not seconds. Seconds return HTTP 200 with an empty usage
    // object, which would silently report no usage at all.
    var start = startUtc.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);
    var end = endUtc.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture);

    using var request = new HttpRequestMessage(
        HttpMethod.Get,
        $"/v1/usage/character-stats?start_unix={start}&end_unix={end}");
    request.Headers.Add("xi-api-key", apiKey);

    using var response = await httpClient.SendAsync(
        request,
        HttpCompletionOption.ResponseContentRead,
        cancellationToken);

    var body = await response.Content.ReadAsStringAsync(cancellationToken);

    if (!response.IsSuccessStatusCode)
    {
        throw new InvalidOperationException(
            $"ElevenLabs usage request failed ({(int)response.StatusCode} {response.ReasonPhrase}).");
    }

    return ParseDailyUsage(body);
}

/// <summary>
/// Pairs the "time" array with the usage series, element by element.
/// Shares SumUsage's never-throw contract: a malformed body yields an empty
/// list, because losing the chart must not break the surrounding refresh.
/// </summary>
public static IReadOnlyList<UsageDay> ParseDailyUsage(string json)
{
    if (string.IsNullOrWhiteSpace(json))
    {
        return Array.Empty<UsageDay>();
    }

    try
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.ValueKind != JsonValueKind.Object ||
            !root.TryGetProperty("time", out var time) ||
            time.ValueKind != JsonValueKind.Array ||
            !root.TryGetProperty("usage", out var usage) ||
            usage.ValueKind != JsonValueKind.Object)
        {
            return Array.Empty<UsageDay>();
        }

        var series = SelectSeries(usage);
        return series is null ? Array.Empty<UsageDay>() : PairDays(time, series.Value);
    }
    catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException)
    {
        // Same three failure types as SumUsage. See that method for why the
        // category is caught rather than each individual site.
        return Array.Empty<UsageDay>();
    }
}

private static JsonElement? SelectSeries(JsonElement usage)
{
    // "All" is the aggregate series. Any other single series is a breakdown.
    if (usage.TryGetProperty("All", out var all) && all.ValueKind == JsonValueKind.Array)
    {
        return all;
    }

    foreach (var candidate in usage.EnumerateObject())
    {
        if (candidate.Value.ValueKind == JsonValueKind.Array)
        {
            return candidate.Value;
        }
    }

    return null;
}

/// <summary>
/// Reads the shorter of the two arrays. A length mismatch must lose days, not
/// throw: the series is a nicety and the refresh around it is not.
/// </summary>
private static List<UsageDay> PairDays(JsonElement time, JsonElement series)
{
    var count = Math.Min(time.GetArrayLength(), series.GetArrayLength());
    var days = new List<UsageDay>(count);

    for (var index = 0; index < count; index++)
    {
        var stamp = time[index];
        if (stamp.ValueKind != JsonValueKind.Number || !stamp.TryGetInt64(out var milliseconds))
        {
            continue;
        }

        var date = DateOnly.FromDateTime(
            DateTimeOffset.FromUnixTimeMilliseconds(milliseconds).UtcDateTime);
        days.Add(new UsageDay(date, ReadCharacters(series[index])));
    }

    return days;
}

private static long ReadCharacters(JsonElement entry)
{
    if (entry.ValueKind == JsonValueKind.Number &&
        entry.TryGetDouble(out var value) &&
        double.IsFinite(value) &&
        value > 0)
    {
        return (long)Math.Round(value);
    }

    return 0;
}
```

Add `using System.Collections.Generic;` to the file's usings.

- [ ] **Step 5: Run the tests to verify they pass**

```powershell
dotnet build CodeCompanionDesktop.sln
```

Then:

```powershell
dotnet test CodeCompanionDesktop.sln --no-build
```

Expected: PASS, all previous tests still green.

- [ ] **Step 6: Commit**

```bash
git add src/CodeCompanionDesktop/ElevenLabs/UsageDay.cs src/CodeCompanionDesktop/ElevenLabs/ElevenLabsUsageClient.cs tests/CodeCompanionDesktop.Tests/ElevenLabs/ElevenLabsUsageClientTests.cs
git commit -m "feat(quota): parse daily usage buckets"
```

---

### Task 2: Forecast

Burn rate, projected dry date, survival budget, and the warn decision.

**Files:**
- Create: `src/CodeCompanionDesktop/ElevenLabs/QuotaForecast.cs`
- Test: `tests/CodeCompanionDesktop.Tests/ElevenLabs/QuotaForecastTests.cs`

**Interfaces:**
- Consumes: `UsageDay` from Task 1; `QuotaSnapshot` from
  `src/CodeCompanionDesktop/ElevenLabs/QuotaTracker.cs` (existing — properties
  `CharacterCount`, `CharacterLimit`, `NextReset`, `Remaining`).
- Produces:
  - `public sealed record QuotaForecast(double BurnRatePerDay, DateOnly? ProjectedDryDate, double SurvivalBudgetPerDay, bool ShouldWarn)`
  - `public static QuotaForecast QuotaForecast.Create(IReadOnlyList<UsageDay> usageDays, QuotaSnapshot snapshot, DateTimeOffset now)`
  - `public const int QuotaForecast.BurnRateWindowDays = 7`
  - `public const int QuotaForecast.MinimumCompleteDays = 3`
  - `public static readonly QuotaForecast QuotaForecast.None`

- [ ] **Step 1: Write the failing tests**

Create `tests/CodeCompanionDesktop.Tests/ElevenLabs/QuotaForecastTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using CodeCompanionDesktop.ElevenLabs;

namespace CodeCompanionDesktop.Tests.ElevenLabs;

public sealed class QuotaForecastTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 8, 6, 10, 30, 0, TimeSpan.Zero);

    private static QuotaSnapshot Snapshot(long used = 157_404, long limit = 195_494)
    {
        return new QuotaSnapshot(
            used,
            limit,
            new DateTimeOffset(2026, 8, 17, 11, 59, 33, TimeSpan.Zero),
            "creator",
            Now,
            QuotaSnapshotSource.Server);
    }

    // The seven complete days before 2026-08-06, taken from real API data.
    private static List<UsageDay> SevenRealDays()
    {
        return new List<UsageDay>
        {
            new(new DateOnly(2026, 7, 30), 10_201),
            new(new DateOnly(2026, 7, 31), 13_435),
            new(new DateOnly(2026, 8, 1), 6_267),
            new(new DateOnly(2026, 8, 2), 8_394),
            new(new DateOnly(2026, 8, 3), 7_383),
            new(new DateOnly(2026, 8, 4), 7_535),
            new(new DateOnly(2026, 8, 5), 16_216),
        };
    }

    [Fact]
    public void BurnRateIsTheMeanOfTheTrailingCompleteDays()
    {
        var forecast = QuotaForecast.Create(SevenRealDays(), Snapshot(), Now);

        // 69,431 / 7
        Assert.Equal(9918.71, forecast.BurnRatePerDay, 2);
    }

    [Fact]
    public void TodaysPartialBucketIsExcludedFromTheBurnRate()
    {
        var days = SevenRealDays();
        days.Add(new UsageDay(new DateOnly(2026, 8, 6), 1_806));

        var forecast = QuotaForecast.Create(days, Snapshot(), Now);

        // Unchanged: today's low partial figure must not drag the mean down.
        Assert.Equal(9918.71, forecast.BurnRatePerDay, 2);
    }

    [Fact]
    public void OnlyTheMostRecentSevenCompleteDaysCount()
    {
        var days = new List<UsageDay> { new(new DateOnly(2026, 7, 29), 1_000_000) };
        days.AddRange(SevenRealDays());

        var forecast = QuotaForecast.Create(days, Snapshot(), Now);

        Assert.Equal(9918.71, forecast.BurnRatePerDay, 2);
    }

    [Fact]
    public void ZeroDaysAreIncludedInTheMean()
    {
        var days = new List<UsageDay>
        {
            new(new DateOnly(2026, 8, 3), 0),
            new(new DateOnly(2026, 8, 4), 0),
            new(new DateOnly(2026, 8, 5), 300),
        };

        var forecast = QuotaForecast.Create(days, Snapshot(), Now);

        Assert.Equal(100d, forecast.BurnRatePerDay, 2);
    }

    [Fact]
    public void WarnsWhenProjectedDryDateFallsBeforeTheReset()
    {
        var forecast = QuotaForecast.Create(SevenRealDays(), Snapshot(), Now);

        Assert.True(forecast.ShouldWarn);
        Assert.Equal(new DateOnly(2026, 8, 10), forecast.ProjectedDryDate);
    }

    [Fact]
    public void DoesNotWarnWhenTheProjectionReachesTheReset()
    {
        // 500/day against 38,090 remaining lasts far beyond 17 Aug.
        var days = new List<UsageDay>
        {
            new(new DateOnly(2026, 8, 3), 500),
            new(new DateOnly(2026, 8, 4), 500),
            new(new DateOnly(2026, 8, 5), 500),
        };

        var forecast = QuotaForecast.Create(days, Snapshot(), Now);

        Assert.False(forecast.ShouldWarn);
        Assert.NotNull(forecast.ProjectedDryDate);
    }

    [Fact]
    public void SurvivalBudgetIsRemainingOverDaysUntilReset()
    {
        var forecast = QuotaForecast.Create(SevenRealDays(), Snapshot(), Now);

        // 38,090 remaining over 11.0621875 days = 3443.26
        Assert.Equal(3443d, forecast.SurvivalBudgetPerDay, 0);
    }

    [Fact]
    public void FewerThanThreeCompleteDaysProducesNoWarning()
    {
        var days = new List<UsageDay>
        {
            new(new DateOnly(2026, 8, 4), 20_000),
            new(new DateOnly(2026, 8, 5), 20_000),
        };

        var forecast = QuotaForecast.Create(days, Snapshot(), Now);

        Assert.False(forecast.ShouldWarn);
        Assert.Null(forecast.ProjectedDryDate);
        Assert.Equal(0d, forecast.BurnRatePerDay);
    }

    [Fact]
    public void NoUsageDataProducesNoWarning()
    {
        var forecast = QuotaForecast.Create(Array.Empty<UsageDay>(), Snapshot(), Now);

        Assert.False(forecast.ShouldWarn);
        Assert.Null(forecast.ProjectedDryDate);
    }

    [Fact]
    public void ZeroLimitProducesNoWarning()
    {
        var forecast = QuotaForecast.Create(SevenRealDays(), Snapshot(limit: 0), Now);

        Assert.False(forecast.ShouldWarn);
        Assert.Null(forecast.ProjectedDryDate);
    }

    [Fact]
    public void ZeroBurnProducesNoDryDateAndNoWarning()
    {
        var days = new List<UsageDay>
        {
            new(new DateOnly(2026, 8, 3), 0),
            new(new DateOnly(2026, 8, 4), 0),
            new(new DateOnly(2026, 8, 5), 0),
        };

        var forecast = QuotaForecast.Create(days, Snapshot(), Now);

        Assert.Equal(0d, forecast.BurnRatePerDay);
        Assert.Null(forecast.ProjectedDryDate);
        Assert.False(forecast.ShouldWarn);
    }

    [Fact]
    public void AResetDateInThePastProducesNoWarning()
    {
        var stale = new QuotaSnapshot(
            157_404,
            195_494,
            new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero),
            "creator",
            Now,
            QuotaSnapshotSource.Server);

        var forecast = QuotaForecast.Create(SevenRealDays(), stale, Now);

        Assert.False(forecast.ShouldWarn);
    }

    [Fact]
    public void AnExhaustedQuotaStillWarnsBecauseTheActionIsStillToTopUp()
    {
        // Remaining is 0, so the runway is 0 and the dry date is today - which is
        // before the reset, so it warns. That is deliberate: at zero the required
        // action ("buy credits") is exactly what it is at 10%. The spoken warning
        // is separately suppressed here by the headroom check in
        // MainWindow.QuotaMeter.cs, because there are no credits left to speak it.
        var forecast = QuotaForecast.Create(SevenRealDays(), Snapshot(used: 195_494), Now);

        Assert.True(forecast.ShouldWarn);
        Assert.Equal(new DateOnly(2026, 8, 6), forecast.ProjectedDryDate);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```powershell
dotnet build CodeCompanionDesktop.sln
```

Expected: FAIL — `QuotaForecast` does not exist.

- [ ] **Step 3: Implement `QuotaForecast`**

Create `src/CodeCompanionDesktop/ElevenLabs/QuotaForecast.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeCompanionDesktop.ElevenLabs;

/// <summary>
/// Projects when the ElevenLabs quota runs out, and decides whether that is soon
/// enough to warn about.
///
/// The warning is a PURCHASING trigger, not a slow-down trigger: this account
/// cannot extend its own character limit, so reaching it stops speech dead rather
/// than billing overage. See docs/elevenlabs-credit-early-warning-spec.md.
///
/// Pure by design - no clock, no HTTP, no WPF - so every rule below is testable.
/// </summary>
public sealed record QuotaForecast(
    double BurnRatePerDay,
    DateOnly? ProjectedDryDate,
    double SurvivalBudgetPerDay,
    bool ShouldWarn)
{
    /// <summary>
    /// Seven days spans a whole week, so quiet weekend days are represented in
    /// proportion. Daily spend has ranged from 0 to 16,216 within one billing
    /// period; a shorter window makes the warning flap.
    /// </summary>
    public const int BurnRateWindowDays = 7;

    /// <summary>
    /// Below this, a mean is noise rather than a rate. Reached immediately after
    /// a reset, when warning on one day's spend would be meaningless.
    /// </summary>
    public const int MinimumCompleteDays = 3;

    public static readonly QuotaForecast None = new(0d, null, 0d, false);

    public static QuotaForecast Create(
        IReadOnlyList<UsageDay> usageDays,
        QuotaSnapshot snapshot,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(usageDays);
        ArgumentNullException.ThrowIfNull(snapshot);

        var daysUntilReset = (snapshot.NextReset - now).TotalDays;
        if (snapshot.CharacterLimit <= 0 || daysUntilReset <= 0)
        {
            return None;
        }

        var burnRate = CalculateBurnRate(usageDays, DateOnly.FromDateTime(now.UtcDateTime));
        var survivalBudget = snapshot.Remaining / daysUntilReset;

        if (burnRate <= 0)
        {
            return new QuotaForecast(0d, null, survivalBudget, false);
        }

        var runwayDays = snapshot.Remaining / burnRate;
        var dryInstant = now.AddDays(runwayDays);

        return new QuotaForecast(
            burnRate,
            DateOnly.FromDateTime(dryInstant.UtcDateTime),
            survivalBudget,
            dryInstant < snapshot.NextReset);
    }

    /// <summary>
    /// Mean characters per day over the trailing complete days. Today is excluded:
    /// its bucket is a partial day, and including it drags the mean down and fires
    /// the warning late.
    /// </summary>
    private static double CalculateBurnRate(IReadOnlyList<UsageDay> usageDays, DateOnly today)
    {
        var completeDays = usageDays
            .Where(day => day.Date < today)
            .OrderBy(day => day.Date)
            .TakeLast(BurnRateWindowDays)
            .ToList();

        if (completeDays.Count < MinimumCompleteDays)
        {
            return 0d;
        }

        return completeDays.Average(day => (double)day.Characters);
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```powershell
dotnet build CodeCompanionDesktop.sln
```

Then:

```powershell
dotnet test CodeCompanionDesktop.sln --no-build
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/CodeCompanionDesktop/ElevenLabs/QuotaForecast.cs tests/CodeCompanionDesktop.Tests/ElevenLabs/QuotaForecastTests.cs
git commit -m "feat(quota): project the dry date from a trailing burn rate"
```

---

### Task 3: Fire-once warning policy

Decide whether to fire, and remember that it fired.

**Files:**
- Create: `src/CodeCompanionDesktop/ElevenLabs/QuotaWarningPolicy.cs`
- Modify: `src/CodeCompanionDesktop/Settings/AppSettings.cs`
- Test: `tests/CodeCompanionDesktop.Tests/ElevenLabs/QuotaWarningPolicyTests.cs`
- Test: `tests/CodeCompanionDesktop.Tests/Settings/AppSettingsTests.cs`

**Interfaces:**
- Consumes: `QuotaForecast` from Task 2.
- Produces:
  - `public static bool QuotaWarningPolicy.ShouldFire(QuotaForecast forecast, long periodResetUnix, long? lastWarnedPeriodResetUnix)`
  - `public static string QuotaWarningPolicy.BuildBalloonMessage(QuotaForecast forecast, DateTimeOffset resetLocal)`
  - `public static string QuotaWarningPolicy.BuildSpokenMessage(QuotaForecast forecast, DateTimeOffset resetLocal)`
  - `public long? AppSettings.LastQuotaWarningPeriodResetUnix { get; set; }`

- [ ] **Step 1: Write the failing tests**

Create `tests/CodeCompanionDesktop.Tests/ElevenLabs/QuotaWarningPolicyTests.cs`:

```csharp
using System;
using CodeCompanionDesktop.ElevenLabs;

namespace CodeCompanionDesktop.Tests.ElevenLabs;

public sealed class QuotaWarningPolicyTests
{
    private const long PeriodReset = 1_786_964_373;

    private static readonly QuotaForecast Warning =
        new(9_919d, new DateOnly(2026, 8, 10), 3_444d, ShouldWarn: true);

    private static readonly DateTimeOffset ResetLocal =
        new(2026, 8, 17, 12, 59, 33, TimeSpan.FromHours(1));

    [Fact]
    public void FiresWhenTheForecastWarnsAndNothingHasFiredYet()
    {
        Assert.True(QuotaWarningPolicy.ShouldFire(Warning, PeriodReset, null));
    }

    [Fact]
    public void DoesNotFireTwiceInTheSamePeriod()
    {
        Assert.False(QuotaWarningPolicy.ShouldFire(Warning, PeriodReset, PeriodReset));
    }

    [Fact]
    public void ReArmsWhenTheBillingPeriodChanges()
    {
        const long nextPeriod = 1_789_642_773;

        Assert.True(QuotaWarningPolicy.ShouldFire(Warning, nextPeriod, PeriodReset));
    }

    [Fact]
    public void DoesNotFireWhenTheForecastDoesNotWarn()
    {
        Assert.False(QuotaWarningPolicy.ShouldFire(QuotaForecast.None, PeriodReset, null));
    }

    [Fact]
    public void BalloonMessageNamesBothDatesAndTheAction()
    {
        var message = QuotaWarningPolicy.BuildBalloonMessage(Warning, ResetLocal);

        Assert.Contains("10 Aug", message, StringComparison.Ordinal);
        Assert.Contains("17 Aug", message, StringComparison.Ordinal);
        Assert.Contains("Top up", message, StringComparison.Ordinal);
    }

    [Fact]
    public void SpokenMessageFitsTheSpokenBudget()
    {
        var message = QuotaWarningPolicy.BuildSpokenMessage(Warning, ResetLocal);

        Assert.InRange(message.Length, 60, 160);
    }

    [Fact]
    public void SpokenMessageLeadsWithTheHeadline()
    {
        var message = QuotaWarningPolicy.BuildSpokenMessage(Warning, ResetLocal);

        // The engine speaks the opening of a message, so the action comes first.
        Assert.StartsWith("Voice credits", message, StringComparison.Ordinal);
    }

    [Fact]
    public void MessagesFallBackToTheResetDateWhenNoDryDateExists()
    {
        var noDryDate = new QuotaForecast(0d, null, 0d, ShouldWarn: true);

        var balloon = QuotaWarningPolicy.BuildBalloonMessage(noDryDate, ResetLocal);
        var spoken = QuotaWarningPolicy.BuildSpokenMessage(noDryDate, ResetLocal);

        Assert.Contains("soon", balloon, StringComparison.Ordinal);
        Assert.Contains("soon", spoken, StringComparison.Ordinal);
    }
}
```

Insert these methods **inside the `AppSettingsTests` class body** in
`tests/CodeCompanionDesktop.Tests/Settings/AppSettingsTests.cs`, after the last
existing `[Fact]` method
(`NormalizeFallsBackToSilentForAnUnknownSelfTestPlaybackValue`) and before the
class's closing brace — not after it, which is `CS0116`.

```csharp
[Fact]
public void LastQuotaWarningPeriodResetUnixDefaultsToNull()
{
    var settings = new AppSettings();

    Assert.Null(settings.LastQuotaWarningPeriodResetUnix);
}

[Fact]
public void NormalizeKeepsLastQuotaWarningPeriodResetUnix()
{
    var settings = new AppSettings { LastQuotaWarningPeriodResetUnix = 1_786_964_373 };

    settings.Normalize();

    Assert.Equal(1_786_964_373, settings.LastQuotaWarningPeriodResetUnix);
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```powershell
dotnet build CodeCompanionDesktop.sln
```

Expected: FAIL — `QuotaWarningPolicy` and the setting do not exist.

- [ ] **Step 3: Add the setting**

In `src/CodeCompanionDesktop/Settings/AppSettings.cs`, immediately after the
`LastKnownElevenLabsQuota` property:

```csharp
    /// <summary>
    /// The billing period the credit warning last fired for, as the period's
    /// reset timestamp. Keyed this way so a new billing period re-arms the
    /// warning with no expiry logic, and a restart mid-period does not re-nag.
    /// </summary>
    public long? LastQuotaWarningPeriodResetUnix { get; set; }
```

Do not add anything to `Normalize()`. A nullable long has no invalid state to
correct.

- [ ] **Step 4: Implement the policy**

Create `src/CodeCompanionDesktop/ElevenLabs/QuotaWarningPolicy.cs`:

```csharp
using System;

namespace CodeCompanionDesktop.ElevenLabs;

/// <summary>
/// Decides whether the credit warning fires, and writes its wording.
///
/// The warning fires once per billing period. It is keyed on the period's reset
/// timestamp so a new period re-arms it automatically: no expiry logic, and a
/// restart mid-period does not warn again.
/// </summary>
public static class QuotaWarningPolicy
{
    public static bool ShouldFire(
        QuotaForecast forecast,
        long periodResetUnix,
        long? lastWarnedPeriodResetUnix)
    {
        ArgumentNullException.ThrowIfNull(forecast);

        return forecast.ShouldWarn && lastWarnedPeriodResetUnix != periodResetUnix;
    }

    public static string BuildBalloonMessage(QuotaForecast forecast, DateTimeOffset resetLocal)
    {
        ArgumentNullException.ThrowIfNull(forecast);

        return $"Credits run out {DescribeDryDate(forecast)}, before your "
            + $"{resetLocal:d MMM} reset. Top up to avoid speech stopping.";
    }

    public static string BuildSpokenMessage(QuotaForecast forecast, DateTimeOffset resetLocal)
    {
        ArgumentNullException.ThrowIfNull(forecast);

        // Front-loaded: the engine speaks the opening of a message, so the
        // headline and the action come before the detail.
        return $"Voice credits run out {DescribeDryDate(forecast)}, before the "
            + $"{resetLocal:d MMMM} reset. Top up when you can.";
    }

    private static string DescribeDryDate(QuotaForecast forecast)
    {
        return forecast.ProjectedDryDate is DateOnly dry
            ? $"around {dry:d MMMM}"
            : "soon";
    }
}
```

- [ ] **Step 5: Run the tests to verify they pass**

```powershell
dotnet build CodeCompanionDesktop.sln
```

Then:

```powershell
dotnet test CodeCompanionDesktop.sln --no-build
```

Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/CodeCompanionDesktop/ElevenLabs/QuotaWarningPolicy.cs src/CodeCompanionDesktop/Settings/AppSettings.cs tests/CodeCompanionDesktop.Tests/ElevenLabs/QuotaWarningPolicyTests.cs tests/CodeCompanionDesktop.Tests/Settings/AppSettingsTests.cs
git commit -m "feat(quota): fire the credit warning once per billing period"
```

---

### Task 4: Chart model

Bar values, colours and the budget line — computed without WPF so they are testable.

**Files:**
- Create: `src/CodeCompanionDesktop/ElevenLabs/QuotaChartModel.cs`
- Test: `tests/CodeCompanionDesktop.Tests/ElevenLabs/QuotaChartModelTests.cs`

**Interfaces:**
- Consumes: `UsageDay` from Task 1.
- Produces:
  - `public enum QuotaChartBarLevel { Normal, Warning, Critical }`
  - `public sealed record QuotaChartBar(DateOnly Date, long Characters, QuotaChartBarLevel Level)`
  - `public sealed record QuotaChartModel(IReadOnlyList<QuotaChartBar> Bars, double? BudgetLine)`
  - `public static QuotaChartModel QuotaChartModel.Create(IReadOnlyList<UsageDay> usageDays, double? survivalBudgetPerDay)`
  - `public static readonly QuotaChartModel QuotaChartModel.Empty`

- [ ] **Step 1: Write the failing tests**

Create `tests/CodeCompanionDesktop.Tests/ElevenLabs/QuotaChartModelTests.cs`:

```csharp
using System;
using System.Collections.Generic;
using CodeCompanionDesktop.ElevenLabs;

namespace CodeCompanionDesktop.Tests.ElevenLabs;

public sealed class QuotaChartModelTests
{
    private static List<UsageDay> Days()
    {
        return new List<UsageDay>
        {
            new(new DateOnly(2026, 8, 3), 3_000),
            new(new DateOnly(2026, 8, 4), 5_000),
            new(new DateOnly(2026, 8, 5), 16_216),
        };
    }

    [Fact]
    public void BarsKeepDateOrderAndCharacters()
    {
        var model = QuotaChartModel.Create(Days(), 5_000d);

        Assert.Equal(3, model.Bars.Count);
        Assert.Equal(new DateOnly(2026, 8, 3), model.Bars[0].Date);
        Assert.Equal(16_216, model.Bars[2].Characters);
    }

    [Fact]
    public void BarsAreSortedByDateEvenWhenTheSeriesIsNot()
    {
        var unsorted = new List<UsageDay>
        {
            new(new DateOnly(2026, 8, 5), 1),
            new(new DateOnly(2026, 8, 3), 2),
        };

        var model = QuotaChartModel.Create(unsorted, null);

        Assert.Equal(new DateOnly(2026, 8, 3), model.Bars[0].Date);
    }

    [Fact]
    public void BelowBudgetIsNormalAtOrAboveIsWarningDoubleIsCritical()
    {
        var model = QuotaChartModel.Create(Days(), 5_000d);

        Assert.Equal(QuotaChartBarLevel.Normal, model.Bars[0].Level);
        Assert.Equal(QuotaChartBarLevel.Warning, model.Bars[1].Level);
        Assert.Equal(QuotaChartBarLevel.Critical, model.Bars[2].Level);
    }

    [Fact]
    public void EveryBarIsNormalWhenThereIsNoBudgetLine()
    {
        var model = QuotaChartModel.Create(Days(), null);

        Assert.All(model.Bars, bar => Assert.Equal(QuotaChartBarLevel.Normal, bar.Level));
        Assert.Null(model.BudgetLine);
    }

    [Fact]
    public void ANonPositiveBudgetIsTreatedAsNoBudgetLine()
    {
        var model = QuotaChartModel.Create(Days(), 0d);

        Assert.Null(model.BudgetLine);
        Assert.All(model.Bars, bar => Assert.Equal(QuotaChartBarLevel.Normal, bar.Level));
    }

    [Fact]
    public void AnEmptySeriesProducesNoBars()
    {
        var model = QuotaChartModel.Create(Array.Empty<UsageDay>(), 5_000d);

        Assert.Empty(model.Bars);
    }

    [Fact]
    public void EmptyHasNoBarsAndNoBudgetLine()
    {
        Assert.Empty(QuotaChartModel.Empty.Bars);
        Assert.Null(QuotaChartModel.Empty.BudgetLine);
    }
}
```

- [ ] **Step 2: Run the tests to verify they fail**

```powershell
dotnet build CodeCompanionDesktop.sln
```

Expected: FAIL — `QuotaChartModel` does not exist.

- [ ] **Step 3: Implement the chart model**

Create `src/CodeCompanionDesktop/ElevenLabs/QuotaChartModel.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;

namespace CodeCompanionDesktop.ElevenLabs;

public enum QuotaChartBarLevel
{
    Normal,
    Warning,
    Critical,
}

public sealed record QuotaChartBar(DateOnly Date, long Characters, QuotaChartBarLevel Level);

/// <summary>
/// The daily usage chart, computed without WPF so the geometry and the colour
/// thresholds are testable. MainWindow.QuotaGraph.cs turns this into a PlotModel.
///
/// Where the plan limit is unreadable there is no budget line, but the bars are
/// still true - usage needs only the text-to-speech key. Show what is known, omit
/// what is not.
/// </summary>
public sealed record QuotaChartModel(IReadOnlyList<QuotaChartBar> Bars, double? BudgetLine)
{
    public static readonly QuotaChartModel Empty =
        new(Array.Empty<QuotaChartBar>(), null);

    public static QuotaChartModel Create(
        IReadOnlyList<UsageDay> usageDays,
        double? survivalBudgetPerDay)
    {
        ArgumentNullException.ThrowIfNull(usageDays);

        var budget = survivalBudgetPerDay is > 0 ? survivalBudgetPerDay : null;

        var bars = usageDays
            .OrderBy(day => day.Date)
            .Select(day => new QuotaChartBar(day.Date, day.Characters, Classify(day.Characters, budget)))
            .ToList();

        return new QuotaChartModel(bars, budget);
    }

    /// <summary>
    /// At or over the survival budget is amber; double it is red. Same language as
    /// the quota bar's existing 70/90 thresholds: over budget means this day is
    /// spending the reset's headroom.
    /// </summary>
    private static QuotaChartBarLevel Classify(long characters, double? budget)
    {
        if (budget is not double line)
        {
            return QuotaChartBarLevel.Normal;
        }

        if (characters >= line * 2)
        {
            return QuotaChartBarLevel.Critical;
        }

        return characters >= line ? QuotaChartBarLevel.Warning : QuotaChartBarLevel.Normal;
    }
}
```

- [ ] **Step 4: Run the tests to verify they pass**

```powershell
dotnet build CodeCompanionDesktop.sln
```

Then:

```powershell
dotnet test CodeCompanionDesktop.sln --no-build
```

Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/CodeCompanionDesktop/ElevenLabs/QuotaChartModel.cs tests/CodeCompanionDesktop.Tests/ElevenLabs/QuotaChartModelTests.cs
git commit -m "feat(quota): compute usage chart bars and budget line"
```

---

### Task 5: Render the chart

Add OxyPlot and draw the model.

**Files:**
- Modify: `src/CodeCompanionDesktop/CodeCompanionDesktop.csproj`
- Modify: `src/CodeCompanionDesktop/MainWindow.xaml` (after `QuotaDetailAsOfText`, line 272-275)
- Create: `src/CodeCompanionDesktop/MainWindow.QuotaGraph.cs`

**Interfaces:**
- Consumes: `QuotaChartModel`, `QuotaChartBar`, `QuotaChartBarLevel` from Task 4.
- Produces:
  - `private void MainWindow.RenderQuotaGraph(QuotaChartModel model)`
  - XAML element `QuotaUsagePlot` (`OxyPlot.Wpf.PlotView`)

There are no unit tests in this task: it is rendering only, and every value it
draws was tested in Task 4. It is verified by building and looking at the app.

- [ ] **Step 1: Add the package reference**

In `src/CodeCompanionDesktop/CodeCompanionDesktop.csproj`, add a new `ItemGroup`
after the `InternalsVisibleTo` group:

```xml
  <ItemGroup>
    <PackageReference Include="OxyPlot.Wpf" Version="2.2.0" />
  </ItemGroup>
```

The version is pinned exactly. Do not use a floating range.

- [ ] **Step 2: Verify the package restores and pulls nothing native**

```powershell
dotnet restore CodeCompanionDesktop.sln
```

Expected: succeeds. `OxyPlot.Wpf` brings exactly `OxyPlot.Core` and
`OxyPlot.Wpf.Shared`, and no native asset packages.

- [ ] **Step 3: Add the PlotView to the XAML**

Add the OxyPlot namespace to the root `<Window>` element in
`src/CodeCompanionDesktop/MainWindow.xaml`:

```xml
xmlns:oxy="http://oxyplot.org/wpf"
```

Then insert this immediately after the `QuotaDetailAsOfText` `TextBlock`
(currently ending at line 275) and before `QuotaDetailStatusText`:

```xml
                                <oxy:PlotView x:Name="QuotaUsagePlot"
                                              Height="180"
                                              Margin="0,12,0,0"
                                              Background="Transparent"
                                              Visibility="Collapsed" />
```

- [ ] **Step 4: Implement the renderer**

Create `src/CodeCompanionDesktop/MainWindow.QuotaGraph.cs`:

```csharp
using System;
using System.Globalization;
using System.Windows;
using CodeCompanionDesktop.ElevenLabs;
using OxyPlot;
using OxyPlot.Annotations;
using OxyPlot.Axes;
using OxyPlot.Series;

namespace CodeCompanionDesktop;

/// <summary>
/// Draws the daily usage chart. Geometry and colours come from QuotaChartModel,
/// which is tested without WPF; this file only turns that into a PlotModel.
/// </summary>
public partial class MainWindow
{
    private static readonly OxyColor NormalBarColor = OxyColor.FromRgb(60, 179, 113);
    private static readonly OxyColor WarningBarColor = OxyColor.FromRgb(218, 165, 32);
    private static readonly OxyColor CriticalBarColor = OxyColor.FromRgb(205, 92, 92);

    /// <summary>Half a column's width, in category units.</summary>
    private const double ColumnHalfWidth = 0.35;

    private void RenderQuotaGraph(QuotaChartModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        if (model.Bars.Count == 0)
        {
            QuotaUsagePlot.Visibility = Visibility.Collapsed;
            QuotaUsagePlot.Model = null;
            return;
        }

        var plot = new PlotModel
        {
            PlotAreaBorderThickness = new OxyThickness(0, 0, 0, 1),
            PlotAreaBorderColor = OxyColors.LightGray,
        };

        plot.Axes.Add(BuildCategoryAxis(model));
        plot.Axes.Add(BuildValueAxis());
        plot.Series.Add(BuildBarSeries(model));
        AddBudgetLine(plot, model.BudgetLine);

        QuotaUsagePlot.Model = plot;
        QuotaUsagePlot.Visibility = Visibility.Visible;
    }

    /// <summary>
    /// Labels are added explicitly rather than via ItemsSource/LabelField:
    /// DateOnly does not format reliably through the axis StringFormat path.
    /// </summary>
    private static CategoryAxis BuildCategoryAxis(QuotaChartModel model)
    {
        var axis = new CategoryAxis
        {
            Position = AxisPosition.Bottom,
            IsTickCentered = true,
            GapWidth = 0.3,
        };

        foreach (var bar in model.Bars)
        {
            axis.Labels.Add(bar.Date.ToString("d MMM", CultureInfo.CurrentCulture));
        }

        return axis;
    }

    private static LinearAxis BuildValueAxis()
    {
        return new LinearAxis
        {
            Position = AxisPosition.Left,
            Minimum = 0,
            StringFormat = "#,0",
            MajorGridlineStyle = LineStyle.Dot,
        };
    }

    /// <summary>
    /// RectangleBarSeries. OxyPlot 2.2.0 ships no ColumnSeries (it was a 1.x type),
    /// and its BarSeries is horizontal-only: BarSeriesBase.GetCategoryAxis() throws
    /// "BarSeries requires a CategoryAxis on the Y Axis", which PlotModel.Update
    /// swallows into GetLastPlotException and the view paints as
    /// "OxyPlot exception: ..." in place of the chart - no build error, a blank
    /// chart at runtime. RectangleBarSeries draws vertical columns against a bottom
    /// CategoryAxis and its items carry a per-item Color, which the threshold
    /// palette needs.
    /// </summary>
    private static RectangleBarSeries BuildBarSeries(QuotaChartModel model)
    {
        var series = new RectangleBarSeries
        {
            FillColor = NormalBarColor,
            StrokeThickness = 0,
            TrackerFormatString = "{Title}: {Y1:#,0} characters",
        };

        for (var index = 0; index < model.Bars.Count; index++)
        {
            var bar = model.Bars[index];
            series.Items.Add(new RectangleBarItem(
                index - ColumnHalfWidth,
                0,
                index + ColumnHalfWidth,
                bar.Characters)
            {
                Color = ColorFor(bar.Level),
                Title = bar.Date.ToString("d MMM", CultureInfo.CurrentCulture),
            });
        }

        return series;
    }

    private static void AddBudgetLine(PlotModel plot, double? budgetLine)
    {
        // No limit means no denominator, so there is no budget to draw. The bars
        // are still true, and are left alone.
        if (budgetLine is not double budget)
        {
            return;
        }

        plot.Annotations.Add(new LineAnnotation
        {
            Type = LineAnnotationType.Horizontal,
            Y = budget,
            Color = OxyColors.SteelBlue,
            LineStyle = LineStyle.Dash,
            Text = $"Budget {budget:#,0}/day",
            TextColor = OxyColors.SteelBlue,
        });
    }

    private static OxyColor ColorFor(QuotaChartBarLevel level)
    {
        return level switch
        {
            QuotaChartBarLevel.Critical => CriticalBarColor,
            QuotaChartBarLevel.Warning => WarningBarColor,
            _ => NormalBarColor,
        };
    }
}
```

- [ ] **Step 5: Build and confirm the suite is still green**

Close the desktop app first — a running `CodeCompanionDesktop.exe` locks the
output files.

```powershell
dotnet build CodeCompanionDesktop.sln
```

Then:

```powershell
dotnet test CodeCompanionDesktop.sln --no-build
```

Expected: 0 errors, 0 warnings, all tests green.

- [ ] **Step 6: Commit**

```bash
git add src/CodeCompanionDesktop/CodeCompanionDesktop.csproj src/CodeCompanionDesktop/MainWindow.xaml src/CodeCompanionDesktop/MainWindow.QuotaGraph.cs
git commit -m "feat(quota): draw the daily usage chart with OxyPlot"
```

---

### Task 6: Wire the forecast, the chart and the warning

Fetch buckets on refresh, render the chart, and fire the warning.

**Files:**
- Create: `src/CodeCompanionDesktop/MainWindow.QuotaWarning.cs`
- Modify: `src/CodeCompanionDesktop/MainWindow.QuotaMeter.cs` (5 lines only)
- Modify: `src/CodeCompanionDesktop/App.xaml.cs`

**Interfaces:**
- Consumes: everything from Tasks 1-5.
- Produces:
  - `public void App.ShowTrayWarning(string title, string message)`
  - `private Task MainWindow.RefreshDailyUsageAsync(string apiKey)`
  - `private void MainWindow.EvaluateQuotaForecast()`

**The new code goes in a new partial, not in `MainWindow.QuotaMeter.cs`.** That
file is 334 lines against a 400-line ceiling, and these additions are about **137
physical lines** — fields, four methods, two usings and the blank separators —
which would land it near **470**. Moving only the two warning methods, an earlier
draft of this plan's fallback, removes just 60 lines and still leaves it at 409.

So `MainWindow.QuotaMeter.cs` gains **only** the two call lines in
`RefreshQuotaAsync` and the three in `RenderQuotaAccessDenied`, finishing near 339.
Everything else lands in `MainWindow.QuotaWarning.cs`, which ends up near 140.

- [ ] **Step 1: Add the tray warning entry point**

In `src/CodeCompanionDesktop/App.xaml.cs`, add after `ShowMainWindow()`:

```csharp
    /// <summary>
    /// Surfaces a warning balloon from the tray icon. Used by the credit warning,
    /// which must be visible while the window is hidden to tray.
    /// </summary>
    public void ShowTrayWarning(string title, string message)
    {
        trayIcon?.ShowBalloonTip(10000, title, message, Forms.ToolTipIcon.Warning);
    }
```

- [ ] **Step 2: Create the warning partial with its fields**

Create `src/CodeCompanionDesktop/MainWindow.QuotaWarning.cs`:

```csharp
using System;
using System.Threading.Tasks;
using CodeCompanionDesktop.ElevenLabs;

namespace CodeCompanionDesktop;

/// <summary>
/// Fetches the daily usage series, projects when credits run out, and fires the
/// warning. Separate from MainWindow.QuotaMeter.cs to keep both files inside the
/// 400-line ceiling.
/// </summary>
public partial class MainWindow
{
    private static readonly TimeSpan DailyUsageRefreshInterval = TimeSpan.FromHours(1);

    // One MaxSpeechTextLength of headroom beyond the warning itself, so the
    // warning is not the utterance that exhausts the quota.
    private const int SpokenWarningHeadroomCharacters = 250;

    private IReadOnlyList<UsageDay> quotaDailyUsage = Array.Empty<UsageDay>();
    private DateTimeOffset? quotaDailyUsageFetchedAt;
    private bool isSpeakingQuotaWarning;
}
```

`System.Collections.Generic` and `System.Linq` are already global usings via
`ImplicitUsings`, so `IReadOnlyList<T>` needs no directive here. The methods in
Step 4 go inside this class body.

- [ ] **Step 3: Fetch the buckets and evaluate after a successful refresh**

In `RefreshQuotaAsync`, replace this existing line in the `try` block:

```csharp
            ApplyQuotaCardVisibility();
            QuotaDetailStatusText.Text = $"Refreshed at {DateTimeOffset.Now:t}.";
```

with:

```csharp
            ApplyQuotaCardVisibility();
            await RefreshDailyUsageAsync(apiKey);
            EvaluateQuotaForecast();
            QuotaDetailStatusText.Text = $"Refreshed at {DateTimeOffset.Now:t}.";
```

- [ ] **Step 4: Implement the fetch, the render and the warning**

Add these methods inside the `MainWindow` class body in the new
`src/CodeCompanionDesktop/MainWindow.QuotaWarning.cs`, after the fields:

```csharp
    /// <summary>
    /// Daily totals do not move fast enough to justify fetching them on every
    /// reconcile, so this throttles to once an hour.
    ///
    /// The window starts at the PREVIOUS reset instant, not midnight on that date.
    /// The API honours the time of day: starting from midnight includes the tail
    /// of the previous billing period and silently inflates every derived figure.
    /// </summary>
    private async Task RefreshDailyUsageAsync(string apiKey)
    {
        var snapshot = quotaTracker.Snapshot;
        if (snapshot is null || snapshot.CharacterLimit <= 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        if (quotaDailyUsageFetchedAt is DateTimeOffset last &&
            now - last < DailyUsageRefreshInterval)
        {
            return;
        }

        try
        {
            var periodStart = snapshot.NextReset.AddMonths(-1);
            quotaDailyUsage = await elevenLabsUsageClient.GetDailyUsageAsync(apiKey, periodStart, now);
            quotaDailyUsageFetchedAt = now;
        }
        catch (Exception)
        {
            // The chart and the projection are both niceties. Losing them must not
            // break the refresh that carries the meter itself.
        }
    }

    private void EvaluateQuotaForecast()
    {
        var snapshot = quotaTracker.Snapshot;
        if (snapshot is null)
        {
            RenderQuotaGraph(QuotaChartModel.Empty);
            return;
        }

        var forecast = QuotaForecast.Create(quotaDailyUsage, snapshot, DateTimeOffset.UtcNow);
        RenderQuotaGraph(QuotaChartModel.Create(quotaDailyUsage, forecast.SurvivalBudgetPerDay));

        var periodResetUnix = snapshot.NextReset.ToUnixTimeSeconds();
        if (!QuotaWarningPolicy.ShouldFire(forecast, periodResetUnix, settings.LastQuotaWarningPeriodResetUnix))
        {
            return;
        }

        settings.LastQuotaWarningPeriodResetUnix = periodResetUnix;
        SaveQuotaToSettings();
        FireQuotaWarning(forecast, snapshot);
    }

    /// <summary>
    /// The balloon goes first: it is free and cannot fail for want of credits.
    /// The spoken warning is best effort on top of it.
    /// </summary>
    private void FireQuotaWarning(QuotaForecast forecast, QuotaSnapshot snapshot)
    {
        var resetLocal = snapshot.NextReset.ToLocalTime();

        (System.Windows.Application.Current as App)?.ShowTrayWarning(
            "Code Companion Desktop",
            QuotaWarningPolicy.BuildBalloonMessage(forecast, resetLocal));

        var spoken = QuotaWarningPolicy.BuildSpokenMessage(forecast, resetLocal);
        if (snapshot.Remaining >= spoken.Length + SpokenWarningHeadroomCharacters)
        {
            _ = SpeakQuotaWarningAsync(spoken);
        }
    }

    /// <summary>
    /// Speaks the warning directly, bypassing SpeechCandidatePipeline: this is not
    /// an assistant message and must not be dropped by text-hash dedupe.
    ///
    /// If playback is already in progress it retries once, then gives up. The
    /// balloon has already landed, so a dropped utterance is not a lost warning.
    /// </summary>
    private async Task SpeakQuotaWarningAsync(string text)
    {
        if (isSpeakingQuotaWarning)
        {
            return;
        }

        isSpeakingQuotaWarning = true;
        try
        {
            await TryPlayQuotaWarningAsync(text);
        }
        finally
        {
            isSpeakingQuotaWarning = false;
        }
    }

    private async Task TryPlayQuotaWarningAsync(string text)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                await PlayElevenLabsSpeechAsync(text, "credit warning");
                return;
            }
            catch (InvalidOperationException)
            {
                // Busy or unavailable. Wait once for the current utterance to
                // finish, then give up rather than queueing behind the session.
                // The delay is guarded on the attempt index: without the guard it
                // runs on both passes, holding isSpeakingQuotaWarning for 40s.
                if (attempt == 1)
                {
                    return;
                }

                await Task.Delay(TimeSpan.FromSeconds(20));
            }
            catch (Exception)
            {
                return;
            }
        }
    }
```

- [ ] **Step 5: Hide the chart in the access-denied state**

In `RenderQuotaAccessDenied()`, add after the existing
`QuotaCompactDetailText.Text = string.Empty;` line:

```csharp
        // Usage needs only the text-to-speech key, so the bars are still true.
        // Only the budget line needs a limit, and QuotaChartModel omits it.
        RenderQuotaGraph(QuotaChartModel.Create(quotaDailyUsage, null));
```

- [ ] **Step 6: Check both files are under the ceiling**

```bash
wc -l src/CodeCompanionDesktop/MainWindow.QuotaMeter.cs src/CodeCompanionDesktop/MainWindow.QuotaWarning.cs src/CodeCompanionDesktop/MainWindow.QuotaGraph.cs
```

Expected: `MainWindow.QuotaMeter.cs` near 339, `MainWindow.QuotaWarning.cs` near
140, `MainWindow.QuotaGraph.cs` near 120 — all under 400.

- [ ] **Step 7: Build and run the full suite**

Close the desktop app first.

```powershell
dotnet build CodeCompanionDesktop.sln
```

Then:

```powershell
dotnet test CodeCompanionDesktop.sln --no-build
```

Expected: 0 errors, 0 warnings, all tests green.

- [ ] **Step 8: Verify on screen**

Start the app, open the Status tab, and confirm:
- the usage chart draws one bar per day of the current billing period
- the dashed budget line sits at the survival budget
- days above the budget are amber, days at double the budget are red

A human looking at the screen has caught defects here that no test could reach.
Do not skip this step.

- [ ] **Step 9: Commit**

```bash
git add src/CodeCompanionDesktop/MainWindow.QuotaMeter.cs src/CodeCompanionDesktop/App.xaml.cs
git commit -m "feat(quota): warn before credits run out and show daily usage"
```

- [ ] **Step 10: Push the branch**

```bash
git push -u origin feat/credit-early-warning
```

---

## Verification

The whole feature is done when, from PowerShell in the Windows checkout with the
app closed:

- `dotnet build CodeCompanionDesktop.sln` reports 0 errors and 0 warnings
- `dotnet test CodeCompanionDesktop.sln --no-build` is green, with at least 40
  new test cases on top of the 125 at the branch point
- `git diff --check` is clean
- the chart renders on the Status tab, with the budget line and threshold colours

## Deliberately not in this plan

- **The `character-cost` defect.** The meter's local prediction uses
  `text.Length`, but the TTS response header `character-cost` is what ElevenLabs
  bills. The forecast reads server buckets, so this cannot affect the warning.
  Separate pass.
- **Any change to what gets spoken.** `SpeechCandidatePipeline` has no volume
  gate. Adding one was considered on 2026-08-06 and rejected in favour of buying
  credits.
