using CodexUsage.Core.Reset;
using CodexUsage.Core.Usage;

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
            return "reset unavailable";
        }

        var until = reset - now;
        if (until <= TimeSpan.Zero)
        {
            return "reset due";
        }

        if (until <= TimeSpan.FromHours(24))
        {
            return $"resets in {FormatDuration(until)}";
        }

        return $"resets {reset.ToLocalTime():MMM d}";
    }

    public static string FormatAge(DateTimeOffset timestamp, DateTimeOffset now)
    {
        var age = now - timestamp;
        return age <= TimeSpan.Zero ? "just now" : $"{FormatDuration(age)} ago";
    }

    public static string FormatLongAge(DateTimeOffset timestamp, DateTimeOffset now)
    {
        var age = now - timestamp;
        if (age <= TimeSpan.Zero)
        {
            return "just now";
        }

        if (age.TotalMinutes < 60)
        {
            var minutes = Math.Max(1, (int)Math.Round(age.TotalMinutes));
            return $"{minutes} minute{(minutes == 1 ? string.Empty : "s")} ago";
        }

        if (age.TotalHours < 24)
        {
            var hours = Math.Max(1, (int)Math.Floor(age.TotalHours));
            return $"{hours} hour{(hours == 1 ? string.Empty : "s")} ago";
        }

        var days = Math.Max(1, (int)Math.Floor(age.TotalDays));
        return $"{days} day{(days == 1 ? string.Empty : "s")} ago";
    }

    public static string SummarizeAnnouncement(string text)
    {
        var normalized = string.Join(
            " ",
            text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (normalized.Length == 0)
        {
            return "Reset announced for Codex users.";
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
            return "<1m";
        }

        if (duration.TotalHours < 1)
        {
            return $"{Math.Max(1, (int)Math.Round(duration.TotalMinutes))}m";
        }

        if (duration.TotalDays < 1)
        {
            var hours = (int)duration.TotalHours;
            var minutes = duration.Minutes;
            return minutes == 0 ? $"{hours}h" : $"{hours}h {minutes}m";
        }

        return $"{(int)duration.TotalDays}d";
    }
}
