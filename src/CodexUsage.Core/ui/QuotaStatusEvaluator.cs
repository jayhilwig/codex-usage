using CodexUsage.Core.Usage;

namespace CodexUsage.Core.Ui;

public enum QuotaStatusLevel
{
    Normal,
    Amber,
    Red,
}

public static class QuotaStatusEvaluator
{
    public static QuotaStatusLevel EvaluateFiveHour(UsageWindow? window)
    {
        if (window is null)
        {
            return QuotaStatusLevel.Normal;
        }

        var remaining = Math.Clamp(window.RemainingPercent, 0, 100);
        return remaining <= 10
            ? QuotaStatusLevel.Red
            : remaining <= 20
                ? QuotaStatusLevel.Amber
                : QuotaStatusLevel.Normal;
    }

    public static QuotaStatusLevel EvaluateWeekly(UsageWindow? window, DateTimeOffset now)
    {
        if (window?.ResetsAt is not { } resetsAt
            || window.WindowDurationMinutes is not { } durationMinutes
            || durationMinutes <= 0)
        {
            return QuotaStatusLevel.Normal;
        }

        var remainingTimeMinutes = (resetsAt - now).TotalMinutes;
        if (!double.IsFinite(remainingTimeMinutes)
            || remainingTimeMinutes <= 0
            || remainingTimeMinutes > durationMinutes + 1)
        {
            return QuotaStatusLevel.Normal;
        }

        var remainingQuotaPercent = Math.Clamp(window.RemainingPercent, 0, 100);
        var remainingWindowTimePercent = Math.Clamp(
            remainingTimeMinutes / durationMinutes * 100d,
            0d,
            100d);
        if (remainingWindowTimePercent <= 0)
        {
            return QuotaStatusLevel.Normal;
        }

        var runway = remainingQuotaPercent / remainingWindowTimePercent;
        return runway >= 0.8
            ? QuotaStatusLevel.Normal
            : runway >= 0.5
                ? QuotaStatusLevel.Amber
                : QuotaStatusLevel.Red;
    }
}
