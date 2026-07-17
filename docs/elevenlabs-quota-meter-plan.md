# ElevenLabs Quota Meter Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Finish the parked quota meter so it reports real ElevenLabs character usage with the API key the user already has, and shows a full percentage meter when the key can also read the plan limit.

**Architecture:** Two data sources behind one UI. `ElevenLabsAccountClient` (existing) reads `/v1/user/subscription` for used/limit/tier/reset, but needs the `user_read` scope. When that returns 401/403 it now throws a typed `ElevenLabsAccountAccessDeniedException` carrying ElevenLabs' own message, and the UI falls back to a new `ElevenLabsUsageClient` reading `/v1/usage/character-stats`, which needs no extra scope but supplies usage only. An explicit UI state field decides which of three states renders, so no async ordering race can overwrite the fallback.

**Tech Stack:** C# / .NET 8 (`net8.0-windows`), WPF, xUnit, `System.Text.Json`, `HttpClient`.

## Global Constraints

- Spec: `docs/elevenlabs-quota-meter-spec.md`. Read it before starting.
- Branch: `feature/elevenlabs-quota-meter`. Already merged with `main`; baseline is **87/87 tests passing**.
- **Close Code Companion Desktop before any build or test.** A running app holds a file lock on the output assembly and the build fails.
- **Build and test on Windows PowerShell only.** WSL `dotnet` cannot build `net8.0-windows` / WinExe.
- The usage window is **unix milliseconds**. Seconds return HTTP 200 with an empty `usage` object — a silent zero, not an error. Never pass seconds.
- `xi-api-key` is the correct auth header. `Authorization: Bearer` is rejected. Do not "fix" this.
- Never log, print, or persist the API key.
- Existing style: file-scoped namespaces, `ArgumentNullException.ThrowIfNull`, four-space indent, `sealed` classes.
- `QuotaTracker` and its 9 tests are correct and must not change.

---

### Task 1: Typed access-denied failure carrying the provider's message

