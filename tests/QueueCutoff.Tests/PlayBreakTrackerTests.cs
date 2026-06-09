using QueueCutoff.Core.Services;
using Xunit;

namespace QueueCutoff.Tests;

public sealed class PlayBreakTrackerTests
{
    [Fact]
    public void Decide_DoesNotCountClientOnlyTime()
    {
        var tracker = new PlayBreakTracker(playLimit: TimeSpan.FromHours(1));
        var start = Local(2026, 4, 29, 20, 0);

        tracker.Decide(start, isGameProcessRunning: false);
        var decision = tracker.Decide(start.AddHours(2), isGameProcessRunning: false);

        Assert.False(decision.ShouldBlock);
        Assert.False(decision.IsBreakPending);
        Assert.Equal(TimeSpan.Zero, decision.AccumulatedPlayTime);
    }

    [Fact]
    public void Decide_AccumulatesAcrossShortInactiveGap()
    {
        var tracker = new PlayBreakTracker(playLimit: TimeSpan.FromHours(1));
        var start = Local(2026, 4, 29, 20, 0);

        tracker.Decide(start, isGameProcessRunning: true);
        tracker.Decide(start.AddMinutes(30), isGameProcessRunning: true);
        tracker.Decide(start.AddMinutes(32), isGameProcessRunning: false);
        tracker.Decide(start.AddMinutes(34), isGameProcessRunning: true);
        var decision = tracker.Decide(start.AddMinutes(64), isGameProcessRunning: true);

        Assert.False(decision.ShouldBlock);
        Assert.True(decision.IsBreakPending);
        Assert.Equal(TimeSpan.FromMinutes(62), decision.AccumulatedPlayTime);
    }

    [Fact]
    public void Decide_DoesNotResetPlayTimeBeforeInactivityLimit()
    {
        var tracker = new PlayBreakTracker(playLimit: TimeSpan.FromHours(1));
        var start = Local(2026, 4, 29, 20, 0);

        tracker.Decide(start, isGameProcessRunning: true);
        tracker.Decide(start.AddMinutes(30), isGameProcessRunning: true);
        tracker.Decide(start.AddMinutes(30), isGameProcessRunning: false);
        var decision = tracker.Decide(start.AddMinutes(34).AddSeconds(59), isGameProcessRunning: false);

        Assert.False(decision.ShouldBlock);
        Assert.False(decision.IsBreakPending);
        Assert.Equal(TimeSpan.FromMinutes(30), decision.AccumulatedPlayTime);
    }

    [Fact]
    public void Decide_ResetsPlayTimeAfterFiveMinutesInactive()
    {
        var tracker = new PlayBreakTracker(playLimit: TimeSpan.FromHours(1));
        var start = Local(2026, 4, 29, 20, 0);

        tracker.Decide(start, isGameProcessRunning: true);
        tracker.Decide(start.AddMinutes(30), isGameProcessRunning: true);
        tracker.Decide(start.AddMinutes(30), isGameProcessRunning: false);
        var decision = tracker.Decide(start.AddMinutes(35), isGameProcessRunning: false);

        Assert.False(decision.ShouldBlock);
        Assert.False(decision.IsBreakPending);
        Assert.Equal(TimeSpan.Zero, decision.AccumulatedPlayTime);
    }

    [Fact]
    public void Decide_AccumulatesFromZeroAfterInactiveReset()
    {
        var tracker = new PlayBreakTracker(playLimit: TimeSpan.FromHours(1));
        var start = Local(2026, 4, 29, 20, 0);

        tracker.Decide(start, isGameProcessRunning: true);
        tracker.Decide(start.AddMinutes(30), isGameProcessRunning: true);
        tracker.Decide(start.AddMinutes(30), isGameProcessRunning: false);
        tracker.Decide(start.AddMinutes(35), isGameProcessRunning: false);
        tracker.Decide(start.AddMinutes(36), isGameProcessRunning: true);
        var decision = tracker.Decide(start.AddMinutes(46), isGameProcessRunning: true);

        Assert.False(decision.ShouldBlock);
        Assert.False(decision.IsBreakPending);
        Assert.Equal(TimeSpan.FromMinutes(10), decision.AccumulatedPlayTime);
    }

