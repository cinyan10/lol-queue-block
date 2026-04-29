using QueueCutoff.Core.Abstractions;
using QueueCutoff.Core.Models;

namespace QueueCutoff.Core.Services;

public sealed class DailyLockService(IStateStore stateStore, IClock clock)
{
    public async Task<DailyLock?> GetTodayLockAsync(CancellationToken cancellationToken = default)
    {
        var dailyLock = await stateStore.LoadDailyLockAsync(cancellationToken);
        var settings = await stateStore.LoadSettingsAsync(cancellationToken);
        var resetTime = dailyLock?.DayResetTime ?? settings.DayResetTime;
        var operatingDate = GamingDay.GetOperatingDate(clock.Now, resetTime);
        return dailyLock is not null && dailyLock.IsFor(operatingDate) ? dailyLock : null;
    }

    public async Task<DailyLock> ConfirmTodayAsync(TimeOnly cutoff, CancellationToken cancellationToken = default)
    {
        var existing = await GetTodayLockAsync(cancellationToken);
        var settings = await stateStore.LoadSettingsAsync(cancellationToken);
        if (existing is not null)
        {
            if (GamingDay.CompareCutoffOrder(existing.Date, cutoff, existing.LockedCutoff, existing.DayResetTime) > 0)
            {
                throw new InvalidOperationException("Today's cutoff is already locked and cannot be increased.");
            }

            if (cutoff == existing.LockedCutoff)
            {
                return existing;
            }

            return await ShortenTodayAsync(cutoff, cancellationToken);
        }

        var dailyLock = new DailyLock
        {
            Date = GamingDay.GetOperatingDate(clock.Now, settings.DayResetTime),
            LockedCutoff = cutoff,
            DayResetTime = settings.DayResetTime,
            ConfirmedAt = clock.Now,
            EnforcementActivated = false
        };

        await stateStore.SaveDailyLockAsync(dailyLock, cancellationToken);
        return dailyLock;
    }

    public async Task<DailyLock> ShortenTodayAsync(TimeOnly cutoff, CancellationToken cancellationToken = default)
    {
        var existing = await GetTodayLockAsync(cancellationToken)
            ?? throw new InvalidOperationException("No daily cutoff has been confirmed yet.");

        if (GamingDay.CompareCutoffOrder(existing.Date, cutoff, existing.LockedCutoff, existing.DayResetTime) > 0)
        {
            throw new InvalidOperationException("Today's cutoff can only be shortened.");
        }

        var updated = existing with { LockedCutoff = cutoff };
        await stateStore.SaveDailyLockAsync(updated, cancellationToken);
        return updated;
    }

    public async Task MarkEnforcementActivatedAsync(CancellationToken cancellationToken = default)
    {
        var existing = await GetTodayLockAsync(cancellationToken);
        if (existing is null || existing.EnforcementActivated)
        {
            return;
        }

        await stateStore.SaveDailyLockAsync(existing with { EnforcementActivated = true }, cancellationToken);
    }
}
