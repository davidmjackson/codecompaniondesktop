using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
using CodeCompanionDesktop.Bridge;
using CodeCompanionDesktop.Credentials;
using Forms = System.Windows.Forms;
using WpfApplication = System.Windows.Application;

namespace CodeCompanionDesktop;

public partial class App : WpfApplication
{
    private Forms.NotifyIcon? trayIcon;
    private MainWindow? mainWindow;
    private LocalBridgeServer? bridgeServer;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var credentialStore = new WindowsCredentialStore();
        var bridgeTokenStore = new BridgeTokenStore(credentialStore);
        var bridgeToken = bridgeTokenStore.EnsureToken();

        mainWindow = new MainWindow(bridgeTokenStore);
        ConfigureTrayIcon();
        StartBridgeServer(bridgeToken);
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        bridgeServer?.Dispose();
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
            Icon = SystemIcons.Application,
            Text = "Code Companion Desktop",
            Visible = true,
            ContextMenuStrip = BuildTrayMenu()
        };

        trayIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private Forms.ContextMenuStrip BuildTrayMenu()
    {
        var menu = new Forms.ContextMenuStrip();

        menu.Items.Add("Show", null, (_, _) => ShowMainWindow());
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

    private void StartBridgeServer(string bridgeToken)
    {
        if (mainWindow is null)
        {
            return;
        }

        try
        {
            bridgeServer = new LocalBridgeServer(bridgeToken, SpeakFromBridgeAsync);
            bridgeServer.Start();
            mainWindow.SetBridgeStatus($"Bridge listening on {LocalBridgeServer.BaseUrl}");
        }
        catch (Exception ex)
        {
            mainWindow.SetBridgeStatus($"Bridge failed to start: {ex.Message}");
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
}
