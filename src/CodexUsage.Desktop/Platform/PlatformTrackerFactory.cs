using CodexUsage.Core.Window;

namespace CodexUsage.Desktop.Platform;

internal static class PlatformTrackerFactory
{
    public static ICodexWindowTracker Create()
    {
        if (OperatingSystem.IsWindows())
        {
            return new WindowsCodexWindowTracker();
        }

        if (OperatingSystem.IsMacOS())
        {
            return new MacCodexWindowTracker();
        }

        return new UnsupportedCodexWindowTracker();
    }
}

// The macOS host has an explicit adapter slot so the app-server client, reset client,
// resolver, state, view-model, and Avalonia UI remain unchanged. V1 implements Windows;
// the macOS adapter will use CGWindowListCopyWindowInfo + Accessibility geometry.
internal sealed class MacCodexWindowTracker : ICodexWindowTracker
{
    public bool TryGetSnapshot(out CodexWindowSnapshot? snapshot)
    {
        snapshot = null;
        return false;
    }
}

internal sealed class UnsupportedCodexWindowTracker : ICodexWindowTracker
{
    public bool TryGetSnapshot(out CodexWindowSnapshot? snapshot)
    {
        snapshot = null;
        return false;
    }
}
