using System.Diagnostics;
using System.IO;
using QueueCutoff.Core.Abstractions;

namespace QueueCutoff.App.Services;

public sealed class ElevatedFirewallBlockBackend(string helperPath) : IBlockBackend
{
    public async Task EnableAsync(IReadOnlyCollection<string> executablePaths, CancellationToken cancellationToken = default)
    {
        if (executablePaths.Count == 0)
        {
            return;
        }

        await RunHelperAsync("enable", executablePaths, cancellationToken);
    }

    public Task DisableAsync(CancellationToken cancellationToken = default)
    {
        return RunHelperAsync("disable", [], cancellationToken);
    }

    public async Task<bool> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(helperPath))
        {
            return false;
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = helperPath,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        startInfo.ArgumentList.Add("status");

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return false;
        }

        var output = await process.StandardOutput.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);
        return output.Contains("enabled", StringComparison.OrdinalIgnoreCase);
    }

    private async Task RunHelperAsync(string command, IReadOnlyCollection<string> paths, CancellationToken cancellationToken)
    {
        if (!File.Exists(helperPath))
        {
            throw new FileNotFoundException("QueueCutoff elevated helper was not found.", helperPath);
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = helperPath,
            UseShellExecute = true,
            Verb = "runas"
        };
        startInfo.ArgumentList.Add(command);
        foreach (var path in paths)
        {
            startInfo.ArgumentList.Add(path);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the elevated helper.");
        await process.WaitForExitAsync(cancellationToken);

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Elevated helper exited with code {process.ExitCode}.");
        }
    }
}
