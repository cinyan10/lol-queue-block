using QueueCutoff.Core.Models;

namespace QueueCutoff.Core.Services;

public sealed class EnforcementEngine
{
    private static readonly HashSet<GameflowPhase> ActivePlayPhases =
    [
        GameflowPhase.ChampSelect,
        GameflowPhase.GameStart,
        GameflowPhase.InProgress,
        GameflowPhase.Reconnect
    ];

    public EnforcementDecision Decide(
        DateTimeOffset now,
        DailyLock? dailyLock,
        GameflowPhase phase,
        bool isGameProcessRunning)
    {
        if (dailyLock is null || dailyLock.Date != GamingDay.GetOperatingDate(now, dailyLock.DayResetTime))
        {
            return new(false, false, false, "No confirmed cutoff for today.");
        }

        var isAfterCutoff = now >= GamingDay.GetCutoffInstant(dailyLock, dailyLock.DayResetTime, now.Offset);
        if (!isAfterCutoff)
        {
            return new(false, false, false, "Cutoff has not been reached.");
        }

        var isActivePlay = ActivePlayPhases.Contains(phase);
        if (isActivePlay)
        {
            return new(false, true, true, $"Active play phase: {phase}.");
        }

        if (phase == GameflowPhase.Unknown && isGameProcessRunning)
        {
            return new(false, true, false, "LCU phase is unknown while the game process is running.");
        }

        return new(true, true, false, $"Blocking phase: {phase}.");
    }

    public static bool IsActivePlay(GameflowPhase phase) => ActivePlayPhases.Contains(phase);
}
