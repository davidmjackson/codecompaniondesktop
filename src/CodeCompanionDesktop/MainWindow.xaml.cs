using System;
using System.ComponentModel;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using CodeCompanionDesktop.Audio;
using CodeCompanionDesktop.Bridge;
using CodeCompanionDesktop.Credentials;
using CodeCompanionDesktop.ElevenLabs;
using CodeCompanionDesktop.Settings;
using WpfApplication = System.Windows.Application;

namespace CodeCompanionDesktop;

public partial class MainWindow : Window
{
    private readonly TestTonePlayer testTonePlayer = new();
    private readonly AudioFilePlayer audioFilePlayer = new();
    private readonly WindowsCredentialStore credentialStore = new();
    private readonly BridgeRuntimeState bridgeRuntimeState;
    private readonly ClientTrustStore clientTrustStore;
    private readonly AppSettingsStore settingsStore;
    private readonly AppSettings settings;
    private readonly WindowsStartupRegistration startupRegistration = new();
    private readonly ElevenLabsTextToSpeechClient textToSpeechClient = new();
    private bool isPlaying;
    private bool isInitializing;

    public MainWindow(
        BridgeRuntimeState bridgeRuntimeState,
        ClientTrustStore clientTrustStore,
        AppSettingsStore settingsStore,
        AppSettings settings)
    {
        this.bridgeRuntimeState = bridgeRuntimeState;
        this.clientTrustStore = clientTrustStore;
        this.settingsStore = settingsStore;
        this.settings = settings;
        this.settings.Normalize();
        InitializeComponent();
        LoadSettings();
    }

    public bool AllowClose { get; set; }

    public async Task PlayTestSoundAsync()
    {
        if (isPlaying)
        {
            return;
        }

        isPlaying = true;
        SetPlaybackButtonsEnabled(false);
        StatusText.Text = "Playing generated local WAV test tone...";
        AudioPathText.Text = string.Empty;

        try
        {
            var path = await testTonePlayer.PlayAsync();
            StatusText.Text = "Playback completed.";
            AudioPathText.Text = path;
        }
        catch (Exception ex)
        {
            StatusText.Text = "Playback failed.";
            AudioPathText.Text = ex.Message;
        }
        finally
        {
            isPlaying = false;
            SetPlaybackButtonsEnabled(true);
            RefreshReadinessSummary();
        }
    }

    public async Task PlayElevenLabsTestSpeechAsync()
    {
        try
        {
            await PlayElevenLabsSpeechAsync("Code Companion desktop speech test.", "ElevenLabs test");
        }
        catch
        {
            // The UI has already been updated with the playback error.
        }
    }

    public async Task PlayBridgeSpeechAsync(string text)
    {
        await PlayElevenLabsSpeechAsync(text, "bridge request");
        BridgeStatusText.Text = $"Last bridge request completed. Endpoint: {LocalBridgeServer.BaseUrl}";
    }

    public void SetBridgeStatus(string status)
    {
        BridgeStatusText.Text = status;
        RefreshSpeechDiagnostics();
        RefreshProjectRegistry();
        RefreshProjectSpeechHistory();
        RefreshClientPairing();
    }

