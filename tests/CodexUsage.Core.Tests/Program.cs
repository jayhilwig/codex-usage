using CodexUsage.Core.Reset;
using CodexUsage.Core.Ui;
using CodexUsage.Core.Usage;
using System.Reflection;
using System.Text.Json;

var now = new DateTimeOffset(2026, 8, 28, 20, 0, 0, TimeSpan.Zero);
var resolver = new ResetStateResolver();
var emptyState = new PersistedCompanionState();

var announcement = new ResetAnnouncement(
    "event-1",
    "regular",
    now - TimeSpan.FromMinutes(10),
    "Public reset announcement",
    new ResetSource("x_post", "thsottiaux", "https://example.test/event-1"));
var publicStatus = new PublicResetStatus(
    new ResetStatusData(announcement, null),
    new ResetStatusMeta("v1", now));

AssertEqual(
    ResetVisualState.Unknown,
    resolver.Resolve(new PublicResetFetch(false, publicStatus, now), null, null, emptyState, now).State,
    "offline public source is unknown");

AssertEqual(
    ResetVisualState.Announced,
    resolver.Resolve(new PublicResetFetch(true, publicStatus, now), null, null, emptyState, now).State,
    "announcement without local evidence remains amber");

var previous = Snapshot(30, 25, now - TimeSpan.FromMinutes(15), now + TimeSpan.FromHours(4), now + TimeSpan.FromDays(4));
var refreshed = Snapshot(98, 96, now - TimeSpan.FromMinutes(1), now + TimeSpan.FromHours(5), now + TimeSpan.FromDays(7));
AssertEqual(
    ResetVisualState.Confirmed,
    resolver.Resolve(new PublicResetFetch(true, publicStatus, now), previous, refreshed, emptyState, now).State,
    "large simultaneous non-natural refresh confirms green");

var naturallyDuePrevious = Snapshot(
    30,
    25,
    now - TimeSpan.FromMinutes(15),
    now - TimeSpan.FromMinutes(2),
    now - TimeSpan.FromMinutes(2));
AssertEqual(
    ResetVisualState.Announced,
    resolver.Resolve(
        new PublicResetFetch(true, publicStatus, now),
        naturallyDuePrevious,
        refreshed,
        emptyState,
        now).State,
    "natural reset timing blocks green attribution");

var oldStatus = publicStatus with
{
    Data = publicStatus.Data with { LatestReset = announcement with { AnnouncedAt = now - TimeSpan.FromHours(10) } },
};
AssertEqual(
    ResetVisualState.Neutral,
    resolver.Resolve(new PublicResetFetch(true, oldStatus, now), null, null, emptyState, now).State,
    "old announcement returns to neutral");

var watch = new ResetWatch(
    "strong",
    80,
    "next two hours",
    now - TimeSpan.FromMinutes(5),
    now + TimeSpan.FromHours(1),
    "Strong watch",
    new ResetSource("x_post", "thsottiaux", "https://example.test/watch"));
var watchStatus = new PublicResetStatus(
    new ResetStatusData(null, watch),
    new ResetStatusMeta("v1", now));
AssertEqual(
    ResetVisualState.Announced,
    resolver.Resolve(new PublicResetFetch(true, watchStatus, now), null, null, emptyState, now).State,
    "active strong watch is amber");

AssertEqual(
    QuotaStatusLevel.Normal,
    QuotaStatusEvaluator.EvaluateFiveHour(Window(21, now + TimeSpan.FromHours(5), 300)),
    "five-hour above 20 percent is normal");
AssertEqual(
    QuotaStatusLevel.Amber,
    QuotaStatusEvaluator.EvaluateFiveHour(Window(20, now + TimeSpan.FromHours(5), 300)),
    "five-hour 10 to 20 percent is amber");
AssertEqual(
    QuotaStatusLevel.Red,
    QuotaStatusEvaluator.EvaluateFiveHour(Window(10, now + TimeSpan.FromHours(5), 300)),
    "five-hour 10 percent or less is red");
AssertEqual(
    QuotaStatusLevel.Normal,
    QuotaStatusEvaluator.EvaluateWeekly(Window(80, now + TimeSpan.FromDays(7), 10_080), now),
    "weekly runway of 0.8 is normal");
