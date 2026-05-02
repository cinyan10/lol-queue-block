namespace QueueCutoff.Core.Models;

public sealed record PlayBreakDecision(
    bool ShouldBlock,
    bool IsBreakPending,
    bool IsBreakActive,
    bool BreakStarted,
    TimeSpan RemainingBreak,
    TimeSpan AccumulatedPlayTime);