    [Fact]
    public void Decide_DoesNotBlockWhenLimitIsReachedDuringActiveGame()
    {
        var tracker = new PlayBreakTracker(playLimit: TimeSpan.FromHours(1));
        var start = Local(2026, 4, 29, 20, 0);

        tracker.Decide(start, isGameProcessRunning: true);
        var decision = tracker.Decide(start.AddHours(1), isGameProcessRunning: true);

        Assert.False(decision.ShouldBlock);
        Assert.True(decision.IsBreakPending);
        Assert.False(decision.IsBreakActive);
    }

    [Fact]
    public void Decide_StartsBreakWhenGameEndsAfterLimit()
    {
        var tracker = new PlayBreakTracker(
            playLimit: TimeSpan.FromHours(1),
            breakDuration: TimeSpan.FromMinutes(3));
        var start = Local(2026, 4, 29, 20, 0);

        tracker.Decide(start, isGameProcessRunning: true);
        tracker.Decide(start.AddHours(1), isGameProcessRunning: true);
        var decision = tracker.Decide(start.AddHours(1).AddSeconds(5), isGameProcessRunning: false);

        Assert.True(decision.ShouldBlock);
        Assert.True(decision.IsBreakPending);
        Assert.True(decision.IsBreakActive);
        Assert.True(decision.BreakStarted);
        Assert.Equal(TimeSpan.FromMinutes(3), decision.RemainingBreak);
    }

    [Fact]
    public void Decide_KeepsBreakActiveForDurationThenResetsPlayTime()
    {
        var tracker = new PlayBreakTracker(
            playLimit: TimeSpan.FromHours(1),
            breakDuration: TimeSpan.FromMinutes(3));
        var start = Local(2026, 4, 29, 20, 0);
        var breakStart = start.AddHours(1).AddSeconds(5);

        tracker.Decide(start, isGameProcessRunning: true);
        tracker.Decide(start.AddHours(1), isGameProcessRunning: true);
        tracker.Decide(breakStart, isGameProcessRunning: false);

        var duringBreak = tracker.Decide(breakStart.AddMinutes(2), isGameProcessRunning: false);
        var afterBreak = tracker.Decide(breakStart.AddMinutes(3), isGameProcessRunning: false);

        Assert.True(duringBreak.ShouldBlock);
        Assert.True(duringBreak.IsBreakActive);
        Assert.Equal(TimeSpan.FromMinutes(1), duringBreak.RemainingBreak);
        Assert.False(afterBreak.ShouldBlock);
        Assert.False(afterBreak.IsBreakPending);
        Assert.False(afterBreak.IsBreakActive);
        Assert.Equal(TimeSpan.Zero, afterBreak.AccumulatedPlayTime);
    }

    [Fact]
    public void Decide_CanTriggerSecondBreakAfterReset()
    {
        var tracker = new PlayBreakTracker(
            playLimit: TimeSpan.FromMinutes(1),
            breakDuration: TimeSpan.FromMinutes(3));
        var start = Local(2026, 4, 29, 20, 0);
        var firstBreakStart = start.AddMinutes(1).AddSeconds(1);
        var secondStart = firstBreakStart.AddMinutes(3);

        tracker.Decide(start, isGameProcessRunning: true);
        tracker.Decide(start.AddMinutes(1), isGameProcessRunning: true);
        tracker.Decide(firstBreakStart, isGameProcessRunning: false);
        tracker.Decide(secondStart, isGameProcessRunning: false);

        tracker.Decide(secondStart.AddSeconds(1), isGameProcessRunning: true);
        tracker.Decide(secondStart.AddMinutes(1).AddSeconds(1), isGameProcessRunning: true);
        var secondBreak = tracker.Decide(secondStart.AddMinutes(1).AddSeconds(2), isGameProcessRunning: false);

        Assert.True(secondBreak.ShouldBlock);
        Assert.True(secondBreak.BreakStarted);
        Assert.True(secondBreak.IsBreakActive);
    }

    private static DateTimeOffset Local(int year, int month, int day, int hour, int minute)
    {
        var local = new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Local);
        return new DateTimeOffset(local);
    }
}
