using QueueCutoff.Core.Models;

namespace QueueCutoff.Core.Abstractions;

public interface IStateStore
{
    Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default);
    Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default);
    Task<DailyLock?> LoadDailyLockAsync(CancellationToken cancellationToken = default);
    Task SaveDailyLockAsync(DailyLock dailyLock, CancellationToken cancellationToken = default);
}
