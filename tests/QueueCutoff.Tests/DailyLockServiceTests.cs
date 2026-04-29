using QueueCutoff.Core.Abstractions;
using QueueCutoff.Core.Models;
using QueueCutoff.Core.Services;
using Xunit;

namespace QueueCutoff.Tests;

public sealed class DailyLockServiceTests
{
    [Fact]
    public async Task ConfirmToday_CreatesLockOnce()
    {
        var store = new MemoryStateStore();
        var clock = new FakeClock(Local(2026, 4, 29, 20, 0));
        var service = new DailyLockService(store, clock);

        var first = await service.ConfirmTodayAsync(new TimeOnly(22, 10));
        var second = await service.ConfirmTodayAsync(new TimeOnly(22, 10));

        Assert.Equal(first, second);
        Assert.Equal(new TimeOnly(22, 10), second.LockedCutoff);
    }

    [Fact]
    public async Task ConfirmToday_CannotIncreaseCutoff()
    {
        var service = new DailyLockService(
            new MemoryStateStore(),
            new FakeClock(Local(2026, 4, 29, 20, 0)));

        await service.ConfirmTodayAsync(new TimeOnly(22, 10));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ConfirmTodayAsync(new TimeOnly(22, 30)));
    }

    [Fact]
    public async Task ConfirmToday_CanShortenCutoff()
    {
        var service = new DailyLockService(
            new MemoryStateStore(),
            new FakeClock(Local(2026, 4, 29, 20, 0)));

        await service.ConfirmTodayAsync(new TimeOnly(22, 10));
        var shortened = await service.ConfirmTodayAsync(new TimeOnly(21, 45));

        Assert.Equal(new TimeOnly(21, 45), shortened.LockedCutoff);
    }

    [Fact]
    public async Task ConfirmToday_AllowsEarlyMorningCutoffInSameGamingDay()
    {
        var store = new MemoryStateStore();
        await store.SaveSettingsAsync(new AppSettings
        {
            DefaultCutoff = new TimeOnly(2, 0),
            DayResetTime = new TimeOnly(8, 0)
        });
        var clock = new FakeClock(Local(2026, 4, 29, 21, 0));
        var service = new DailyLockService(store, clock);

        var dailyLock = await service.ConfirmTodayAsync(new TimeOnly(2, 0));

        Assert.Equal(new DateOnly(2026, 4, 29), dailyLock.Date);
        Assert.Equal(new TimeOnly(8, 0), dailyLock.DayResetTime);
    }

    [Fact]
    public async Task GetTodayLock_UsesResetTimeToKeepEarlyMorningInPreviousGamingDay()
    {
        var store = new MemoryStateStore();
        await store.SaveSettingsAsync(new AppSettings { DayResetTime = new TimeOnly(8, 0) });
        var clock = new FakeClock(Local(2026, 4, 29, 21, 0));
        var service = new DailyLockService(store, clock);

        await service.ConfirmTodayAsync(new TimeOnly(2, 0));

        clock.Now = Local(2026, 4, 30, 1, 59);
        Assert.NotNull(await service.GetTodayLockAsync());

        clock.Now = Local(2026, 4, 30, 8, 0);
        Assert.Null(await service.GetTodayLockAsync());
    }

    private sealed class FakeClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset Now { get; set; } = now;
        public DateOnly Today => DateOnly.FromDateTime(Now.LocalDateTime);
    }

    private static DateTimeOffset Local(int year, int month, int day, int hour, int minute)
    {
        var local = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Local);
        return new DateTimeOffset(local);
    }

    private sealed class MemoryStateStore : IStateStore
    {
        private AppSettings _settings = new();
        private DailyLock? _dailyLock;

        public Task<AppSettings> LoadSettingsAsync(CancellationToken cancellationToken = default) => Task.FromResult(_settings);
        public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            _settings = settings;
            return Task.CompletedTask;
        }

        public Task<DailyLock?> LoadDailyLockAsync(CancellationToken cancellationToken = default) => Task.FromResult(_dailyLock);
        public Task SaveDailyLockAsync(DailyLock dailyLock, CancellationToken cancellationToken = default)
        {
            _dailyLock = dailyLock;
            return Task.CompletedTask;
        }
    }
}
