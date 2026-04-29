using QueueCutoff.Core.Models;

namespace QueueCutoff.Core.Services;

public static class GamingDay
{
    public static DateOnly GetOperatingDate(DateTimeOffset now, TimeOnly resetTime)
    {
        var localDate = DateOnly.FromDateTime(now.LocalDateTime);
        var localTime = TimeOnly.FromDateTime(now.LocalDateTime);
        return localTime < resetTime ? localDate.AddDays(-1) : localDate;
    }

    public static DateTimeOffset GetCutoffInstant(DailyLock dailyLock, TimeOnly resetTime, TimeSpan offset)
    {
        var cutoffDate = dailyLock.LockedCutoff < resetTime
            ? dailyLock.Date.AddDays(1)
            : dailyLock.Date;

        return new DateTimeOffset(cutoffDate.ToDateTime(dailyLock.LockedCutoff), offset);
    }

    public static int CompareCutoffOrder(DateOnly operatingDate, TimeOnly left, TimeOnly right, TimeOnly resetTime)
    {
        var leftDate = left < resetTime ? operatingDate.AddDays(1) : operatingDate;
        var rightDate = right < resetTime ? operatingDate.AddDays(1) : operatingDate;
        return leftDate.ToDateTime(left).CompareTo(rightDate.ToDateTime(right));
    }
}
