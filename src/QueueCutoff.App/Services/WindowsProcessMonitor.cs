using System.Diagnostics;
using System.Management;
using QueueCutoff.Core.Abstractions;

namespace QueueCutoff.App.Services;

public sealed class WindowsProcessMonitor : IProcessMonitor
{
    private readonly string[] _processNames =
    [
        "LeagueClient",
        "LeagueClientUx",
        "LeagueClientUxRender",
        "League of Legends",
        "RiotClientServices"
    ];

    private ManagementEventWatcher? _startWatcher;
    private ManagementEventWatcher? _stopWatcher;

    public event Action<LeagueProcessSnapshot>? LeagueProcessesChanged;

    public Task<LeagueProcessSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default)
    {
        var paths = new List<string>();
        var isClientRunning = false;
        var isGameRunning = false;

        foreach (var name in _processNames)
        {
            foreach (var process in Process.GetProcessesByName(name))
            {
                using (process)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var fileName = TryGetProcessPath(process);
                    if (fileName is not null)
                    {
                        paths.Add(fileName);
                    }

                    isClientRunning |= name.StartsWith("LeagueClient", StringComparison.OrdinalIgnoreCase);
                    isGameRunning |= name.Equals("League of Legends", StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        var snapshot = new LeagueProcessSnapshot(
            isClientRunning,
            isGameRunning,
            paths.Distinct(StringComparer.OrdinalIgnoreCase).ToArray());
        return Task.FromResult(snapshot);
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _startWatcher = CreateWatcher("Win32_ProcessStartTrace");
            _stopWatcher = CreateWatcher("Win32_ProcessStopTrace");
            _startWatcher.Start();
            _stopWatcher.Start();
        }
        catch
        {
            _startWatcher?.Dispose();
            _stopWatcher?.Dispose();
            _startWatcher = null;
            _stopWatcher = null;
        }

        return Task.CompletedTask;
    }

    private ManagementEventWatcher CreateWatcher(string eventClass)
    {
        var names = string.Join(" OR ", _processNames.Select(name => $"ProcessName = '{name}.exe'"));
        var watcher = new ManagementEventWatcher(new WqlEventQuery($"SELECT * FROM {eventClass} WHERE {names}"));
        watcher.EventArrived += async (_, _) =>
        {
            try
            {
                LeagueProcessesChanged?.Invoke(await GetSnapshotAsync());
            }
            catch
            {
            }
        };
        return watcher;
    }

    private static string? TryGetProcessPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            _startWatcher?.Stop();
            _stopWatcher?.Stop();
        }
        catch
        {
        }

        _startWatcher?.Dispose();
        _stopWatcher?.Dispose();
        return ValueTask.CompletedTask;
    }
}
