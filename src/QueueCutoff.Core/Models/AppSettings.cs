namespace QueueCutoff.Core.Models;

public sealed record AppSettings
{
    public TimeOnly DefaultCutoff { get; init; } = new(22, 10);
    public TimeOnly DayResetTime { get; init; } = new(8, 0);
    public bool AutostartEnabled { get; init; } = true;
    public string? LeagueInstallPath { get; init; }
    public bool PreventExitWhileLeagueRunning { get; init; } = true;
    public bool PreventExitWhileEnforcing { get; init; } = true;
}
