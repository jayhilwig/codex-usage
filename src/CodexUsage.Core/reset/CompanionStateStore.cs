using System.Text.Json;
using CodexUsage.Core.Usage;

namespace CodexUsage.Core.Reset;

public sealed record PersistedCompanionState(
    IReadOnlyList<UsageSnapshot>? UsageHistory = null,
    PublicResetStatus? LastPublicReset = null,
    DateTimeOffset? LastPublicResetSuccessAt = null,
    string? ConfirmedEventId = null,
    DateTimeOffset? ConfirmedAt = null,
    UsageSnapshot? ConfirmedUsage = null);

public sealed class CompanionStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _path;

    public CompanionStateStore(string? path = null)
    {
        _path = path ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "CodexUsageHud",
            "state.json");
    }

    public async Task<PersistedCompanionState> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_path))
            {
                return new PersistedCompanionState();
            }

            await using var stream = File.OpenRead(_path);
            return await JsonSerializer.DeserializeAsync<PersistedCompanionState>(
                stream,
                JsonOptions,
                cancellationToken).ConfigureAwait(false)
                ?? new PersistedCompanionState();
        }
        catch
        {
            return new PersistedCompanionState();
        }
    }

    public async Task WriteAsync(PersistedCompanionState state, CancellationToken cancellationToken)
    {
        var directory = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = _path + ".tmp";
        await using (var stream = File.Create(temporaryPath))
        {
            await JsonSerializer.SerializeAsync(stream, state, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
        }

        File.Move(temporaryPath, _path, overwrite: true);
    }
}
