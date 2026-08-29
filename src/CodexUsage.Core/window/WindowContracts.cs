namespace CodexUsage.Core.Window;

public readonly record struct ScreenRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
}

public sealed record CodexWindowSnapshot(
    nint NativeHandle,
    ScreenRect Bounds,
    ScreenRect? CaptionButtons,
    double DisplayScale,
    bool IsVisible,
    bool IsMinimized,
    bool IsDarkMode);

public interface ICodexWindowTracker
{
    bool TryGetSnapshot(out CodexWindowSnapshot? snapshot);
}
