namespace QueueCutoff.App.Infrastructure;

public interface IAutostartService
{
    Task SetEnabledAsync(bool enabled, CancellationToken cancellationToken = default);
}
