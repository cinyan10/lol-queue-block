namespace QueueCutoff.Core.Models;

public sealed record EnforcementDecision(
    bool ShouldBlock,
    bool IsAfterCutoff,
    bool IsActivePlay,
    string Reason);
