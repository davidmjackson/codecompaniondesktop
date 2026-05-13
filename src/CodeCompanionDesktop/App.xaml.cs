using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using CodeCompanionDesktop.Bridge;
using CodeCompanionDesktop.History;
using CodeCompanionDesktop.Settings;
using Forms = System.Windows.Forms;
using WpfApplication = System.Windows.Application;

namespace CodeCompanionDesktop;

public partial class App : WpfApplication
{
    private Forms.NotifyIcon? trayIcon;
    private MainWindow? mainWindow;
    private LocalBridgeServer? bridgeServer;
    private SpeechCandidateInboxWatcher? candidateInboxWatcher;
    private BridgeRuntimeState? bridgeRuntimeState;
    private BridgeSpeechQueue? bridgeSpeechQueue;
    private SpeechCandidateProcessor? speechCandidateProcessor;
    private AppSettingsStore? settingsStore;
    private ClientTrustStore? clientTrustStore;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        settingsStore = new AppSettingsStore();
        var settings = settingsStore.Load();
        var runtimeState = new BridgeRuntimeState(new SpeechHistoryStore(), new ProjectRegistryStore());
        runtimeState.ConfigureQueue(settings.QueueBridgeSpeechRequests, settings.MaxQueuedBridgeSpeechRequests);
        bridgeSpeechQueue = new BridgeSpeechQueue(SpeakFromBridgeAsync, runtimeState);
        speechCandidateProcessor = new SpeechCandidateProcessor(SpeakFromBridgeAsync, runtimeState, bridgeSpeechQueue);
        clientTrustStore = new ClientTrustStore();
        bridgeRuntimeState = runtimeState;

        mainWindow = new MainWindow(runtimeState, clientTrustStore, settingsStore, settings);
        ConfigureTrayIcon();
        StartBridgeServer(runtimeState);
        StartCandidateInbox(runtimeState);

        if (settings.StartHiddenToTray)
        {
            trayIcon?.ShowBalloonTip(
                2500,
                "Code Companion Desktop",
                $"Running in tray. Bridge listening on port {LocalBridgeServer.Port}.",
                Forms.ToolTipIcon.Info);
            return;
        }

        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        bridgeServer?.Dispose();
        candidateInboxWatcher?.Dispose();
        trayIcon?.Dispose();
        base.OnExit(e);
    }

    public void ShowMainWindow()
    {
        if (mainWindow is null)
        {
            return;
        }

        if (!mainWindow.IsVisible)
        {
            mainWindow.Show();
        }

        if (mainWindow.WindowState == WindowState.Minimized)
        {
            mainWindow.WindowState = WindowState.Normal;
        }

        mainWindow.Activate();
    }

    public void ExitApplication()
    {
        if (mainWindow is not null)
        {
            mainWindow.AllowClose = true;
        }

        if (trayIcon is not null)
        {
            trayIcon.Visible = false;
        }

        Shutdown();
    }

    private void ConfigureTrayIcon()
    {
        trayIcon = new Forms.NotifyIcon
        {
            Icon = CreateTrayIcon(),
            Text = "Code Companion Desktop",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };

        trayIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private static Icon CreateTrayIcon()
    {
        var resourceInfo = WpfApplication.GetResourceStream(new Uri("pack://application:,,,/Assets/tray.png", UriKind.Absolute));
        if (resourceInfo is null)
        {
            return SystemIcons.Application;
        }

        using var stream = resourceInfo.Stream;
        using var bitmap = new Bitmap(stream);
        var iconHandle = bitmap.GetHicon();

        try
        {
            using var icon = Icon.FromHandle(iconHandle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(iconHandle);
        }
    }

    private Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new Forms.ContextMenuStrip();

        menu.Items.Add("Show", null, (_, _) => ShowMainWindow());
        menu.Items.Add("Hide to Tray", null, (_, _) => mainWindow?.Hide());
        menu.Items.Add("Bridge Status", null, (_, _) => ShowBridgeStatus());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Play Test Sound", null, async (_, _) =>
        {
            ShowMainWindow();

            if (mainWindow is not null)
            {
                await mainWindow.PlayTestSoundAsync();
            }
        });
        menu.Items.Add("Play ElevenLabs Test", null, async (_, _) =>
        {
            ShowMainWindow();

            if (mainWindow is not null)
            {
                await mainWindow.PlayElevenLabsTestSpeechAsync();
            }
        });
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());

        return menu;
    }

    private void StartBridgeServer(BridgeRuntimeState runtimeState)
    {
        if (mainWindow is null || bridgeSpeechQueue is null || speechCandidateProcessor is null)
        {
            return;
        }

        try
        {
            bridgeServer = new LocalBridgeServer(
                SpeakFromBridgeAsync,
                runtimeState,
                bridgeSpeechQueue,
                speechCandidateProcessor: speechCandidateProcessor,
                clientTrustStore: clientTrustStore);
            bridgeServer.Start();
            mainWindow.SetBridgeStatus($"Bridge listening on {LocalBridgeServer.BaseUrl}");
        }
        catch (Exception ex)
        {
            mainWindow.SetBridgeStatus($"Bridge failed to start: {ex.Message}");
        }
    }

    private void StartCandidateInbox(BridgeRuntimeState runtimeState)
    {
        if (speechCandidateProcessor is null || mainWindow is null)
        {
            return;
        }

        try
        {
            candidateInboxWatcher = new SpeechCandidateInboxWatcher(
                SpeechCandidateInboxWatcher.GetDefaultInboxDirectory(),
                speechCandidateProcessor,
                runtimeState);
            candidateInboxWatcher.Start();
            mainWindow.SetBridgeStatus(
                $"Bridge listening on {LocalBridgeServer.BaseUrl}. Candidate inbox: {candidateInboxWatcher.InboxDirectory}");
        }
        catch (Exception ex)
        {
            runtimeState.RecordCandidateInboxError(ex.Message);
            mainWindow.SetBridgeStatus($"Candidate inbox failed to start: {ex.Message}");
        }
    }

    private Task SpeakFromBridgeAsync(string text)
    {
        if (mainWindow is null)
        {
            return Task.CompletedTask;
        }

        return mainWindow.Dispatcher.InvokeAsync(() => mainWindow.PlayBridgeSpeechAsync(text)).Task.Unwrap();
    }

    private void ShowBridgeStatus()
    {
        ShowMainWindow();

        if (mainWindow is null)
        {
            return;
        }

        var bridge = bridgeServer?.IsRunning == true ? "listening" : "stopped";
        var speaking = bridgeRuntimeState?.IsSpeaking == true ? "speaking" : "idle";
        var lastStatus = bridgeRuntimeState?.LastStatus ?? "No bridge status available.";
        mainWindow.SetBridgeStatus($"Bridge {bridge} on {LocalBridgeServer.BaseUrl}. State: {speaking}. {lastStatus}");
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
