using QueueCutoff.Core.Models;
using QueueCutoff.Core.Services;
using Xunit;

namespace QueueCutoff.Tests;

public sealed class EnforcementEngineTests
{
    private readonly EnforcementEngine _engine = new();
    private readonly DailyLock _lock = new()
    {
        Date = new DateOnly(2026, 4, 29),
        LockedCutoff = new TimeOnly(22, 10),
        ConfirmedAt = Local(2026, 4, 29, 20, 0)
    };

    [Fact]
    public void Decide_DoesNotBlockBeforeCutoff()
    {
        var decision = _engine.Decide(
            Local(2026, 4, 29, 22, 9),
            _lock,
            GameflowPhase.Lobby,
            isGameProcessRunning: false);

        Assert.False(decision.ShouldBlock);
    }

    [Theory]
    [InlineData(GameflowPhase.ChampSelect)]
    [InlineData(GameflowPhase.GameStart)]
    [InlineData(GameflowPhase.InProgress)]
    [InlineData(GameflowPhase.Reconnect)]
    public void Decide_DoesNotBlockActivePlay(GameflowPhase phase)
    {
        var decision = _engine.Decide(
            Local(2026, 4, 29, 22, 11),
            _lock,
            phase,
            isGameProcessRunning: true);

        Assert.False(decision.ShouldBlock);
        Assert.True(decision.IsActivePlay);
    }

    [Theory]
    [InlineData(GameflowPhase.Lobby)]
    [InlineData(GameflowPhase.Matchmaking)]
    [InlineData(GameflowPhase.ReadyCheck)]
    [InlineData(GameflowPhase.EndOfGame)]
    [InlineData(GameflowPhase.None)]
    public void Decide_BlocksQueuePhasesAfterCutoff(GameflowPhase phase)
    {
        var decision = _engine.Decide(
            Local(2026, 4, 29, 22, 11),
            _lock,
            phase,
            isGameProcessRunning: false);

        Assert.True(decision.ShouldBlock);
    }

    [Fact]
    public void Decide_UnknownPhaseBlocksOnlyWhenGameProcessIsNotRunning()
    {
        var now = Local(2026, 4, 29, 22, 11);

        Assert.False(_engine.Decide(now, _lock, GameflowPhase.Unknown, isGameProcessRunning: true).ShouldBlock);
        Assert.True(_engine.Decide(now, _lock, GameflowPhase.Unknown, isGameProcessRunning: false).ShouldBlock);
    }

    [Fact]
    public void Decide_TreatsEarlyMorningCutoffAsNextCalendarDayBeforeReset()
    {
        var lateLock = _lock with
        {
            LockedCutoff = new TimeOnly(2, 0),
            DayResetTime = new TimeOnly(8, 0)
        };

        Assert.False(_engine.Decide(
            Local(2026, 4, 30, 1, 59),
            lateLock,
            GameflowPhase.Lobby,
            isGameProcessRunning: false).ShouldBlock);

        Assert.True(_engine.Decide(
            Local(2026, 4, 30, 2, 0),
            lateLock,
            GameflowPhase.Lobby,
            isGameProcessRunning: false).ShouldBlock);
    }

    [Fact]
    public void Decide_ResetsGamingDayAtConfiguredResetTime()
    {
        var lateLock = _lock with
        {
            LockedCutoff = new TimeOnly(2, 0),
            DayResetTime = new TimeOnly(8, 0)
        };

        var afterReset = _engine.Decide(
            Local(2026, 4, 30, 8, 0),
            lateLock,
            GameflowPhase.Lobby,
            isGameProcessRunning: false);

        Assert.False(afterReset.ShouldBlock);
    }

    private static DateTimeOffset Local(int year, int month, int day, int hour, int minute)
    {
        var local = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Local);
        return new DateTimeOffset(local);
    }
}
