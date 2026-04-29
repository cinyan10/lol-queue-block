using QueueCutoff.Core.Abstractions;

namespace QueueCutoff.Core.Services;

public sealed class SystemClock : IClock
{
    public DateTimeOffset Now => DateTimeOffset.Now;
    public DateOnly Today => DateOnly.FromDateTime(Now.LocalDateTime);
}