    private async Task PlayElevenLabsSpeechAsync(string text, string source)
    {
        if (isPlaying)
        {
            throw new InvalidOperationException("Speech playback is already in progress.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ArgumentException("Speech text cannot be empty.", nameof(text));
        }

        if (text.Length > 1000)
        {
            throw new ArgumentException("Speech text cannot be longer than 1000 characters.", nameof(text));
        }

        string? apiKey;
        try
        {
            apiKey = credentialStore.ReadSecret(WindowsCredentialStore.ElevenLabsApiKeyTarget)?.Trim();
        }
        catch (Exception ex)
        {
            StatusText.Text = "Unable to read ElevenLabs API key.";
            AudioPathText.Text = ex.Message;
            bridgeRuntimeState.RecordProviderError(ex.Message);
            RefreshSpeechDiagnostics();
            throw new InvalidOperationException("Unable to read ElevenLabs API key.", ex);
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            StatusText.Text = "Save an ElevenLabs API key first.";
            AudioPathText.Text = string.Empty;
            bridgeRuntimeState.RecordProviderError("No saved ElevenLabs API key.");
            RefreshSpeechDiagnostics();
            throw new InvalidOperationException("Save an ElevenLabs API key first.");
        }

        isPlaying = true;
        SetPlaybackButtonsEnabled(false);
        StatusText.Text = $"Generating ElevenLabs speech from {source}...";
        AudioPathText.Text = string.Empty;

        try
        {
            settings.Normalize();
            if (!string.Equals(settings.SpeechProvider, AppSettings.ElevenLabsProvider, StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"Unsupported speech provider: {settings.SpeechProvider}");
            }

            var speechOptions = new ElevenLabsSpeechOptions(
                settings.ElevenLabsVoiceId,
                settings.ElevenLabsModelId,
                settings.ElevenLabsOutputFormat);
            string path;
            try
            {
                path = await textToSpeechClient.CreateSpeechAsync(apiKey, text, speechOptions);
                bridgeRuntimeState.ClearProviderError();
                OnSpeechProduced(text.Length);
            }
            catch (Exception ex)
            {
                bridgeRuntimeState.RecordProviderError(ex.Message);
                throw;
            }

            StatusText.Text = $"Playing ElevenLabs speech from {source}...";
            AudioPathText.Text = path;
            try
            {
                await audioFilePlayer.PlayAsync(path);
                bridgeRuntimeState.RecordPlaybackCompleted(source);
            }
            catch (Exception ex)
            {
                bridgeRuntimeState.RecordPlaybackError(ex.Message);
                throw;
            }

            StatusText.Text = $"ElevenLabs playback completed from {source}.";
        }
        catch (Exception ex)
        {
            StatusText.Text = "ElevenLabs playback failed.";
            AudioPathText.Text = ex.Message;
            throw;
        }
        finally
        {
            isPlaying = false;
            SetPlaybackButtonsEnabled(true);
            RefreshSpeechDiagnostics();
        }
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!AllowClose)
        {
            e.Cancel = true;
            Hide();
            return;
        }

        base.OnClosing(e);
    }

    private async void PlayButton_Click(object sender, RoutedEventArgs e)
    {
        await PlayTestSoundAsync();
    }

    private async void PlayElevenLabsButton_Click(object sender, RoutedEventArgs e)
    {
        await PlayElevenLabsTestSpeechAsync();
    }

    private void HideButton_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void ExitButton_Click(object sender, RoutedEventArgs e)
    {
        if (WpfApplication.Current is App app)
        {
            app.ExitApplication();
        }
    }

    private void StartHiddenToTrayCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (isInitializing)
        {
            return;
        }

