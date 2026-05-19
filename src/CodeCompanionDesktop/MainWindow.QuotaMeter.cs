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

    private readonly QuotaTracker quotaTracker = new();
    private readonly ElevenLabsAccountClient elevenLabsAccountClient = new();
    private int speechesUntilReconcile = SpeechesBetweenServerReconcile;
    private bool isRefreshingQuota;
    private bool quotaWired;

    private void WireQuotaMeter()
    {
        if (!quotaWired)
        {
            quotaTracker.StateChanged += (_, _) =>
                Dispatcher.InvokeAsync(UpdateQuotaUiFromTracker);
            quotaWired = true;
        }

        ShowQuotaMeterCheckBox.IsChecked = settings.ShowElevenLabsQuotaMeter;
        QuotaMeterCompactCard.Visibility = settings.ShowElevenLabsQuotaMeter
            ? Visibility.Visible
            : Visibility.Collapsed;

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
            quotaTracker.UpdateFromSubscription(subscription, DateTimeOffset.UtcNow);
            SaveQuotaToSettings();
            speechesUntilReconcile = SpeechesBetweenServerReconcile;

            QuotaDetailStatusText.Text = $"Refreshed at {DateTimeOffset.Now:t}.";
        }
        catch (Exception ex)
        {
            QuotaDetailStatusText.Text = $"Refresh failed: {ex.Message}";
        }
        finally
        {
            isRefreshingQuota = false;
            RefreshQuotaButton.IsEnabled = true;
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
        var snapshot = quotaTracker.Snapshot;

        if (snapshot is null || snapshot.CharacterLimit <= 0)
        {
            QuotaCompactSummaryText.Text = "Save an ElevenLabs API key to see remaining characters.";
            QuotaCompactProgressBar.Visibility = Visibility.Collapsed;
            QuotaCompactDetailText.Text = string.Empty;

            QuotaDetailTierText.Text = "Tier: unknown";
            QuotaDetailCharactersText.Text = "Used: -";
            QuotaDetailRemainingText.Text = "Remaining: -";
            QuotaDetailResetText.Text = "Resets: -";
            QuotaDetailAsOfText.Text = "No data yet.";
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
        QuotaMeterCompactCard.Visibility = settings.ShowElevenLabsQuotaMeter
            ? Visibility.Visible
            : Visibility.Collapsed;

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
