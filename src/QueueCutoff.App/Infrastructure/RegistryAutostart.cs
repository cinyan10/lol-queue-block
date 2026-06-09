using Microsoft.Win32;

namespace QueueCutoff.App.Infrastructure;

public sealed class RegistryAutostart : IAutostartService
{
    private const string AppName = "QueueCutoff";
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    public Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var runKey = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (runKey is null)
        {
            throw new InvalidOperationException("Cannot open the current user's Windows startup registry key.");
        }

        if (enabled)
        {
            var exePath = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate the running executable.");
            runKey.SetValue(AppName, $"\"{exePath}\" --minimized");
        }
        else
        {
            runKey.DeleteValue(AppName, throwOnMissingValue: false);
        }

        return Task.CompletedTask;
    }
}
