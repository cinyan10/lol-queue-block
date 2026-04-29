namespace QueueCutoff.Core.Abstractions;

public interface IClock
{
    DateTimeOffset Now { get; }
    DateOnly Today { get; }
}
