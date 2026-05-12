using System;
using System.Drawing;
using System.Windows;
using Forms = System.Windows.Forms;
using WpfApplication = System.Windows.Application;

namespace CodeCompanionDesktop;

public partial class App : WpfApplication
{
    private Forms.NotifyIcon? trayIcon;
    private MainWindow? mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        mainWindow = new MainWindow();
        ConfigureTrayIcon();
        mainWindow.Show();
    }

    protected override void OnExit(ExitEventArgs e)
    {
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
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());

        return menu;
    }
}
