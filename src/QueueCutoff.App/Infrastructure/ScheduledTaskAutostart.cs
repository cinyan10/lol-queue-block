using System.Diagnostics;

namespace QueueCutoff.App.Infrastructure;

public sealed class ScheduledTaskAutostart : IAutostartService
{
    private const string TaskName = "QueueCutoff";

    public Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        var exePath = Environment.ProcessPath ?? throw new InvalidOperationException("Cannot locate the running executable.");
        var arguments = enabled
            ? $"/Create /TN \"{TaskName}\" /SC ONLOGON /RL LIMITED /F /TR \"\\\"{exePath}\\\" --minimized\""
            : $"/Delete /TN \"{TaskName}\" /F";

        return RunSchtasksAsync(arguments, cancellationToken);
    }

    private static async Task RunSchtasksAsync(string arguments, CancellationToken cancellationToken)
    {
        using var process = Process.Start(new ProcessStartInfo
        {
            FileName = "schtasks.exe",
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true
        }) ?? throw new InvalidOperationException("Failed to start schtasks.exe.");

        await process.WaitForExitAsync(cancellationToken);
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"schtasks.exe exited with code {process.ExitCode}.");
        }
    }
}
