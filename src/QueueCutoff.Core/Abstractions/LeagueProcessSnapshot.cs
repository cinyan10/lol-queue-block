namespace QueueCutoff.Core.Abstractions;

public sealed record LeagueProcessSnapshot(
    bool IsClientRunning,
    bool IsGameRunning,
    IReadOnlyCollection<string> ExecutablePaths);
