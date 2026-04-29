namespace QueueCutoff.Core.Abstractions;

public interface IBlockBackend
{
    Task EnableAsync(IReadOnlyCollection<string> executablePaths, CancellationToken cancellationToken = default);
    Task DisableAsync(CancellationToken cancellationToken = default);
    Task<bool> GetStatusAsync(CancellationToken cancellationToken = default);
}