**Files:**
- Create: `src/CodeCompanionDesktop/ElevenLabs/ElevenLabsAccountAccessDeniedException.cs`
- Modify: `src/CodeCompanionDesktop/ElevenLabs/ElevenLabsAccountClient.cs`
- Test: `tests/CodeCompanionDesktop.Tests/ElevenLabs/ElevenLabsAccountClientTests.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `ElevenLabsAccountAccessDeniedException : Exception` with a normal `Message`. `ElevenLabsAccountClient.GetSubscriptionAsync(string apiKey, CancellationToken = default)` throws it on 401/403 and keeps throwing `InvalidOperationException` for other failures. Task 3 catches this type.

- [ ] **Step 1: Write the failing tests**

Replace the existing `GetSubscriptionAsyncThrowsOnUnauthorized` test in `tests/CodeCompanionDesktop.Tests/ElevenLabs/ElevenLabsAccountClientTests.cs` with the tests below. It asserts `InvalidOperationException`, and xUnit's `Assert.ThrowsAsync<T>` matches the **exact** type, so it must go or it will fail.

```csharp
    [Fact]
    public async Task GetSubscriptionAsyncThrowsAccessDeniedOnUnauthorized()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(
                    """{"detail":{"type":"authentication_error","code":"unauthorized","message":"The API key you used is missing the permission user_read to execute this operation."}}"""),
            }));

        var account = new ElevenLabsAccountClient(new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<ElevenLabsAccountAccessDeniedException>(() =>
            account.GetSubscriptionAsync("tts-only-key"));

        Assert.Contains("user_read", ex.Message);
    }

    [Fact]
    public async Task GetSubscriptionAsyncThrowsAccessDeniedOnForbidden()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                Content = new StringContent("""{"detail":{"message":"forbidden here"}}"""),
            }));

        var account = new ElevenLabsAccountClient(new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<ElevenLabsAccountAccessDeniedException>(() =>
            account.GetSubscriptionAsync("key"));

        Assert.Equal("forbidden here", ex.Message);
    }

    [Fact]
    public async Task GetSubscriptionAsyncAccessDeniedToleratesUnparseableBody()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("<html>gateway blew up</html>"),
            }));

        var account = new ElevenLabsAccountClient(new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<ElevenLabsAccountAccessDeniedException>(() =>
            account.GetSubscriptionAsync("key"));

        Assert.Contains("gateway blew up", ex.Message);
    }

    [Fact]
    public async Task GetSubscriptionAsyncAccessDeniedToleratesEmptyBody()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(string.Empty),
            }));

        var account = new ElevenLabsAccountClient(new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<ElevenLabsAccountAccessDeniedException>(() =>
            account.GetSubscriptionAsync("key"));

        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    [Fact]
    public async Task GetSubscriptionAsyncStillThrowsInvalidOperationOnServerError()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""{"detail":{"message":"boom"}}"""),
            }));

        var account = new ElevenLabsAccountClient(new HttpClient(handler));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            account.GetSubscriptionAsync("key"));
    }
```

- [ ] **Step 2: Run tests to verify they fail**

Close the desktop app first, then from Windows PowerShell in `D:\Development\CodeCompanionDesktop`:

```powershell
dotnet test CodeCompanionDesktop.sln --filter "FullyQualifiedName~ElevenLabsAccountClientTests"
```

Expected: FAIL — compile error, `ElevenLabsAccountAccessDeniedException` does not exist.

- [ ] **Step 3: Create the exception**

Create `src/CodeCompanionDesktop/ElevenLabs/ElevenLabsAccountAccessDeniedException.cs`:

```csharp
using System;

namespace CodeCompanionDesktop.ElevenLabs;

/// <summary>
/// The API key authenticated but is not permitted to read account information.
/// Carries the provider's own explanation, which names the missing scope.
/// </summary>
public sealed class ElevenLabsAccountAccessDeniedException : Exception
{
    public ElevenLabsAccountAccessDeniedException(string message)
        : base(message)
    {
    }
}
```

- [ ] **Step 4: Throw it from the client**

In `src/CodeCompanionDesktop/ElevenLabs/ElevenLabsAccountClient.cs`, add `using System.Net;` to the existing usings, then replace this block:

```csharp
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"ElevenLabs subscription request failed ({(int)response.StatusCode} {response.ReasonPhrase}): {TrimError(body)}");
        }
```

with:

```csharp
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new ElevenLabsAccountAccessDeniedException(ExtractProviderMessage(body));
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"ElevenLabs subscription request failed ({(int)response.StatusCode} {response.ReasonPhrase}): {TrimError(body)}");
        }
```

Then add this method to the same class, next to `TrimError`:

```csharp
    /// <summary>
    /// Pulls ElevenLabs' own error text out of a `detail.message` body. Their
    /// message names the exact missing scope, so it beats anything we invent and
    /// does not rot if they rename a scope. Must never throw.
    /// </summary>
    internal static string ExtractProviderMessage(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return "ElevenLabs denied access to account information for this API key.";
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            // TryGetProperty throws InvalidOperationException (not JsonException)
            // when the root is valid JSON but not an object — a bare null, number,
            // bool, array or string, which a gateway can return on a 401. Guard the
            // kind rather than catching InvalidOperationException, which would mask
            // unrelated real bugs.
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("detail", out var detail) &&
                detail.ValueKind == JsonValueKind.Object &&
                detail.TryGetProperty("message", out var message) &&
                message.ValueKind == JsonValueKind.String)
            {
                var text = message.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text!;
                }
            }
        }
        catch (JsonException)
        {
            // Not JSON. Fall through and show the raw body.
        }

        return TrimError(body);
    }
```

Also add this test, which is what proves the guard above is needed:

```csharp
    [Theory]
    [InlineData("null")]
    [InlineData("5")]
    [InlineData("true")]
    [InlineData("[1,2,3]")]
    [InlineData("\"just a string\"")]
    public async Task GetSubscriptionAsyncAccessDeniedToleratesNonObjectJsonRoot(string body)
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent(body),
            }));

        var account = new ElevenLabsAccountClient(new HttpClient(handler));

        var ex = await Assert.ThrowsAsync<ElevenLabsAccountAccessDeniedException>(() =>
            account.GetSubscriptionAsync("key"));

        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }
```

- [ ] **Step 5: Run tests to verify they pass**

```powershell
dotnet test CodeCompanionDesktop.sln --filter "FullyQualifiedName~ElevenLabsAccountClientTests"
```

Expected: PASS — 15 tests, 0 failed. The file had 6; one is replaced by the 5 `[Fact]` tests above, giving 10, and the `[Theory]` contributes 5 more cases.

- [ ] **Step 6: Commit**

```bash
git add src/CodeCompanionDesktop/ElevenLabs/ElevenLabsAccountAccessDeniedException.cs src/CodeCompanionDesktop/ElevenLabs/ElevenLabsAccountClient.cs tests/CodeCompanionDesktop.Tests/ElevenLabs/ElevenLabsAccountClientTests.cs
git commit -m "feat(quota): type the access-denied failure and keep the provider's message

A 401 from /v1/user/subscription means the key lacks user_read, not that the
request is broken. Callers could not tell that from a generic
InvalidOperationException, so the meter reported it as 'Refresh failed: ...401'.
Carry ElevenLabs' own message, which names the missing scope exactly."
```

---

### Task 2: Usage client for `/v1/usage/character-stats`

**Files:**
- Create: `src/CodeCompanionDesktop/ElevenLabs/ElevenLabsUsageClient.cs`
- Test: `tests/CodeCompanionDesktop.Tests/ElevenLabs/ElevenLabsUsageClientTests.cs`

**Interfaces:**
- Consumes: nothing from Task 1.
- Produces: `ElevenLabsUsageClient` with `ElevenLabsUsageClient()`, `ElevenLabsUsageClient(HttpClient)`, `Task<long> GetCharactersUsedAsync(string apiKey, DateTimeOffset startUtc, DateTimeOffset endUtc, CancellationToken = default)` and `static long SumUsage(string json)`. Task 3 constructs it parameterless and calls `GetCharactersUsedAsync`.

Verified response shape (real call, 2026-07-17):

```json
{"time":[1781654400000,1781740800000],"usage":{"All":[1596.0,14679.0]}}
```

- [ ] **Step 1: Write the failing tests**

Create `tests/CodeCompanionDesktop.Tests/ElevenLabs/ElevenLabsUsageClientTests.cs`:

```csharp
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CodeCompanionDesktop.ElevenLabs;

namespace CodeCompanionDesktop.Tests.ElevenLabs;

public sealed class ElevenLabsUsageClientTests
{
    [Fact]
    public void SumUsageAddsAllSeriesBuckets()
    {
        const string json = """{"time":[1,2,3],"usage":{"All":[1596.0,14679.0,11306.0]}}""";

        Assert.Equal(27581L, ElevenLabsUsageClient.SumUsage(json));
    }

    [Fact]
    public void SumUsageReturnsZeroForEmptyUsageObject()
    {
        // This is the shape returned when the window is sent in seconds:
        // HTTP 200 with no usage. It must read as zero, not throw.
        Assert.Equal(0L, ElevenLabsUsageClient.SumUsage("""{"time":[1728000000],"usage":{}}"""));
    }

    [Fact]
    public void SumUsageToleratesMissingUsageProperty()
    {
        Assert.Equal(0L, ElevenLabsUsageClient.SumUsage("""{"time":[1,2]}"""));
    }

    [Fact]
    public void SumUsageToleratesNonNumericAndNegativeEntries()
    {
        const string json = """{"usage":{"All":[100.0,"nope",null,-5.0,25.0]}}""";

        Assert.Equal(125L, ElevenLabsUsageClient.SumUsage(json));
    }

    [Fact]
    public void SumUsageToleratesUnparseableJson()
    {
        Assert.Equal(0L, ElevenLabsUsageClient.SumUsage("<html>nope</html>"));
    }

    [Fact]
    public void SumUsagePrefersAllSeriesOverPerBreakdownSeries()
    {
        // "All" is the aggregate. Summing every series would double count.
        const string json = """{"usage":{"All":[100.0],"voice-a":[60.0],"voice-b":[40.0]}}""";

        Assert.Equal(100L, ElevenLabsUsageClient.SumUsage(json));
    }

    [Fact]
    public async Task GetCharactersUsedAsyncSendsMillisecondWindowAndApiKey()
    {
        // The window MUST be milliseconds. Seconds return 200 with empty usage,
        // so this assertion is what stops a silent zero-usage bug.
        Uri? capturedUri = null;
        string? capturedHeader = null;

        var handler = new StubHandler((request, _) =>
        {
            capturedUri = request.RequestUri;
            capturedHeader = request.Headers.GetValues("xi-api-key").Single();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"time":[1],"usage":{"All":[7.0]}}"""),
            });
        });

        var client = new ElevenLabsUsageClient(new HttpClient(handler));

        var start = DateTimeOffset.FromUnixTimeSeconds(1_700_000_000);
        var end = DateTimeOffset.FromUnixTimeSeconds(1_700_086_400);

        var used = await client.GetCharactersUsedAsync("test-key", start, end);

        Assert.Equal(7L, used);
        Assert.NotNull(capturedUri);
        Assert.Equal("/v1/usage/character-stats", capturedUri!.AbsolutePath);
        Assert.Equal("test-key", capturedHeader);

        // Milliseconds, not seconds. 1700000000 seconds -> 1700000000000 ms.
        Assert.Contains("start_unix=1700000000000", capturedUri.Query);
        Assert.Contains("end_unix=1700086400000", capturedUri.Query);
    }

    [Fact]
    public async Task GetCharactersUsedAsyncThrowsOnFailureStatus()
    {
        var handler = new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("boom"),
            }));

        var client = new ElevenLabsUsageClient(new HttpClient(handler));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            client.GetCharactersUsedAsync("key", DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task GetCharactersUsedAsyncRejectsBlankApiKey()
    {
        var client = new ElevenLabsUsageClient(new HttpClient(new StubHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)))));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            client.GetCharactersUsedAsync("   ", DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow));
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler;

        public StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            this.handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return handler(request, cancellationToken);
        }
    }
}
```

No new project references are needed: the query is asserted as a plain string, so
`System.Web` is not used.

- [ ] **Step 2: Run tests to verify they fail**

```powershell
dotnet test CodeCompanionDesktop.sln --filter "FullyQualifiedName~ElevenLabsUsageClientTests"
```

Expected: FAIL — compile error, `ElevenLabsUsageClient` does not exist.

- [ ] **Step 3: Write the client**

Create `src/CodeCompanionDesktop/ElevenLabs/ElevenLabsUsageClient.cs`:

```csharp
using System;
using System.Globalization;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CodeCompanionDesktop.ElevenLabs;

