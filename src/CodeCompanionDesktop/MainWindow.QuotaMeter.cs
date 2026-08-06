using System;
using System.Threading.Tasks;
using System.Windows;
using CodeCompanionDesktop.Credentials;
using CodeCompanionDesktop.ElevenLabs;
using CodeCompanionDesktop.Settings;
using Brushes = System.Windows.Media.Brushes;

namespace CodeCompanionDesktop;

public partial class MainWindow
{
    private const int SpeechesBetweenServerReconcile = 5;
    private const int UsageFallbackWindowDays = 30;
    private const string QuotaSubtitleBillingPeriod =
        "Live ElevenLabs character usage for the current billing period.";

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

    // null until the first refresh has looked. Distinguishes "no key saved" from
    // "key saved but we have no data", which must not read the same on screen.
    private bool? quotaApiKeyPresent;

    private void WireQuotaMeter()
    {
        if (!quotaWired)
        {
            quotaTracker.StateChanged += (_, _) =>
                Dispatcher.InvokeAsync(UpdateQuotaUiFromTracker);
            quotaWired = true;
        }

        ShowQuotaMeterCheckBox.IsChecked = settings.ShowElevenLabsQuotaMeter;
        ApplyQuotaCardVisibility();

        RestoreQuotaSnapshotFromSettings();
        UpdateQuotaUiFromTracker();

        _ = RefreshQuotaAsync(quiet: true);
    }

    private void RestoreQuotaSnapshotFromSettings()
    {
        var saved = settings.LastKnownElevenLabsQuota;
        if (saved is null)
        {
            return;
        }

        var snapshot = new QuotaSnapshot(
            saved.CharacterCount,
            saved.CharacterLimit,
            DateTimeOffset.FromUnixTimeSeconds(saved.NextResetUnix),
            saved.Tier ?? string.Empty,
            DateTimeOffset.FromUnixTimeSeconds(saved.AsOfUnix),
            QuotaSnapshotSource.Server);

        quotaTracker.RestoreFromPersisted(snapshot);
    }

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

    private void OnSpeechProduced(int characters)
    {
        if (characters <= 0)
        {
            return;
        }

        quotaTracker.RecordSpentCharacters(characters, DateTimeOffset.UtcNow);
        SaveQuotaToSettings();

        speechesUntilReconcile--;
        if (speechesUntilReconcile <= 0)
        {
            speechesUntilReconcile = SpeechesBetweenServerReconcile;
            _ = RefreshQuotaAsync(quiet: true);
        }
    }

    private async Task RefreshQuotaAsync(bool quiet)
    {
        if (isRefreshingQuota)
        {
            return;
        }

        string? apiKey;
        try
        {
            apiKey = credentialStore
                .ReadSecret(WindowsCredentialStore.ElevenLabsApiKeyTarget)
                ?.Trim();
        }
        catch (Exception ex)
        {
            if (!quiet)
            {
                QuotaDetailStatusText.Text = $"Unable to read API key: {ex.Message}";
            }
            return;
        }

        quotaApiKeyPresent = !string.IsNullOrWhiteSpace(apiKey);

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            if (!quiet)
            {
                QuotaDetailStatusText.Text = "Save an ElevenLabs API key first.";
            }
            return;
        }

        isRefreshingQuota = true;
        try
        {
            if (!quiet)
            {
                QuotaDetailStatusText.Text = "Refreshing quota...";
            }
            RefreshQuotaButton.IsEnabled = false;

            var subscription = await elevenLabsAccountClient.GetSubscriptionAsync(apiKey);

            quotaAccessDeniedMessage = null;
            quotaUsageOnlyCharacters = null;
            quotaTracker.UpdateFromSubscription(subscription, DateTimeOffset.UtcNow);
            SaveQuotaToSettings();
            speechesUntilReconcile = SpeechesBetweenServerReconcile;

            ApplyQuotaCardVisibility();
            await RefreshDailyUsageAsync(apiKey);
            EvaluateQuotaForecast();
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
        finally
        {
            isRefreshingQuota = false;
            RefreshQuotaButton.IsEnabled = true;
        }
    }

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

    private void SaveQuotaToSettings()
    {
        var snapshot = quotaTracker.Snapshot;
        settings.LastKnownElevenLabsQuota = snapshot is null
            ? null
            : new ElevenLabsQuotaSnapshotData
            {
                CharacterCount = snapshot.CharacterCount,
                CharacterLimit = snapshot.CharacterLimit,
                NextResetUnix = snapshot.NextReset.ToUnixTimeSeconds(),
                Tier = snapshot.Tier,
                AsOfUnix = snapshot.AsOf.ToUnixTimeSeconds(),
            };

        try
        {
            settingsStore.Save(settings);
        }
        catch
        {
            // Best effort; persistence retried on the next successful save.
        }
    }

