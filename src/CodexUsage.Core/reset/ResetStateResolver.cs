using CodexUsage.Core.Usage;

namespace CodexUsage.Core.Reset;

public sealed class ResetStateResolver
{
    public static readonly TimeSpan RecentAnnouncementWindow = TimeSpan.FromHours(8);
    public static readonly TimeSpan ConfirmedDisplayWindow = TimeSpan.FromHours(6);

    public ResetResolution Resolve(
        PublicResetFetch publicReset,
        UsageSnapshot? previousUsage,
        UsageSnapshot? currentUsage,
        PersistedCompanionState persisted,
        DateTimeOffset now)
    {
        if (!publicReset.IsFresh || publicReset.Status is null)
        {
            return new ResetResolution(
                ResetVisualState.Unknown,
                publicReset.Status?.Data.LatestReset,
                publicReset.Status?.Data.ActiveWatch,
                persisted.ConfirmedAt,
                persisted.ConfirmedEventId,
                persisted.ConfirmedUsage);
        }

        var announcement = publicReset.Status.Data.LatestReset;
        var watch = publicReset.Status.Data.ActiveWatch;

        if (announcement is not null
            && persisted.ConfirmedEventId == announcement.Id
            && persisted.ConfirmedAt is { } confirmedAt
            && now - confirmedAt <= ConfirmedDisplayWindow)
        {
            return new ResetResolution(
                ResetVisualState.Confirmed,
                announcement,
                watch,
                confirmedAt,
                announcement.Id,
                persisted.ConfirmedUsage);
        }

        var recentAnnouncement = announcement is not null
            && now >= announcement.AnnouncedAt
            && now - announcement.AnnouncedAt <= RecentAnnouncementWindow;

        if (recentAnnouncement
            && announcement!.ResetType == "regular"
            && IsConservativeExternalRefresh(announcement, previousUsage, currentUsage))
        {
            return new ResetResolution(
                ResetVisualState.Confirmed,
                announcement,
                watch,
                now,
                announcement.Id,
                currentUsage);
        }

        if (recentAnnouncement)
        {
            return new ResetResolution(
                ResetVisualState.Announced,
                announcement,
                watch,
                persisted.ConfirmedAt,
                persisted.ConfirmedEventId,
                persisted.ConfirmedUsage);
        }

        if (watch is not null && watch.ExpiresAt > now
            && (watch.Level == "strong" || watch.Level == "elevated"))
        {
            return new ResetResolution(
                ResetVisualState.Announced,
                announcement,
                watch,
                persisted.ConfirmedAt,
                persisted.ConfirmedEventId,
                persisted.ConfirmedUsage);
        }

        return new ResetResolution(
            ResetVisualState.Neutral,
            announcement,
            watch,
            persisted.ConfirmedAt,
            persisted.ConfirmedEventId,
            persisted.ConfirmedUsage);
    }

    private static bool IsConservativeExternalRefresh(
        ResetAnnouncement announcement,
        UsageSnapshot? previous,
        UsageSnapshot? current)
    {
        if (previous?.FiveHour is null || previous.Weekly is null
            || current?.FiveHour is null || current.Weekly is null)
        {
            return false;
        }

        if (announcement.AnnouncedAt < previous.ObservedAt - TimeSpan.FromMinutes(30)
            || announcement.AnnouncedAt > current.ObservedAt + TimeSpan.FromMinutes(15))
        {
            return false;
        }

        if (WasNaturalReset(previous.FiveHour, previous.ObservedAt, current.FiveHour, current.ObservedAt)
            || WasNaturalReset(previous.Weekly, previous.ObservedAt, current.Weekly, current.ObservedAt))
        {
            return false;
        }

        var fiveHourIncrease = current.FiveHour.RemainingPercent - previous.FiveHour.RemainingPercent;
        var weeklyIncrease = current.Weekly.RemainingPercent - previous.Weekly.RemainingPercent;
        var materiallyRefreshed = fiveHourIncrease >= 20 && weeklyIncrease >= 20;
        var nearFull = current.FiveHour.RemainingPercent >= 90 || current.Weekly.RemainingPercent >= 90;
        return materiallyRefreshed && nearFull;
    }

    private static bool WasNaturalReset(
        UsageWindow previous,
        DateTimeOffset previousObservedAt,
        UsageWindow current,
        DateTimeOffset currentObservedAt)
    {
        if (previous.ResetsAt is not { } previousReset)
        {
            return false;
        }

        var fellBetweenSnapshots = previousReset >= previousObservedAt - TimeSpan.FromMinutes(10)
            && previousReset <= currentObservedAt + TimeSpan.FromMinutes(5);
        var resetAdvanced = current.ResetsAt is { } currentReset
            && currentReset - previousReset > TimeSpan.FromHours(1);
        return fellBetweenSnapshots && resetAdvanced;
    }
}