        settings.StartHiddenToTray = StartHiddenToTrayCheckBox.IsChecked == true;
        SaveSettings();
    }

    private void StartWithWindowsCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (isInitializing)
        {
            return;
        }

        SaveWindowsStartupRegistration();
    }

    private void SaveProviderSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        settings.SpeechProvider = GetSelectedSpeechProvider();
        settings.ElevenLabsVoiceId = ElevenLabsVoiceIdTextBox.Text;
        settings.ElevenLabsModelId = ElevenLabsModelIdTextBox.Text;
        settings.ElevenLabsOutputFormat = ElevenLabsOutputFormatTextBox.Text;
        settings.Normalize();
        ApplyProviderSettingsToUi();

        try
        {
            settingsStore.Save(settings);
            ProviderSettingsStatusText.Text = $"Saved {settings.SpeechProvider} voice settings.";
            RefreshSpeechDiagnostics();
            RefreshReadinessSummary();
        }
        catch (Exception ex)
        {
            ProviderSettingsStatusText.Text = $"Saving provider settings failed: {ex.Message}";
            RefreshReadinessSummary();
        }
    }

    private void SaveApiKeyButton_Click(object sender, RoutedEventArgs e)
    {
        var apiKey = ElevenLabsApiKeyBox.Password.Trim();
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            CredentialStatusText.Text = "Enter an ElevenLabs API key before saving.";
            return;
        }

        try
        {
            credentialStore.SaveSecret(
                WindowsCredentialStore.ElevenLabsApiKeyTarget,
                "ElevenLabs",
                apiKey);
            CredentialStatusText.Text = $"Key loaded. Saved to Windows Credential Manager ({DescribeSecret(apiKey)}).";
            RefreshReadinessSummary();
        }
        catch (Exception ex)
        {
            CredentialStatusText.Text = $"Save failed: {ex.Message}";
            RefreshReadinessSummary();
        }
    }

    private void LoadApiKeyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var apiKey = credentialStore.ReadSecret(WindowsCredentialStore.ElevenLabsApiKeyTarget);
            if (apiKey is null)
            {
                ElevenLabsApiKeyBox.Clear();
                CredentialStatusText.Text = "No saved ElevenLabs API key found.";
                RefreshReadinessSummary();
                return;
            }

            ElevenLabsApiKeyBox.Password = apiKey;
            CredentialStatusText.Text = $"Key loaded from Windows Credential Manager ({DescribeSecret(apiKey)}).";
            RefreshReadinessSummary();
        }
        catch (Exception ex)
        {
            CredentialStatusText.Text = $"Load failed: {ex.Message}";
            RefreshReadinessSummary();
        }
    }

    private void ClearApiKeyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var deleted = credentialStore.DeleteSecret(WindowsCredentialStore.ElevenLabsApiKeyTarget);
            ElevenLabsApiKeyBox.Clear();
            CredentialStatusText.Text = deleted
                ? "Deleted saved ElevenLabs API key."
                : "No saved ElevenLabs API key found.";
            RefreshReadinessSummary();
        }
        catch (Exception ex)
        {
            CredentialStatusText.Text = $"Clear failed: {ex.Message}";
            RefreshReadinessSummary();
        }
    }

    private void RefreshBridgeStatusButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshBridgeStatus();
    }

    private void RefreshSpeechDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshSpeechDiagnostics();
    }

    private void CopySpeechDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshSpeechDiagnostics();

        try
        {
            System.Windows.Clipboard.SetText(SpeechDiagnosticsTextBox.Text);
            BridgeStatusText.Text = "Copied speech diagnostics.";
            RefreshReadinessSummary();
        }
        catch (Exception ex)
        {
            BridgeStatusText.Text = $"Copying speech diagnostics failed: {ex.Message}";
            RefreshReadinessSummary();
        }
    }

    private void RefreshProjectRegistryButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshProjectRegistry();
    }

    private void CopyProjectRegistryButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshProjectRegistry();

        try
        {
            System.Windows.Clipboard.SetText(ProjectRegistryTextBox.Text);
            BridgeStatusText.Text = "Copied project registry.";
            RefreshReadinessSummary();
        }
        catch (Exception ex)
        {
            BridgeStatusText.Text = $"Copying project registry failed: {ex.Message}";
            RefreshReadinessSummary();
        }
    }

    private void AddProjectAliasButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateProjectAlias(isAdd: true);
    }

    private void RemoveProjectAliasButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateProjectAlias(isAdd: false);
    }

    private void RefreshProjectSpeechHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshProjectSpeechHistory();
    }

    private void CopyProjectSpeechHistoryButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshProjectSpeechHistory();

        try
        {
            System.Windows.Clipboard.SetText(ProjectSpeechHistoryTextBox.Text);
            BridgeStatusText.Text = "Copied project speech history.";
            RefreshReadinessSummary();
        }
        catch (Exception ex)
        {
            BridgeStatusText.Text = $"Copying project speech history failed: {ex.Message}";
            RefreshReadinessSummary();
        }
    }

    private void RefreshClientPairingButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshClientPairing();
    }

    private void CopyClientPairingButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshClientPairing();

        try
        {
            System.Windows.Clipboard.SetText(ClientPairingTextBox.Text);
            ClientPairingStatusText.Text = "Copied client pairing diagnostics.";
        }
        catch (Exception ex)
        {
            ClientPairingStatusText.Text = $"Copying client pairing diagnostics failed: {ex.Message}";
        }

        RefreshReadinessSummary();
    }

    private void ApproveClientPairingButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateClientPairing(ClientTrustStore.Allowed);
    }

    private void ApprovePendingClientPairingButton_Click(object sender, RoutedEventArgs e)
    {
        if (!clientTrustStore.TrySetMostRecentPendingAuthorization(ClientTrustStore.Allowed, out var client))
        {
            ClientPairingStatusText.Text = "No pending clients to approve.";
            RefreshClientPairing();
            RefreshReadinessSummary();
            return;
        }

        ClientPairingClientIdTextBox.Text = client!.ClientId;
        ClientPairingStatusText.Text = $"Approved pending client {client.Name} ({client.ClientId}).";
        RefreshClientPairing();
        RefreshReadinessSummary();
    }

    private void DenyClientPairingButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateClientPairing(ClientTrustStore.Denied);
    }

    private void QueueBridgeSpeechCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (isInitializing)
        {
            return;
        }

        SaveBridgeSpeechSettings();
    }

    private void SelfTestPlaybackCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (isInitializing)
        {
            return;
        }

        SaveBridgeSpeechSettings();
    }

    private void MaxBridgeQueueComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (isInitializing)
        {
            return;
        }

        SaveBridgeSpeechSettings();
    }

    private void RefreshStartupDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshStartupDiagnostics();
        SettingsStatusText.Text = DescribeStartupPreferences("Refreshed startup diagnostics.");
    }

    private void CopyStartupDiagnosticsButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshStartupDiagnostics();

        try
        {
            System.Windows.Clipboard.SetText(StartupDiagnosticsTextBox.Text);
            SettingsStatusText.Text = DescribeStartupPreferences("Copied startup diagnostics.");
        }
        catch (Exception ex)
        {
            SettingsStatusText.Text = $"Copying startup diagnostics failed: {ex.Message}";
        }
    }

    private void RefreshBridgeStatus()
    {
        var speaking = bridgeRuntimeState.IsSpeaking ? "speaking" : "idle";
        var queue = bridgeRuntimeState.QueueBridgeSpeechRequests
            ? $"Queue: {bridgeRuntimeState.PendingSpeechRequests}/{bridgeRuntimeState.MaxQueuedSpeechRequests}."
            : "Queue disabled.";
        BridgeStatusText.Text = $"Bridge listening on {LocalBridgeServer.BaseUrl}. State: {speaking}. Profile: {bridgeRuntimeState.SpeechProfiles.ActiveProfileName}. {queue} {bridgeRuntimeState.LastStatus}";
        RefreshSpeechProfileStatus();
        RefreshSpeechDiagnostics();
        RefreshProjectRegistry();
        RefreshProjectSpeechHistory();
        RefreshClientPairing();
        RefreshReadinessSummary();
    }

    private void RefreshSpeechDiagnostics()
    {
        var snapshot = bridgeRuntimeState.GetDiagnosticsSnapshot();
        RefreshSpeechProfileStatus();
        var queue = snapshot.QueueBridgeSpeechRequests
            ? $"{snapshot.PendingSpeechRequests}/{snapshot.MaxQueuedSpeechRequests}"
            : "disabled";
        var providerKeyStatus = GetProviderKeyStatus();
        var recentClients = snapshot.RecentBridgeClients.Count == 0
            ? "Recent clients: none"
            : string.Join(
                Environment.NewLine,
                snapshot.RecentBridgeClients.Select(client => $"Client: {client}"));
        var recentProjects = snapshot.RecentProjects.Count == 0
            ? "Recent projects: none"
            : string.Join(
                Environment.NewLine,
                snapshot.RecentProjects.Select(project => $"Project: {project}"));
        var recent = snapshot.RecentSpeechResults.Count == 0
            ? "Recent speech results: none"
            : string.Join(
                Environment.NewLine,
                snapshot.RecentSpeechResults.Select(result => $"Recent: {result}"));

        SpeechDiagnosticsTextBox.Text = string.Join(
            Environment.NewLine,
            $"Bridge endpoint: {LocalBridgeServer.BaseUrl}",
            $"Bridge state: {(snapshot.IsSpeaking ? "speaking" : "idle")}",
            $"Speech profile: {snapshot.ActiveSpeechProfile}",
            $"Last speech profile change: {snapshot.LastSpeechProfileChange}",
            $"Queue: {queue}",
            $"Provider: {settings.SpeechProvider}",
            $"ElevenLabs voice ID: {settings.ElevenLabsVoiceId}",
            $"ElevenLabs model ID: {settings.ElevenLabsModelId}",
            $"ElevenLabs output format: {settings.ElevenLabsOutputFormat}",
            $"Provider key: {providerKeyStatus}",
            $"Last client: {snapshot.LastClient}",
            $"Last candidate: {snapshot.LastSpeechCandidate}",
            $"Last decision: {snapshot.LastSpeechDecision}",
            $"Last provider error: {snapshot.LastProviderError}",
            $"Last playback error: {snapshot.LastPlaybackError}",
            $"Last bridge status: {snapshot.LastStatus}",
            recentProjects,
            recentClients,
            recent);
        RefreshReadinessSummary();
    }

    private async void EndDemoModeButton_Click(object sender, RoutedEventArgs e)
    {
        bridgeRuntimeState.DisableDemoMode();
        RefreshBridgeStatus();

        try
        {
            await PlayBridgeSpeechAsync("Demo Mode is off. Standard speech policy is restored.");
        }
        catch (Exception ex)
        {
            BridgeStatusText.Text = $"Demo Mode ended. Spoken acknowledgement failed: {ex.Message}";
            RefreshSpeechDiagnostics();
        }
    }

    private void RefreshSpeechProfileStatus()
    {
        var snapshot = bridgeRuntimeState.GetDiagnosticsSnapshot();
        var isDemo = string.Equals(snapshot.ActiveSpeechProfile, nameof(SpeechProfile.Demo), StringComparison.Ordinal);
        SpeechProfileStatusText.Text = isDemo
            ? "Speech profile: Demo Mode. Codex will speak more often during this Desktop session."
            : "Speech profile: Standard. Codex will use the normal speech policy.";
        EndDemoModeButton.IsEnabled = isDemo;
    }

    private void RefreshProjectRegistry()
    {
        var projectDetails = bridgeRuntimeState.LoadProjectRegistryDetails(20);
        ProjectRegistryTextBox.Text = projectDetails.Count == 0
            ? "No projects observed yet."
            : string.Join(
                $"{Environment.NewLine}{Environment.NewLine}",
                projectDetails);
    }

    private void RefreshProjectSpeechHistory()
    {
        var projectHistory = bridgeRuntimeState.LoadProjectSpeechHistoryDetails(20);
        ProjectSpeechHistoryTextBox.Text = projectHistory.Count == 0
            ? "No project speech history yet."
            : string.Join(
                $"{Environment.NewLine}{Environment.NewLine}",
                projectHistory);
    }

    private void RefreshClientPairing()
    {
        var clientDetails = clientTrustStore.LoadClientDetails(30);
        ClientPairingTextBox.Text = clientDetails.Count == 0
            ? "No bridge clients observed yet."
            : string.Join(
                $"{Environment.NewLine}{Environment.NewLine}",
                clientDetails);
        RefreshReadinessSummary();
    }

    private void UpdateProjectAlias(bool isAdd)
    {
        var projectId = ProjectAliasProjectIdTextBox.Text.Trim();
        var root = ProjectAliasRootTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(projectId) || string.IsNullOrWhiteSpace(root))
        {
            ProjectRegistryStatusText.Text = "Enter a project ID and root alias.";
            return;
        }

        var updated = isAdd
            ? bridgeRuntimeState.TryAddProjectRootAlias(projectId, root)
            : bridgeRuntimeState.TryRemoveProjectRootAlias(projectId, root);
        if (!updated)
        {
            ProjectRegistryStatusText.Text = isAdd
                ? $"Alias was not added. Project not found: {projectId}."
                : $"Alias was not removed. Project or alias not found: {projectId}.";
            RefreshProjectRegistry();
            return;
        }

        ProjectRegistryStatusText.Text = isAdd
            ? $"Added root alias for {projectId}."
            : $"Removed root alias for {projectId}.";
        RefreshSpeechDiagnostics();
        RefreshProjectRegistry();
    }

    private void UpdateClientPairing(string authorization)
    {
        var clientId = ClientPairingClientIdTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(clientId))
        {
            ClientPairingStatusText.Text = "Enter a client ID.";
            return;
        }

        if (!clientTrustStore.TrySetAuthorization(clientId, authorization))
        {
            ClientPairingStatusText.Text = $"Client not found: {clientId}.";
            RefreshClientPairing();
            return;
        }

        ClientPairingStatusText.Text = $"{authorization} client {clientId}.";
        RefreshClientPairing();
        RefreshReadinessSummary();
    }

    private string GetProviderKeyStatus()
    {
        try
        {
            var apiKey = credentialStore.ReadSecret(WindowsCredentialStore.ElevenLabsApiKeyTarget);
            return string.IsNullOrWhiteSpace(apiKey)
                ? "missing"
                : $"saved ({DescribeSecret(apiKey)})";
        }
        catch (Exception ex)
        {
            return $"unavailable: {ex.Message}";
        }
    }

    private void RefreshCredentialStatus()
    {
        try
        {
            var apiKey = credentialStore.ReadSecret(WindowsCredentialStore.ElevenLabsApiKeyTarget);
            CredentialStatusText.Text = string.IsNullOrWhiteSpace(apiKey)
                ? "No key loaded."
                : $"Key loaded ({DescribeSecret(apiKey)}).";
        }
        catch (Exception ex)
        {
            CredentialStatusText.Text = $"Key status unavailable: {ex.Message}";
        }
    }

    private void RefreshReadinessSummary()
    {
        if (ReadinessIconText is null)
        {
            return;
        }

        var issues = new List<string>();
        var providerKeyStatus = GetProviderKeyStatus();
        if (providerKeyStatus.StartsWith("missing", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add("Save an ElevenLabs API key.");
        }
        else if (providerKeyStatus.StartsWith("unavailable", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add($"Check provider key storage: {providerKeyStatus}.");
        }

        var snapshot = bridgeRuntimeState.GetDiagnosticsSnapshot();
        if (IsActiveError(snapshot.LastProviderError, "No provider errors."))
        {
            issues.Add($"Provider error: {snapshot.LastProviderError}");
        }

        if (IsActiveError(snapshot.LastPlaybackError, "No playback errors."))
        {
            issues.Add($"Playback error: {snapshot.LastPlaybackError}");
        }

        var clients = clientTrustStore.Load().Clients;
        var pendingCount = clients.Count(
            client => string.Equals(client.Authorization, ClientTrustStore.Pending, StringComparison.OrdinalIgnoreCase));
        if (pendingCount > 0)
        {
            issues.Add(pendingCount == 1
                ? "Approve the pending VS Code client."
                : $"Approve {pendingCount} pending VS Code clients.");
        }

        var deniedCount = clients.Count(
            client => string.Equals(client.Authorization, ClientTrustStore.Denied, StringComparison.OrdinalIgnoreCase));
        if (deniedCount > 0)
        {
            issues.Add(deniedCount == 1
                ? "One VS Code client is denied."
                : $"{deniedCount} VS Code clients are denied.");
        }

        var isHealthy = issues.Count == 0;
        var profile = bridgeRuntimeState.SpeechProfiles.ActiveProfileName;
        ReadinessIconText.Text = isHealthy ? "✓" : "✕";
        ReadinessIconText.Foreground = isHealthy ? System.Windows.Media.Brushes.ForestGreen : System.Windows.Media.Brushes.Firebrick;
        ReadinessSummaryText.Text = isHealthy
            ? profile == nameof(SpeechProfile.Demo)
                ? "All systems are working and Demo Mode is active."
                : "All systems are working and APIs are configured."
            : "Code Companion needs attention.";
        ReadinessDetailsText.Text = isHealthy
            ? $"Bridge is listening on {LocalBridgeServer.BaseUrl}. Provider key is loaded. Speech profile: {profile}. No active provider or playback errors."
            : string.Join(Environment.NewLine, issues.Select(issue => $"- {issue}"));
    }

    private static bool IsActiveError(string value, string emptyValue)
    {
        return !string.IsNullOrWhiteSpace(value)
            && !string.Equals(value, emptyValue, StringComparison.OrdinalIgnoreCase);
    }

    private static string DescribeSecret(string secret)
    {
        return $"{secret.Length} characters";
    }

    private void SetPlaybackButtonsEnabled(bool isEnabled)
    {
        PlayButton.IsEnabled = isEnabled;
        PlayElevenLabsButton.IsEnabled = isEnabled;
    }

    private void LoadSettings()
    {
        isInitializing = true;
        try
        {
            StartHiddenToTrayCheckBox.IsChecked = settings.StartHiddenToTray;
            StartWithWindowsCheckBox.IsChecked = startupRegistration.IsRegistered();
            QueueBridgeSpeechCheckBox.IsChecked = settings.QueueBridgeSpeechRequests;
            SetMaxBridgeQueueSelection(settings.MaxQueuedBridgeSpeechRequests);
            SelfTestPlaybackCheckBox.IsChecked = !string.Equals(
                settings.SelfTestPlayback, AppSettings.SelfTestPlaybackSilent, StringComparison.Ordinal);
            ApplyProviderSettingsToUi();
            RefreshCredentialStatus();
            bridgeRuntimeState.ConfigureQueue(
                settings.QueueBridgeSpeechRequests,
                settings.MaxQueuedBridgeSpeechRequests);
            bridgeRuntimeState.ConfigureSelfTestPlayback(
                string.Equals(settings.SelfTestPlayback, AppSettings.SelfTestPlaybackSilent, StringComparison.Ordinal));
            SettingsStatusText.Text = DescribeStartupPreferences("Startup preferences loaded.");
            RefreshStartupDiagnostics();
            RefreshBridgeStatus();
            RefreshSpeechProfileStatus();
            RefreshSpeechDiagnostics();
            RefreshProjectRegistry();
            RefreshProjectSpeechHistory();
            RefreshClientPairing();
            WireQuotaMeter();
            RefreshReadinessSummary();
        }
        finally
        {
            isInitializing = false;
        }
    }

    private void SaveSettings()
    {
        try
        {
            settings.Normalize();
            settingsStore.Save(settings);
            SettingsStatusText.Text = DescribeStartupPreferences("Saved startup preference.");
            RefreshStartupDiagnostics();
            RefreshReadinessSummary();
        }
        catch (Exception ex)
        {
            SettingsStatusText.Text = $"Saving startup preference failed: {ex.Message}";
            RefreshReadinessSummary();
        }
    }

    private void SaveBridgeSpeechSettings()
    {
        settings.QueueBridgeSpeechRequests = QueueBridgeSpeechCheckBox.IsChecked == true;
        settings.MaxQueuedBridgeSpeechRequests = GetSelectedMaxBridgeQueue();
        settings.SelfTestPlayback = SelfTestPlaybackCheckBox.IsChecked == true
            ? AppSettings.SelfTestPlaybackSpeak
            : AppSettings.SelfTestPlaybackSilent;
        settings.Normalize();

        try
        {
            settingsStore.Save(settings);
            bridgeRuntimeState.ConfigureQueue(
                settings.QueueBridgeSpeechRequests,
                settings.MaxQueuedBridgeSpeechRequests);
            bridgeRuntimeState.ConfigureSelfTestPlayback(
                string.Equals(settings.SelfTestPlayback, AppSettings.SelfTestPlaybackSilent, StringComparison.Ordinal));
            RefreshBridgeStatus();
            RefreshReadinessSummary();
        }
        catch (Exception ex)
        {
            BridgeStatusText.Text = $"Saving bridge speech settings failed: {ex.Message}";
            RefreshReadinessSummary();
        }
    }

    private void SaveWindowsStartupRegistration()
    {
        try
        {
            if (StartWithWindowsCheckBox.IsChecked == true)
            {
                startupRegistration.Register();
            }
            else
            {
                startupRegistration.Unregister();
            }

            SettingsStatusText.Text = DescribeStartupPreferences("Saved Windows sign-in preference.");
            RefreshStartupDiagnostics();
            RefreshReadinessSummary();
        }
        catch (Exception ex)
        {
            isInitializing = true;
            try
            {
                StartWithWindowsCheckBox.IsChecked = startupRegistration.IsRegistered();
            }
            finally
            {
                isInitializing = false;
            }

            SettingsStatusText.Text = $"Saving Windows sign-in preference failed: {ex.Message}";
            RefreshStartupDiagnostics();
            RefreshReadinessSummary();
        }
    }

    private string DescribeStartupPreferences(string prefix)
    {
        var windowBehavior = settings.StartHiddenToTray
            ? "starts hidden to tray"
            : "shows the window";
        var loginBehavior = StartWithWindowsCheckBox.IsChecked == true
            ? "starts with Windows sign-in"
            : "does not start with Windows sign-in";

        return $"{prefix} App {windowBehavior} and {loginBehavior}.";
    }

    private void RefreshStartupDiagnostics()
    {
        var diagnostics = startupRegistration.GetDiagnostics();
        var registeredCommand = string.IsNullOrWhiteSpace(diagnostics.RegisteredCommand)
            ? "(not registered)"
            : diagnostics.RegisteredCommand;
        var registeredExecutable = string.IsNullOrWhiteSpace(diagnostics.RegisteredExecutablePath)
            ? "(not available)"
            : diagnostics.RegisteredExecutablePath;
        var currentExecutable = string.IsNullOrWhiteSpace(diagnostics.CurrentExecutablePath)
            ? "(not available)"
            : diagnostics.CurrentExecutablePath;

        StartupDiagnosticsTextBox.Text = string.Join(
            Environment.NewLine,
            $"Registry path: {diagnostics.RegistryPath}",
            $"Value name: {diagnostics.ValueName}",
            $"Registered command: {registeredCommand}",
            $"Registered executable: {registeredExecutable}",
            $"Target exists: {DescribeBoolean(diagnostics.RegisteredTargetExists)}",
            $"Matches this app: {DescribeBoolean(diagnostics.RegisteredExecutableMatchesCurrent)}",
            $"This app executable: {currentExecutable}");
    }

    private static string DescribeBoolean(bool value)
    {
        return value ? "yes" : "no";
    }

    private void SetMaxBridgeQueueSelection(int value)
    {
        foreach (var item in MaxBridgeQueueComboBox.Items)
        {
            if (item is System.Windows.Controls.ComboBoxItem comboBoxItem
                && int.TryParse(comboBoxItem.Content?.ToString(), out var itemValue)
                && itemValue == value)
            {
                MaxBridgeQueueComboBox.SelectedItem = comboBoxItem;
                return;
            }
        }

        MaxBridgeQueueComboBox.SelectedIndex = 1;
    }

    private int GetSelectedMaxBridgeQueue()
    {
        if (MaxBridgeQueueComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem comboBoxItem
            && int.TryParse(comboBoxItem.Content?.ToString(), out var value))
        {
            return value;
        }

        return AppSettings.DefaultMaxQueuedBridgeSpeechRequests;
    }

    private void ApplyProviderSettingsToUi()
    {
        SetSpeechProviderSelection(settings.SpeechProvider);
        ElevenLabsVoiceIdTextBox.Text = settings.ElevenLabsVoiceId;
        ElevenLabsModelIdTextBox.Text = settings.ElevenLabsModelId;
        ElevenLabsOutputFormatTextBox.Text = settings.ElevenLabsOutputFormat;
        ProviderSettingsStatusText.Text = $"{settings.SpeechProvider} voice settings loaded.";
    }

    private void SetSpeechProviderSelection(string value)
    {
        foreach (var item in SpeechProviderComboBox.Items)
        {
            if (item is System.Windows.Controls.ComboBoxItem comboBoxItem
                && string.Equals(comboBoxItem.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
            {
                SpeechProviderComboBox.SelectedItem = comboBoxItem;
                return;
            }
        }

        SpeechProviderComboBox.SelectedIndex = 0;
    }

    private string GetSelectedSpeechProvider()
    {
        if (SpeechProviderComboBox.SelectedItem is System.Windows.Controls.ComboBoxItem comboBoxItem
            && !string.IsNullOrWhiteSpace(comboBoxItem.Content?.ToString()))
        {
            return comboBoxItem.Content.ToString()!;
        }

        return AppSettings.ElevenLabsProvider;
    }
}
