using QueueCutoff.Core.Models;

namespace QueueCutoff.Core.Services;

public sealed class PlayBreakTracker(
    TimeSpan? playLimit = null,
    TimeSpan? breakDuration = null,
    TimeSpan? inactivityReset = null)
{
    public static readonly TimeSpan DefaultPlayLimit = TimeSpan.FromHours(1);
    public static readonly TimeSpan DefaultBreakDuration = TimeSpan.FromMinutes(3);
    public static readonly TimeSpan DefaultInactivityReset = TimeSpan.FromMinutes(5);

    private readonly TimeSpan _playLimit = playLimit ?? DefaultPlayLimit;
    private readonly TimeSpan _breakDuration = breakDuration ?? DefaultBreakDuration;
    private readonly TimeSpan _inactivityReset = inactivityReset ?? DefaultInactivityReset;
    private DateTimeOffset? _lastObservedAt;
    private DateTimeOffset? _inactiveSince;
    private TimeSpan _accumulatedPlayTime;
    private bool _isBreakPending;
    private bool _wasGameProcessRunning;
    private DateTimeOffset? _breakStartedAt;

    public PlayBreakDecision Decide(DateTimeOffset now, bool isGameProcessRunning)
    {
        var delta = GetElapsedSinceLastObservation(now);

        if (_breakStartedAt is not null)
        {
            var elapsedBreak = now - _breakStartedAt.Value;
            if (elapsedBreak >= _breakDuration)
            {
                ResetAfterBreak();
                _wasGameProcessRunning = isGameProcessRunning;
                return CreateDecision(shouldBlock: false, breakStarted: false);
            }

            _wasGameProcessRunning = isGameProcessRunning;
            _inactiveSince = isGameProcessRunning ? null : _inactiveSince ?? now;
            return CreateDecision(
                shouldBlock: !isGameProcessRunning,
                breakStarted: false,
                remainingBreak: _breakDuration - elapsedBreak);
        }

        if (_wasGameProcessRunning)
        {
            _accumulatedPlayTime += delta;
            if (_accumulatedPlayTime >= _playLimit)
            {
                _isBreakPending = true;
            }
        }

        if (isGameProcessRunning)
        {
            _inactiveSince = null;
        }
        else
        {
            _inactiveSince ??= now;
            if (now - _inactiveSince.Value >= _inactivityReset)
            {
                ResetInactivePlayTime();
            }
        }

        if (_isBreakPending && !isGameProcessRunning)
        {
            _breakStartedAt = now;
            _wasGameProcessRunning = isGameProcessRunning;
            return CreateDecision(shouldBlock: true, breakStarted: true, remainingBreak: _breakDuration);
        }

        _wasGameProcessRunning = isGameProcessRunning;
        return CreateDecision(shouldBlock: false, breakStarted: false);
    }

    private TimeSpan GetElapsedSinceLastObservation(DateTimeOffset now)
    {
        if (_lastObservedAt is null)
        {
            _lastObservedAt = now;
            return TimeSpan.Zero;
        }

        var delta = now - _lastObservedAt.Value;
        _lastObservedAt = now;
        return delta > TimeSpan.Zero ? delta : TimeSpan.Zero;
    }

    private void ResetAfterBreak()
    {
        _accumulatedPlayTime = TimeSpan.Zero;
        _isBreakPending = false;
        _breakStartedAt = null;
    }

    private void ResetInactivePlayTime()
    {
        _accumulatedPlayTime = TimeSpan.Zero;
        _isBreakPending = false;
    }

    private PlayBreakDecision CreateDecision(
        bool shouldBlock,
        bool breakStarted,
        TimeSpan? remainingBreak = null)
    {
        var isBreakActive = _breakStartedAt is not null;
        return new PlayBreakDecision(
            shouldBlock,
            _isBreakPending,
            isBreakActive,
            breakStarted,
            remainingBreak ?? TimeSpan.Zero,
            _accumulatedPlayTime);
    }
}