    private void UpdateQuotaUiFromTracker()
    {
        if (quotaAccessDeniedMessage is not null)
        {
            RenderQuotaAccessDenied();
            return;
        }

        QuotaDetailSubtitleText.Text = QuotaSubtitleBillingPeriod;

        var snapshot = quotaTracker.Snapshot;

        if (snapshot is null || snapshot.CharacterLimit <= 0)
        {
            QuotaDetailSubtitleText.Text = "ElevenLabs character usage.";

            QuotaCompactSummaryText.Text = quotaApiKeyPresent == false
                ? "Save an ElevenLabs API key to see remaining characters."
                : "Quota data is not available right now.";
            QuotaCompactProgressBar.Visibility = Visibility.Collapsed;
            QuotaCompactDetailText.Text = string.Empty;

            QuotaDetailTierText.Text = "Tier: unknown";
            QuotaDetailCharactersText.Text = "Used: -";
            QuotaDetailRemainingText.Text = "Remaining: -";
            QuotaDetailResetText.Text = "Resets: -";
            QuotaDetailAsOfText.Text = quotaApiKeyPresent == false
                ? "No data yet."
                : "No quota data — the last refresh did not succeed.";
            return;
        }

        var resetLocal = snapshot.NextReset.ToLocalTime();
        var asOfLocal = snapshot.AsOf.ToLocalTime();
        var stalenessHint = snapshot.Source == QuotaSnapshotSource.LocallyPredicted
            ? " (locally predicted)"
            : string.Empty;

        QuotaCompactSummaryText.Text =
            $"{snapshot.CharacterCount:N0} / {snapshot.CharacterLimit:N0} characters used ({snapshot.PercentUsed}%)";
        QuotaCompactProgressBar.Visibility = Visibility.Visible;
        QuotaCompactProgressBar.Value = snapshot.PercentUsed;
        QuotaCompactProgressBar.Foreground = snapshot.PercentUsed switch
        {
            >= 90 => Brushes.IndianRed,
            >= 70 => Brushes.Goldenrod,
            _ => Brushes.MediumSeaGreen,
        };
        QuotaCompactDetailText.Text =
            $"{snapshot.Remaining:N0} remaining — resets {resetLocal:d MMM yyyy}{stalenessHint}";

        var tierLabel = string.IsNullOrEmpty(snapshot.Tier) ? "unknown" : snapshot.Tier;
        QuotaDetailTierText.Text = $"Tier: {tierLabel}";
        QuotaDetailCharactersText.Text =
            $"Used: {snapshot.CharacterCount:N0} / {snapshot.CharacterLimit:N0} ({snapshot.PercentUsed}%)";
        QuotaDetailRemainingText.Text = $"Remaining: {snapshot.Remaining:N0}";
        QuotaDetailResetText.Text = $"Resets: {resetLocal:d MMM yyyy h:mm tt}";
        QuotaDetailAsOfText.Text = $"As of {asOfLocal:d MMM yyyy h:mm tt}{stalenessHint}";
    }

    private void RenderQuotaAccessDenied()
    {
        ApplyQuotaCardVisibility();
        QuotaCompactProgressBar.Visibility = Visibility.Collapsed;
        QuotaCompactDetailText.Text = string.Empty;

        // Usage needs only the text-to-speech key, so the bars are still true.
        // Only the budget line needs a limit, and QuotaChartModel omits it.
        RenderQuotaGraph(QuotaChartModel.Create(quotaDailyUsage, null));

        QuotaDetailTierText.Text = "Tier: unknown";
        QuotaDetailRemainingText.Text = "Remaining: unknown";
        QuotaDetailResetText.Text = "Resets: unknown";

        if (quotaUsageOnlyCharacters is long used)
        {
            QuotaDetailSubtitleText.Text =
                $"ElevenLabs character usage for the last {UsageFallbackWindowDays} days. Your plan limit is not readable with this API key.";
            var summary = $"{used:N0} characters used (last {UsageFallbackWindowDays} days)";
            QuotaCompactSummaryText.Text = summary;
            QuotaDetailCharactersText.Text = $"Used: {summary}";
            QuotaDetailAsOfText.Text = $"As of {DateTimeOffset.Now:d MMM yyyy h:mm tt}";
            QuotaDetailStatusText.Text =
                $"{quotaAccessDeniedMessage} Add the user_read permission to your ElevenLabs API key to show your limit and percentage.";
        }
        else
        {
            QuotaDetailSubtitleText.Text = "ElevenLabs quota is unavailable with this API key.";
            QuotaCompactSummaryText.Text = "Quota unavailable.";
            QuotaDetailCharactersText.Text = "Used: -";
            QuotaDetailAsOfText.Text = "No data yet.";
            QuotaDetailStatusText.Text = quotaAccessDeniedMessage ?? string.Empty;
        }
    }

    private async void RefreshQuotaButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshQuotaAsync(quiet: false);
    }

    private void ShowQuotaMeterCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (isInitializing)
        {
            return;
        }

        settings.ShowElevenLabsQuotaMeter = ShowQuotaMeterCheckBox.IsChecked == true;
        ApplyQuotaCardVisibility();

        try
        {
            settingsStore.Save(settings);
        }
        catch
        {
            // Best effort; visibility persists on the next successful save.
        }
    }
}
