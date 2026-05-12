using System;
using System.IO;
using Microsoft.Win32;

namespace CodeCompanionDesktop.Settings;

public sealed class WindowsStartupRegistration
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "CodeCompanionDesktop";
    private const string DisplayRunKeyPath = @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Run";

    public StartupRegistrationDiagnostics GetDiagnostics()
    {
        var registeredCommand = ReadRegisteredCommand();
        var executablePath = ExtractExecutablePath(registeredCommand);
        var currentExecutablePath = Environment.ProcessPath;
        var targetExists = executablePath is not null && File.Exists(executablePath);
        var matchesCurrentExecutable = executablePath is not null
            && currentExecutablePath is not null
            && string.Equals(
                Path.GetFullPath(executablePath),
                Path.GetFullPath(currentExecutablePath),
                StringComparison.OrdinalIgnoreCase);

        return new StartupRegistrationDiagnostics(
            DisplayRunKeyPath,
            ValueName,
            registeredCommand,
            executablePath,
            currentExecutablePath,
            targetExists,
            matchesCurrentExecutable);
    }

    public bool IsRegistered()
    {
        return !string.IsNullOrWhiteSpace(ReadRegisteredCommand());
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

    private static string? ReadRegisteredCommand()
    {
        using var runKey = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return runKey?.GetValue(ValueName) as string;
    }

    private static string? ExtractExecutablePath(string? command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        var trimmedCommand = command.Trim();
        if (trimmedCommand.StartsWith('"'))
        {
            var closingQuoteIndex = trimmedCommand.IndexOf('"', startIndex: 1);
            return closingQuoteIndex > 1
                ? trimmedCommand[1..closingQuoteIndex]
                : null;
        }

        var firstSpaceIndex = trimmedCommand.IndexOf(' ');
        return firstSpaceIndex > 0
            ? trimmedCommand[..firstSpaceIndex]
            : trimmedCommand;
    }
}

public sealed record StartupRegistrationDiagnostics(
    string RegistryPath,
    string ValueName,
    string? RegisteredCommand,
    string? RegisteredExecutablePath,
    string? CurrentExecutablePath,
    bool RegisteredTargetExists,
    bool RegisteredExecutableMatchesCurrent);