/// <summary>
/// Reads character usage from /v1/usage/character-stats.
///
/// This endpoint needs no scope beyond the text-to-speech key, so it works when
/// /v1/user/subscription is refused for lacking user_read. It reports usage only:
/// there is no limit, tier, or reset date here, and no other endpoint supplies
/// them (every workspace-level quota path returns 404).
/// </summary>
public sealed class ElevenLabsUsageClient
{
    private static readonly Uri DefaultBaseAddress = new("https://api.elevenlabs.io");

    private readonly HttpClient httpClient;

    public ElevenLabsUsageClient()
        : this(CreateDefaultHttpClient())
    {
    }

    public ElevenLabsUsageClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);

        if (httpClient.BaseAddress is null)
        {
            httpClient.BaseAddress = DefaultBaseAddress;
        }

        this.httpClient = httpClient;
    }

    public async Task<long> GetCharactersUsedAsync(
        string apiKey,
        DateTimeOffset startUtc,
        DateTimeOffset endUtc,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(apiKey);

        // Milliseconds, not seconds. Seconds return HTTP 200 with an empty usage
        // object, which would silently report zero characters used.
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

        return SumUsage(body);
    }

    /// <summary>
    /// Sums the usage series. Shape: {"time":[ms,...],"usage":{"All":[chars,...]}}.
    /// Must never throw: a usage figure is a nicety, and losing it must not break
    /// the surrounding refresh.
    /// </summary>
    public static long SumUsage(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return 0;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            // TryGetProperty throws InvalidOperationException (not JsonException)
            // when the element is not an object, so guard the kind first.
            if (root.ValueKind != JsonValueKind.Object ||
                !root.TryGetProperty("usage", out var usage) ||
                usage.ValueKind != JsonValueKind.Object)
            {
                return 0;
            }

            // "All" is the aggregate series. Summing every series double counts.
            if (usage.TryGetProperty("All", out var all) && all.ValueKind == JsonValueKind.Array)
            {
                return SumArray(all);
            }

            foreach (var series in usage.EnumerateObject())
            {
                if (series.Value.ValueKind == JsonValueKind.Array)
                {
                    return SumArray(series.Value);
                }
            }

            return 0;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or ArgumentException)
        {
            // Best-effort by contract: this must never throw, whatever the body
            // contains. System.Text.Json signals failure with three different types
            // here — JsonException (not JSON), InvalidOperationException (wrong
            // element kind), and ArgumentException (Parse transcoding an already
            // ill-formed UTF-16 string). Catching the category rather than each site
            // stops this becoming whack-a-mole. It stays a filter rather than a bare
            // catch so genuine faults like OutOfMemoryException still propagate.
            return 0;
        }
    }

    private static long SumArray(JsonElement array)
    {
        long total = 0;
        foreach (var entry in array.EnumerateArray())
        {
            // IsFinite matters: 1e400 reads as +Infinity and (long)Math.Round of
            // that is long.MinValue, which would turn the sum sharply negative.
            if (entry.ValueKind == JsonValueKind.Number &&
                entry.TryGetDouble(out var value) &&
                double.IsFinite(value) &&
                value > 0)
            {
                total += (long)Math.Round(value);
            }
        }

        return total;
    }

    private static HttpClient CreateDefaultHttpClient()
    {
        return new HttpClient { BaseAddress = DefaultBaseAddress };
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```powershell
dotnet test CodeCompanionDesktop.sln --filter "FullyQualifiedName~ElevenLabsUsageClientTests"
```

Expected: PASS — 9 tests, 0 failed.

- [ ] **Step 5: Commit**

```bash
git add src/CodeCompanionDesktop/ElevenLabs/ElevenLabsUsageClient.cs tests/CodeCompanionDesktop.Tests/ElevenLabs/ElevenLabsUsageClientTests.cs
git commit -m "feat(quota): read character usage without the user_read scope

/v1/usage/character-stats answers a text-to-speech key, so usage is readable
even when /v1/user/subscription is refused. It carries no limit, tier or reset
date, and nothing else does either — every workspace-level quota path 404s.

The window is unix milliseconds. Seconds return 200 with an empty usage object,
so a seconds bug would silently report zero; the query assertion pins that."
```

---

### Task 3: Three UI states, usage fallback, and the quiet-flag fix

**Files:**
- Modify: `src/CodeCompanionDesktop/MainWindow.QuotaMeter.cs`

**Interfaces:**
- Consumes: `ElevenLabsAccountAccessDeniedException` (Task 1), `ElevenLabsUsageClient.GetCharactersUsedAsync` (Task 2).
- Produces: nothing consumed by later tasks.

No unit tests: this is WPF view code and the project has no UI test harness. It is verified by build plus the live checks in Step 6. That is a deliberate, stated gap, not an oversight.

The three states, per the spec: **Full** (subscription readable), **Usage-only** (access denied, usage readable), **Unavailable** (both failed).

- [ ] **Step 1: Add state fields and the usage client**

In `src/CodeCompanionDesktop/MainWindow.QuotaMeter.cs`, replace this block:

```csharp
    private const int SpeechesBetweenServerReconcile = 5;

    private readonly QuotaTracker quotaTracker = new();
    private readonly ElevenLabsAccountClient elevenLabsAccountClient = new();
    private int speechesUntilReconcile = SpeechesBetweenServerReconcile;
    private bool isRefreshingQuota;
    private bool quotaWired;
```

with:

```csharp
    private const int SpeechesBetweenServerReconcile = 5;
    private const int UsageFallbackWindowDays = 30;

    private readonly QuotaTracker quotaTracker = new();
    private readonly ElevenLabsAccountClient elevenLabsAccountClient = new();
    private readonly ElevenLabsUsageClient elevenLabsUsageClient = new();
    private int speechesUntilReconcile = SpeechesBetweenServerReconcile;
    private bool isRefreshingQuota;
    private bool quotaWired;

    // Non-null means the key cannot read /v1/user/subscription. Held as explicit
    // state rather than decided inside the catch, because QuotaTracker.StateChanged
    // repaints via Dispatcher.InvokeAsync and would otherwise race the fallback
    // and overwrite it.
    private string? quotaAccessDeniedMessage;
    private long? quotaUsageOnlyCharacters;
```

- [ ] **Step 2: Route card visibility through one helper**

Still in `MainWindow.QuotaMeter.cs`, replace this block in `WireQuotaMeter`:

```csharp
        ShowQuotaMeterCheckBox.IsChecked = settings.ShowElevenLabsQuotaMeter;
        QuotaMeterCompactCard.Visibility = settings.ShowElevenLabsQuotaMeter
            ? Visibility.Visible
            : Visibility.Collapsed;
```

with:

```csharp
        ShowQuotaMeterCheckBox.IsChecked = settings.ShowElevenLabsQuotaMeter;
        ApplyQuotaCardVisibility();
```

and replace this block in `ShowQuotaMeterCheckBox_Changed`:

```csharp
        settings.ShowElevenLabsQuotaMeter = ShowQuotaMeterCheckBox.IsChecked == true;
        QuotaMeterCompactCard.Visibility = settings.ShowElevenLabsQuotaMeter
            ? Visibility.Visible
            : Visibility.Collapsed;
```

with:

```csharp
        settings.ShowElevenLabsQuotaMeter = ShowQuotaMeterCheckBox.IsChecked == true;
        ApplyQuotaCardVisibility();
```

Then add this method to the class:

```csharp
    /// <summary>
    /// The compact card is a percentage bar. Without a limit there is no
    /// denominator to draw, so it stays hidden while access is denied however the
    /// user has set the toggle. The toggle setting itself is never overwritten.
    /// </summary>
    private void ApplyQuotaCardVisibility()
    {
        var canShow = settings.ShowElevenLabsQuotaMeter && quotaAccessDeniedMessage is null;
        QuotaMeterCompactCard.Visibility = canShow ? Visibility.Visible : Visibility.Collapsed;
    }
```

- [ ] **Step 3: Render the usage-only state**

Still in `MainWindow.QuotaMeter.cs`, replace the opening of `UpdateQuotaUiFromTracker`:

```csharp
    private void UpdateQuotaUiFromTracker()
    {
        var snapshot = quotaTracker.Snapshot;

        if (snapshot is null || snapshot.CharacterLimit <= 0)
```

with:

```csharp
    private void UpdateQuotaUiFromTracker()
    {
        if (quotaAccessDeniedMessage is not null)
        {
            RenderQuotaAccessDenied();
            return;
        }

        var snapshot = quotaTracker.Snapshot;

        if (snapshot is null || snapshot.CharacterLimit <= 0)
```

Then add this method to the class:

```csharp
    private void RenderQuotaAccessDenied()
    {
        ApplyQuotaCardVisibility();
        QuotaCompactProgressBar.Visibility = Visibility.Collapsed;
        QuotaCompactDetailText.Text = string.Empty;

        QuotaDetailTierText.Text = "Tier: unknown";
        QuotaDetailRemainingText.Text = "Remaining: unknown";
        QuotaDetailResetText.Text = "Resets: unknown";

        if (quotaUsageOnlyCharacters is long used)
        {
            var summary = $"{used:N0} characters used (last {UsageFallbackWindowDays} days)";
            QuotaCompactSummaryText.Text = summary;
            QuotaDetailCharactersText.Text = $"Used: {summary}";
            QuotaDetailAsOfText.Text = $"As of {DateTimeOffset.Now:d MMM yyyy h:mm tt}";
            QuotaDetailStatusText.Text =
                $"{quotaAccessDeniedMessage} Add the user_read permission to your ElevenLabs API key to show your limit and percentage.";
        }
        else
        {
            QuotaCompactSummaryText.Text = "Quota unavailable.";
            QuotaDetailCharactersText.Text = "Used: -";
            QuotaDetailAsOfText.Text = "No data yet.";
            QuotaDetailStatusText.Text = quotaAccessDeniedMessage ?? string.Empty;
        }
    }
```

- [ ] **Step 4: Add the fallback and fix the quiet flag**

Still in `MainWindow.QuotaMeter.cs`, replace this block in `RefreshQuotaAsync`:

```csharp
            var subscription = await elevenLabsAccountClient.GetSubscriptionAsync(apiKey);
            quotaTracker.UpdateFromSubscription(subscription, DateTimeOffset.UtcNow);
            SaveQuotaToSettings();
            speechesUntilReconcile = SpeechesBetweenServerReconcile;

            QuotaDetailStatusText.Text = $"Refreshed at {DateTimeOffset.Now:t}.";
        }
        catch (Exception ex)
        {
            QuotaDetailStatusText.Text = $"Refresh failed: {ex.Message}";
        }
```

with:

```csharp
            var subscription = await elevenLabsAccountClient.GetSubscriptionAsync(apiKey);

            quotaAccessDeniedMessage = null;
            quotaUsageOnlyCharacters = null;
            quotaTracker.UpdateFromSubscription(subscription, DateTimeOffset.UtcNow);
            SaveQuotaToSettings();
            speechesUntilReconcile = SpeechesBetweenServerReconcile;

            ApplyQuotaCardVisibility();
            QuotaDetailStatusText.Text = $"Refreshed at {DateTimeOffset.Now:t}.";
        }
        catch (ElevenLabsAccountAccessDeniedException ex)
        {
            // The key speaks but cannot read the account. Recorded whether or not
            // this refresh was quiet, because the meter must hide itself either way.
            quotaAccessDeniedMessage = ex.Message;
            quotaUsageOnlyCharacters = await TryGetUsageOnlyCharactersAsync(apiKey);
            RenderQuotaAccessDenied();
        }
        catch (Exception ex)
        {
            // A background refresh must not splash errors into the UI.
            if (!quiet)
            {
                QuotaDetailStatusText.Text = $"Refresh failed: {ex.Message}";
            }
        }
```

Then add this method to the class:

```csharp
    private async Task<long?> TryGetUsageOnlyCharactersAsync(string apiKey)
    {
        try
        {
            var end = DateTimeOffset.UtcNow;
            var start = end.AddDays(-UsageFallbackWindowDays);
            return await elevenLabsUsageClient.GetCharactersUsedAsync(apiKey, start, end);
        }
        catch (Exception)
        {
            // Usage is a fallback for a fallback. Losing it leaves the
            // unavailable state, which is still honest.
            return null;
        }
    }
```

- [ ] **Step 5: Build and run the full suite**

Close the desktop app, then:

```powershell
dotnet build CodeCompanionDesktop.sln
```

Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

```powershell
dotnet test CodeCompanionDesktop.sln --no-build
```

Expected: PASS, 0 failed. Baseline was 87; Task 1 takes the account-client file 6 → 16 (+10) and Task 2 adds 16, so expect **113** tests.

- [ ] **Step 6: Verify live against the real app**

Launch the app:

```powershell
Start-Process -FilePath "D:\Development\CodeCompanionDesktop\src\CodeCompanionDesktop\bin\Debug\net8.0-windows\CodeCompanionDesktop.exe"
```

With the current text-to-speech-only key, confirm all of:

- The Status tab shows **no** quota bar and **no** `Refresh failed: ...401...` text on startup.
- Speech Provider → Quota Details shows `Used: <n> characters used (last 30 days)` with a non-zero `<n>`. A zero here means the window went out as seconds — check Task 2.
- The status line shows ElevenLabs' own message naming `user_read`, followed by the sentence about adding the permission.
- Clicking **Refresh Quota** repeats that without an unhandled exception.
- Ticking **Show quota meter** does not reveal an empty bar.

Then confirm the unavailable state, which is the spec's no-network case. Disable
networking (turn off Wi-Fi, or pull the Ethernet cable) and click **Refresh
Quota**:

- The app does not crash and does not hang.
- The status line reports a failure once, from the manual click.
- Restarting the app with networking still off shows no error splash on startup —
  that is the quiet path.

Re-enable networking afterwards.

- [ ] **Step 7: Commit**

```bash
git add src/CodeCompanionDesktop/MainWindow.QuotaMeter.cs
git commit -m "feat(quota): degrade to usage-only instead of failing

A key without user_read can still read /v1/usage/character-stats, so show real
characters used for the last 30 days rather than an error. No bar is drawn: with
no limit there is no denominator, and a bar without one is a lie.

Access-denied is held as explicit state rather than handled inside the catch,
because QuotaTracker.StateChanged repaints via Dispatcher.InvokeAsync and would
race the fallback and overwrite it.

Also stop background refreshes splashing errors: quiet guarded the earlier
messages but not the final catch, so startup wrote 'Refresh failed' into the UI."
```

---

### Task 4: Merge to main

**Files:**
- Modify: none (branch operation)

**Interfaces:**
- Consumes: Tasks 1-3 complete and committed.
- Produces: nothing.

- [ ] **Step 1: Confirm the tree is clean and green**

```powershell
git status --short
dotnet test CodeCompanionDesktop.sln
```

Expected: no output from `git status --short`; tests PASS with 0 failed.

- [ ] **Step 2: Merge and push**

```bash
git checkout main
git merge --no-ff feature/elevenlabs-quota-meter -m "Merge feature/elevenlabs-quota-meter: complete the quota meter"
git push origin main
```

- [ ] **Step 3: Relaunch the app from main**

```powershell
Start-Process -FilePath "D:\Development\CodeCompanionDesktop\src\CodeCompanionDesktop\bin\Debug\net8.0-windows\CodeCompanionDesktop.exe"
```

---

## Notes for the implementer

- If `dotnet build` fails with a file lock, the desktop app is still running. Close it.
- If the meter shows `0` characters used, the window was sent in seconds. The API returns HTTP 200 with `{"usage":{}}` for a seconds window — it does not error.
- If `/v1/user/subscription` starts returning 200 (the user granted `user_read` or saved a new key), the full meter returns automatically with no settings change. That is `quotaAccessDeniedMessage = null` on the success path.
- Do not add retry, caching, or a usage chart. The endpoint returns per-day buckets and the temptation to graph them is real; the feature is a meter.
