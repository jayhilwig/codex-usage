namespace CodexUsage.Core.Usage;

public sealed record UsageWindow(
    int RemainingPercent,
    int UsedPercent,
    DateTimeOffset? ResetsAt,
    long? WindowDurationMinutes);

public sealed record CreditBalance(decimal Balance);

public sealed record UsageSnapshot(
    UsageWindow? FiveHour,
    UsageWindow? Weekly,
    DateTimeOffset ObservedAt,
    CreditBalance? Credits = null)
{
    public static readonly UsageSnapshot Unavailable = new(null, null, DateTimeOffset.MinValue);
}

public interface ICodexRateLimitsSource : IAsyncDisposable
{
    event EventHandler<UsageSnapshot>? SnapshotChanged;
    event EventHandler<bool>? AvailabilityChanged;

    UsageSnapshot? Current { get; }
    Task StartAsync(CancellationToken cancellationToken);
    Task<UsageSnapshot?> RefreshAsync(CancellationToken cancellationToken);
}
