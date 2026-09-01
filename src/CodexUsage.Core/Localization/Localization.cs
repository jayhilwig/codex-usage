using System.Globalization;
using System.Resources;

namespace CodexUsage.Core.Localization;

public static class Localization
{
    private static readonly ResourceManager Resources = new("CodexUsage.Core.Localization.Strings", typeof(Localization).Assembly);
    private static readonly IReadOnlyDictionary<string, CultureInfo> Cultures = new Dictionary<string, CultureInfo>(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = CultureInfo.GetCultureInfo("en-US"),
        ["de"] = CultureInfo.GetCultureInfo("de-DE"),
        ["ja"] = CultureInfo.GetCultureInfo("ja-JP"),
        ["fr"] = CultureInfo.GetCultureInfo("fr-FR"),
        ["es"] = CultureInfo.GetCultureInfo("es-ES"),
    };

    public static event EventHandler? Changed;
    public static string Locale { get; private set; } = "en";
    public static CultureInfo Culture => Cultures[Locale];
    public static IReadOnlyList<string> SupportedLocales { get; } = ["en", "de", "ja", "fr", "es"];

    public static string ResolveLocale(CultureInfo culture) =>
        Cultures.ContainsKey(culture.TwoLetterISOLanguageName) ? culture.TwoLetterISOLanguageName : "en";

    public static void SetLocale(string? locale)
    {
        var next = locale is not null && Cultures.ContainsKey(locale) ? locale.ToLowerInvariant() : "en";
        if (string.Equals(Locale, next, StringComparison.Ordinal)) return;
        Locale = next;
        Changed?.Invoke(null, EventArgs.Empty);
    }

    public static string Get(string key, params object[] args)
    {
        var value = Resources.GetString(key, Culture) ?? Resources.GetString(key, Cultures["en"]) ?? key;
        return args.Length == 0 ? value : string.Format(Culture, value, args);
    }
}