AssertEqual(
    QuotaStatusLevel.Amber,
    QuotaStatusEvaluator.EvaluateWeekly(Window(60, now + TimeSpan.FromDays(7), 10_080), now),
    "weekly runway between 0.5 and 0.8 is amber");
AssertEqual(
    QuotaStatusLevel.Red,
    QuotaStatusEvaluator.EvaluateWeekly(Window(49, now + TimeSpan.FromDays(7), 10_080), now),
    "weekly runway below 0.5 is red");
AssertEqual(
    QuotaStatusLevel.Normal,
    QuotaStatusEvaluator.EvaluateWeekly(Window(40, now + TimeSpan.FromDays(3.5), 10_080), now),
    "weekly quota tracks remaining time proportionally");
AssertEqual(
    QuotaStatusLevel.Normal,
    QuotaStatusEvaluator.EvaluateWeekly(Window(10, now - TimeSpan.FromMinutes(1), 10_080), now),
    "invalid weekly reset falls back to normal");
AssertEqual(
    QuotaStatusLevel.Normal,
    QuotaStatusEvaluator.EvaluateWeekly(new UsageWindow(10, 90, null, 10_080), now),
    "missing weekly reset falls back to normal");
AssertEqual(
    QuotaStatusLevel.Normal,
    QuotaStatusEvaluator.EvaluateWeekly(Window(10, now + TimeSpan.FromDays(8), 10_080), now),
    "weekly reset beyond its window falls back to normal");

AssertEqual(
    7.84m,
    ParseUsageSnapshot("""
        {
          "rateLimits": { "primary": { "usedPercent": 20 }, "secondary": { "usedPercent": 40 } },
          "rateLimitsByLimitId": {
            "codex": {
              "primary": { "usedPercent": 10, "windowDurationMins": 300 },
              "secondary": { "usedPercent": 20, "windowDurationMins": 10080 },
              "credits": { "hasCredits": false, "unlimited": false, "balance": "7.84" }
            }
          }
        }
        """).Credits?.Balance ?? -1m,
    "codex credit balance string takes priority over hasCredits");

AssertEqual(
    3.5m,
    ParseUsageSnapshot("""
        {
          "rateLimits": {
            "primary": { "usedPercent": 10, "windowDurationMins": 300 },
            "secondary": { "usedPercent": 20, "windowDurationMins": 10080 },
            "credits": { "unlimited": false, "balance": 3.5 }
          }
        }
        """).Credits?.Balance ?? -1m,
    "rate limits credits is the fallback");

AssertEqual(
    true,
    ParseUsageSnapshot("""
        {
          "rateLimits": {
            "primary": { "usedPercent": 10, "windowDurationMins": 300 },
            "secondary": { "usedPercent": 20, "windowDurationMins": 10080 },
            "credits": { "unlimited": true, "balance": "7.84" }
          }
        }
        """).Credits is null,
    "unlimited accounts do not show a purchased credit balance");

Console.WriteLine("19 core status tests passed.");

static UsageSnapshot Snapshot(
    int fiveHourRemaining,
    int weeklyRemaining,
    DateTimeOffset observedAt,
    DateTimeOffset fiveHourReset,
    DateTimeOffset weeklyReset) => new(
    new UsageWindow(fiveHourRemaining, 100 - fiveHourRemaining, fiveHourReset, 300),
    new UsageWindow(weeklyRemaining, 100 - weeklyRemaining, weeklyReset, 10_080),
    observedAt);

static UsageWindow Window(int remaining, DateTimeOffset resetsAt, long durationMinutes) =>
    new(remaining, 100 - remaining, resetsAt, durationMinutes);

static UsageSnapshot ParseUsageSnapshot(string json)
{
    using var document = JsonDocument.Parse(json);
    var parseSnapshot = typeof(CodexRateLimitsClient).GetMethod(
        "ParseSnapshot",
        BindingFlags.Static | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Usage parser was not found.");
    return (UsageSnapshot)(parseSnapshot.Invoke(null, [document.RootElement])
        ?? throw new InvalidOperationException("Usage parser returned null."));
}

static void AssertEqual<T>(T expected, T actual, string name) where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{name}: expected {expected}, got {actual}");
    }
}
