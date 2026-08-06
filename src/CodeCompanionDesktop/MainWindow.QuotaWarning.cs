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
}
