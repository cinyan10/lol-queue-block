namespace QueueCutoff.Core.Abstractions;

public interface IProcessMonitor : IAsyncDisposable
{
    event Action<LeagueProcessSnapshot>? LeagueProcessesChanged;

    Task<LeagueProcessSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
    Task StartAsync(CancellationToken cancellationToken = default);
}
