using Avalonia;

namespace CodexUsage.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() => AppBuilder
        .Configure<App>()
        .With(new MacOSPlatformOptions
        {
            ShowInDock = false,
        })
        .UsePlatformDetect();
}
