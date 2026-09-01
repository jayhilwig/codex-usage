using CodexUsage.Core.Reset;
using CodexUsage.Core.Usage;
using L = CodexUsage.Core.Localization.Localization;

namespace CodexUsage.Core.Ui;

public sealed class HudViewModel
{
    public UsageSnapshot? Usage { get; private set; }
    public ResetAnnouncement? LatestReset { get; private set; }
    public bool LatestResetIsFresh { get; private set; }
    public ResetResolution Reset { get; private set; } = new(
        ResetVisualState.Unknown, null, null, null, null, null);

    public event EventHandler? Changed;

    public string UsageText
    {
        get
        {
            var fiveHour = Usage?.FiveHour is { } five ? $"{five.RemainingPercent}%" : "--";
            var weekly = Usage?.Weekly is { } week ? $"{week.RemainingPercent}%" : "--";
            return $"5h {fiveHour} · W {weekly}";
        }
    }

    public void SetUsage(UsageSnapshot? usage)
    {
        Usage = usage;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetReset(ResetResolution reset)
    {
        Reset = reset;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void SetLatestReset(ResetAnnouncement? latestReset, bool isFresh)
    {
        LatestReset = latestReset;
        LatestResetIsFresh = isFresh;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public static string FormatUsageReset(UsageWindow? window, DateTimeOffset now)
    {
        if (window?.ResetsAt is not { } reset)
        {
            return L.Get("WaitingForAccount");
        }

        var until = reset - now;
        if (until <= TimeSpan.Zero)
        {
            return L.Get("ResetDue");
        }

        if (until <= TimeSpan.FromHours(24))
        {
            return L.Get("ResetsIn", FormatDuration(until));
        }

        return L.Get("ResetsDate", FormatDate(reset));
    }

    public static string FormatAge(DateTimeOffset timestamp, DateTimeOffset now)
    {
        var age = now - timestamp;
        return age <= TimeSpan.Zero ? L.Get("JustNow") : FormatLongDuration(age);
    }

    public static string FormatLongAge(DateTimeOffset timestamp, DateTimeOffset now)
    {
        var age = now - timestamp;
        if (age <= TimeSpan.Zero)
        {
            return L.Get("JustNow");
        }

        if (age.TotalMinutes < 60)
        {
            return L.Get("AnnouncedAgo", FormatLongDuration(age));
        }

        if (age.TotalHours < 24)
        {
            return L.Get("AnnouncedAgo", FormatLongDuration(age));
        }

        return L.Get("AnnouncedAgo", FormatLongDuration(age));
    }

    public static string SummarizeAnnouncement(string text)
    {
        var normalized = string.Join(
            " ",
            text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length == 0)
        {
            return L.Get("ResetAnnounced");
        }

        var sentenceEnd = normalized.IndexOfAny(['.', '!', '?']);
        return sentenceEnd is >= 0 and < 100
            ? normalized[..(sentenceEnd + 1)]
            : normalized;
    }

    private static string FormatDuration(TimeSpan duration)
    {
        if (duration.TotalMinutes < 1)
        {
            return L.Get("LessThanMinute");
        }

        if (duration.TotalHours < 1)
        {
            return L.Get("Minutes", Math.Max(1, (int)Math.Round(duration.TotalMinutes)));
        }

        if (duration.TotalDays < 1)
        {
            var hours = (int)duration.TotalHours;
            var minutes = duration.Minutes;
            return minutes == 0
                ? L.Get("Hours", hours)
                : L.Get("HoursMinutes", hours, minutes);
        }

        return L.Get("Days", (int)duration.TotalDays);
    }

    private static string FormatLongDuration(TimeSpan age)
    {
        if (age.TotalMinutes < 60)
        {
            return L.Get("LongMinutes", Math.Max(1, (int)Math.Round(age.TotalMinutes)));
        }

        if (age.TotalHours < 24)
        {
            return L.Get("LongHours", Math.Max(1, (int)Math.Floor(age.TotalHours)));
        }

        return L.Get("LongDays", Math.Max(1, (int)Math.Floor(age.TotalDays)));
    }

    private static string FormatDate(DateTimeOffset reset)
    {
        var local = reset.ToLocalTime();
        var pattern = L.Locale switch
        {
            "de" => "d. MMM",
            "ja" => "M月d日",
            "fr" or "es" => "d MMM",
            _ => "MMM d",
        };
        return local.ToString(pattern, L.Culture);
    }
}
