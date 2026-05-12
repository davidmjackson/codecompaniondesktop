using System;
using Microsoft.Win32;

namespace CodeCompanionDesktop.Settings;

public sealed class WindowsStartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CodeCompanionDesktop";

    public bool IsRegistered()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return !string.IsNullOrWhiteSpace(runKey?.GetValue(ValueName) as string);
    }

    public void Register()
    {
        var executablePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            throw new InvalidOperationException("Unable to determine the app executable path.");
        }

        using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        runKey.SetValue(ValueName, QuotePath(executablePath), RegistryValueKind.String);
    }

    public void Unregister()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        runKey?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string QuotePath(string path)
    {
        return $"\"{path}\"";
    }
}
