namespace CodexUsage.Desktop;

internal static class LocalePreferences
{
    private static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Codex Usage",
        "locale.txt");

    public static string? Read()
    {
        try { return File.Exists(Path) ? File.ReadAllText(Path).Trim() : null; }
        catch { return null; }
    }

    public static void Write(string locale)
    {
        try
        {
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
            File.WriteAllText(Path, locale);
        }
        catch { }
    }
}
