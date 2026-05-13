using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
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
    private readonly BridgeTokenStore bridgeTokenStore;
    private readonly BridgeRuntimeState bridgeRuntimeState;
    private readonly AppSettingsStore settingsStore;
    private readonly AppSettings settings;
    private readonly WindowsStartupRegistration startupRegistration = new();
    private readonly ElevenLabsTextToSpeechClient textToSpeechClient = new();
    private bool isPlaying;
    private bool isInitializing;

    public MainWindow(
        BridgeTokenStore bridgeTokenStore,
        BridgeRuntimeState bridgeRuntimeState,
        AppSettingsStore settingsStore,
        AppSettings settings)
    {
        this.bridgeTokenStore = bridgeTokenStore;
        this.bridgeRuntimeState = bridgeRuntimeState;
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
            throw new InvalidOperationException("Unable to read ElevenLabs API key.", ex);
        }

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            StatusText.Text = "Save an ElevenLabs API key first.";
            AudioPathText.Text = string.Empty;
            throw new InvalidOperationException("Save an ElevenLabs API key first.");
        }

        isPlaying = true;
        SetPlaybackButtonsEnabled(false);
        StatusText.Text = $"Generating ElevenLabs speech from {source}...";
        AudioPathText.Text = string.Empty;

        try
        {
            var path = await textToSpeechClient.CreateSpeechAsync(apiKey, text);
            StatusText.Text = $"Playing ElevenLabs speech from {source}...";
            AudioPathText.Text = path;
            await audioFilePlayer.PlayAsync(path);
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
            CredentialStatusText.Text = $"Saved key to Windows Credential Manager ({DescribeSecret(apiKey)}).";
        }
        catch (Exception ex)
        {
            CredentialStatusText.Text = $"Save failed: {ex.Message}";
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
                return;
            }

            ElevenLabsApiKeyBox.Password = apiKey;
            CredentialStatusText.Text = $"Loaded saved key from Windows Credential Manager ({DescribeSecret(apiKey)}).";
        }
        catch (Exception ex)
        {
            CredentialStatusText.Text = $"Load failed: {ex.Message}";
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
        }
        catch (Exception ex)
        {
            CredentialStatusText.Text = $"Clear failed: {ex.Message}";
        }
    }

    private void CopyBridgeTokenButton_Click(object sender, RoutedEventArgs e)
    {
        CopyBridgeTokenToClipboard();
    }

    private void RefreshBridgeStatusButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshBridgeStatus();
    }

    private void QueueBridgeSpeechCheckBox_Changed(object sender, RoutedEventArgs e)
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

    public void CopyBridgeTokenToClipboard()
    {
        try
        {
            System.Windows.Clipboard.SetText(bridgeTokenStore.EnsureToken());
            BridgeStatusText.Text = $"Copied bridge token. Endpoint: {LocalBridgeServer.BaseUrl}";
        }
        catch (Exception ex)
        {
            BridgeStatusText.Text = $"Copy token failed: {ex.Message}";
        }
    }

    private void RefreshBridgeStatus()
    {
        var speaking = bridgeRuntimeState.IsSpeaking ? "speaking" : "idle";
        var queue = bridgeRuntimeState.QueueBridgeSpeechRequests
            ? $"Queue: {bridgeRuntimeState.PendingSpeechRequests}/{bridgeRuntimeState.MaxQueuedSpeechRequests}."
            : "Queue disabled.";
        BridgeStatusText.Text = $"Bridge listening on {LocalBridgeServer.BaseUrl}. State: {speaking}. {queue} {bridgeRuntimeState.LastStatus}";
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
            bridgeRuntimeState.ConfigureQueue(
                settings.QueueBridgeSpeechRequests,
                settings.MaxQueuedBridgeSpeechRequests);
            SettingsStatusText.Text = DescribeStartupPreferences("Startup preferences loaded.");
            RefreshStartupDiagnostics();
            RefreshBridgeStatus();
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
            settingsStore.Save(settings);
            SettingsStatusText.Text = DescribeStartupPreferences("Saved startup preference.");
            RefreshStartupDiagnostics();
        }
        catch (Exception ex)
        {
            SettingsStatusText.Text = $"Saving startup preference failed: {ex.Message}";
        }
    }

    private void SaveBridgeSpeechSettings()
    {
        settings.QueueBridgeSpeechRequests = QueueBridgeSpeechCheckBox.IsChecked == true;
        settings.MaxQueuedBridgeSpeechRequests = GetSelectedMaxBridgeQueue();
        settings.Normalize();

        try
        {
            settingsStore.Save(settings);
            bridgeRuntimeState.ConfigureQueue(
                settings.QueueBridgeSpeechRequests,
                settings.MaxQueuedBridgeSpeechRequests);
            RefreshBridgeStatus();
        }
        catch (Exception ex)
        {
            BridgeStatusText.Text = $"Saving bridge speech settings failed: {ex.Message}";
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
}
