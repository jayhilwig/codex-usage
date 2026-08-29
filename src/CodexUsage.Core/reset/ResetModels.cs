using System.Text.Json.Serialization;
using CodexUsage.Core.Usage;

namespace CodexUsage.Core.Reset;

public sealed record ResetAnnouncement(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("reset_type")] string ResetType,
    [property: JsonPropertyName("announced_at")] DateTimeOffset AnnouncedAt,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("source")] ResetSource Source);

public sealed record ResetSource(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("author")] string? Author,
    [property: JsonPropertyName("url")] string? Url);

public sealed record ResetWatch(
    [property: JsonPropertyName("level")] string Level,
    [property: JsonPropertyName("reset_chance_percent")] int? ResetChancePercent,
    [property: JsonPropertyName("forecast_window")] string ForecastWindow,
    [property: JsonPropertyName("observed_at")] DateTimeOffset ObservedAt,
    [property: JsonPropertyName("expires_at")] DateTimeOffset ExpiresAt,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("source")] ResetSource Source);

public sealed record ResetStatusData(
    [property: JsonPropertyName("latest_reset")] ResetAnnouncement? LatestReset,
    [property: JsonPropertyName("active_watch")] ResetWatch? ActiveWatch);

public sealed record ResetStatusMeta(
    [property: JsonPropertyName("api_version")] string ApiVersion,
    [property: JsonPropertyName("generated_at")] DateTimeOffset GeneratedAt);

public sealed record PublicResetStatus(
    [property: JsonPropertyName("data")] ResetStatusData Data,
    [property: JsonPropertyName("meta")] ResetStatusMeta Meta);

public sealed record PublicResetFetch(
    bool IsFresh,
    PublicResetStatus? Status,
    DateTimeOffset? LastSuccessfulAt);

public enum ResetVisualState
{
    Neutral,
    Announced,
    Confirmed,
    Unknown,
}

public sealed record ResetResolution(
    ResetVisualState State,
    ResetAnnouncement? Announcement,
    ResetWatch? Watch,
    DateTimeOffset? ConfirmedAt,
    string? ConfirmedEventId,
    UsageSnapshot? ConfirmedUsage);
