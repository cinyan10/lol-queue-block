namespace QueueCutoff.Core.Models;

public sealed record DailyLock
{
    public DateOnly Date { get; init; }
    public TimeOnly LockedCutoff { get; init; }
    public TimeOnly DayResetTime { get; init; } = new(8, 0);
    public DateTimeOffset ConfirmedAt { get; init; }
    public bool EnforcementActivated { get; init; }

    public bool IsFor(DateOnly date) => Date == date;
}
