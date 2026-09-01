using System.Runtime.InteropServices;
using System.Text;
using CodexUsage.Core.Window;

namespace CodexUsage.Desktop.Platform;

internal interface IPlatformPermissionStatus
{
    bool HasRequiredPermission { get; }
}

// CoreGraphics supplies a permission-light visible-window fallback. When the user grants
// Accessibility, AX supplies the authoritative frame and minimized/hidden state.
internal sealed class MacCodexWindowTracker : ICodexWindowTracker, IPlatformPermissionStatus
{
    public bool HasRequiredPermission => MacNative.IsAccessibilityTrusted();

    public bool TryGetSnapshot(out CodexWindowSnapshot? snapshot)
    {
        snapshot = null;
        if (!OperatingSystem.IsMacOS() || !MacNative.TryFindVisibleCodexWindow(out var window))
        {
            return false;
        }

        var frame = window.Bounds;
        var minimized = false;
        var hidden = false;
        if (HasRequiredPermission)
        {
            MacNative.TryReadAccessibilityWindow(window, ref frame, out minimized, out hidden);
        }

        if (hidden || minimized || frame.Width <= 0 || frame.Height <= 0)
        {
            return false;
        }

        var scale = MacNative.GetDisplayScale(frame);
        snapshot = new CodexWindowSnapshot(
            new nint(window.WindowNumber),
            MacNative.ToScreenRect(frame, scale),
            null,
            scale,
            IsVisible: true,
            IsMinimized: false,
            IsDarkMode: true);
        return true;
    }
}

internal enum MacCodexHostKind
{
    StandaloneCodex,
    ChatGptBundledCodex,
    ChatGptApplication
}

internal readonly record struct MacWindowInfo(
    int ProcessId,
    int WindowNumber,
    MacRect Bounds,
    MacCodexHostKind HostKind);

internal readonly record struct MacHostIdentity(MacCodexHostKind Kind, string Identity);

[StructLayout(LayoutKind.Sequential)]
internal struct MacPoint
{
    public double X;
    public double Y;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MacSize
{
    public double Width;
    public double Height;
}

[StructLayout(LayoutKind.Sequential)]
internal struct MacRect
{
    public double X;
    public double Y;
    public double Width;
    public double Height;
}

internal static class MacWindowDiagnostics
{
    private static readonly object Sync = new();
    private static string? _lastEntry;
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Personal),
        "Library",
        "Logs",
        "CodexUsage",
        "macos-window-discovery.log");

    // Persist only changed trust state. The log intentionally excludes window titles, chat text,
    // account data, and all other user content.
    public static void ReportAccessibilityTrust(bool trusted, string? bundlePath, string? bundleIdentifier, int processId)
    {
        var entry = $"Accessibility trust={trusted}; bundlePath={bundlePath ?? "unbundled"}; bundleIdentifier={bundleIdentifier ?? "unavailable"}; pid={processId}.";
        lock (Sync)
        {
            if (string.Equals(_lastEntry, entry, StringComparison.Ordinal))
            {
                return;
            }

            _lastEntry = entry;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
                File.AppendAllText(LogPath, $"{DateTimeOffset.Now:O} {entry}{Environment.NewLine}");
            }
            catch
            {
                // Diagnostics must never affect the companion overlay.
            }
        }
    }
}

internal static class MacNative
{
    private const uint WindowListOnScreenOnly = 1;
    private const uint WindowListExcludeDesktopElements = 16;
    private const int CfNumberSInt32 = 3;
    private const uint CfStringEncodingUtf8 = 0x08000100;
    private const int AxSuccess = 0;
    private const int AxValuePoint = 1;
    private const int AxValueSize = 2;
    private static readonly Dictionary<string, nint> Strings = new(StringComparer.Ordinal);

    public static bool IsAccessibilityTrusted()
    {
        var trusted = AXIsProcessTrusted();
        MacWindowDiagnostics.ReportAccessibilityTrust(
            trusted,
            GetCurrentBundlePath(),
            GetCurrentBundleIdentifier(),
            Environment.ProcessId);
        return trusted;
    }

    public static bool TryFindVisibleCodexWindow(out MacWindowInfo result)
    {
        result = default;
        var windows = CGWindowListCopyWindowInfo(WindowListOnScreenOnly | WindowListExcludeDesktopElements, 0);
        if (windows == nint.Zero)
        {
            return false;
        }

        try
        {
            var count = CFArrayGetCount(windows);
            var hosts = new Dictionary<int, MacHostIdentity>();
            MacWindowInfo? best = null;
            for (var index = 0; index < count; index++)
            {
                var dictionary = CFArrayGetValueAtIndex(windows, index);
                if (!TryGetInt(dictionary, "kCGWindowOwnerPID", out var processId)
                    || !TryGetInt(dictionary, "kCGWindowLayer", out var layer)
                    || !TryGetInt(dictionary, "kCGWindowNumber", out var windowNumber))
                {
                    continue;
                }

                if (!hosts.TryGetValue(processId, out var host) && !TryGetCodexHost(processId, out host))
                {
                    continue;
                }

                hosts[processId] = host;
                if (layer != 0)
                {
                    continue;
                }

                var boundsValue = CFDictionaryGetValue(dictionary, GetString("kCGWindowBounds"));
                if (boundsValue == nint.Zero || !CGRectMakeWithDictionaryRepresentation(boundsValue, out var bounds))
                {
                    continue;
                }

                // ChatGPT itself can own ordinary ChatGPT windows. Only accept that host when
                // CoreGraphics identifies this particular window as Codex-related; bundled and
                // standalone Codex processes are already process-identity-specific.
                if (host.Kind == MacCodexHostKind.ChatGptApplication
                    && !HasCodexWindowMetadata(dictionary))
                {
                    continue;
                }

                var candidate = new MacWindowInfo(processId, windowNumber, bounds, host.Kind);
                if (best is null || CandidatePriority(candidate) > CandidatePriority(best.Value))
                {
                    best = candidate;
                }
            }

            if (best is { } selected)
            {
                result = selected;
                return true;
            }
        }
        finally
        {
            CFRelease(windows);
        }

        return false;
    }

    public static bool TryReadAccessibilityWindow(MacWindowInfo target, ref MacRect frame, out bool minimized, out bool hidden)
    {
        minimized = false;
        hidden = false;
        var application = AXUIElementCreateApplication(target.ProcessId);
        if (application == nint.Zero)
        {
            return false;
        }

        try
        {
            hidden = TryReadBoolean(application, "AXHidden", out var hiddenValue) && hiddenValue;
            if (!TryCopyAttribute(application, "AXFocusedWindow", out var window))
            {
                return false;
            }

            try
            {
                if (target.HostKind == MacCodexHostKind.ChatGptApplication
                    && (!TryReadString(window, "AXTitle", out var title) || !IsCodexMetadata(title)))
                {
                    return false;
                }

                minimized = TryReadBoolean(window, "AXMinimized", out var minimizedValue) && minimizedValue;
                if (!TryCopyAttribute(window, "AXPosition", out var position)
                    || !TryCopyAttribute(window, "AXSize", out var size))
                {
                    return false;
                }

                try
                {
                    if (!AXValueGetValue(position, AxValuePoint, out MacPoint point)
                        || !AXValueGetValue(size, AxValueSize, out MacSize dimensions))
                    {
                        return false;
                    }

                    frame = new MacRect { X = point.X, Y = point.Y, Width = dimensions.Width, Height = dimensions.Height };
                    return true;
                }
                finally
                {
                    CFRelease(position);
                    CFRelease(size);
                }
            }
            finally
            {
                CFRelease(window);
            }
        }
        finally
        {
            CFRelease(application);
        }
    }

    public static double GetDisplayScale(MacRect bounds)
    {
        var displays = new uint[1];
        if (CGGetDisplaysWithRect(bounds, 1, displays, out var count) != 0 || count == 0)
        {
            displays[0] = CGMainDisplayID();
        }

        var displayBounds = CGDisplayBounds(displays[0]);
        var pixelWidth = CGDisplayPixelsWide(displays[0]);
        return displayBounds.Width > 0 ? Math.Max(1, pixelWidth / displayBounds.Width) : 1;
    }

    public static ScreenRect ToScreenRect(MacRect rect, double scale) => new(
        (int)Math.Round(rect.X * scale),
        (int)Math.Round(rect.Y * scale),
        (int)Math.Round((rect.X + rect.Width) * scale),
        (int)Math.Round((rect.Y + rect.Height) * scale));

    private static int CandidatePriority(MacWindowInfo window) =>
        window.HostKind == MacCodexHostKind.ChatGptApplication ? 1 : 2;

    private static bool TryGetCodexHost(int processId, out MacHostIdentity host)
    {
        var bundleId = GetBundleIdentifier(processId);
        if (!string.IsNullOrWhiteSpace(bundleId) && bundleId.Contains("codex", StringComparison.OrdinalIgnoreCase))
        {
            host = new MacHostIdentity(MacCodexHostKind.StandaloneCodex, "standalone-codex-bundle");
            return true;
        }

        var path = GetProcessPath(processId);
        if (path?.Contains("/Codex.app/", StringComparison.OrdinalIgnoreCase) == true)
        {
            host = new MacHostIdentity(MacCodexHostKind.StandaloneCodex, "standalone-codex-app");
            return true;
        }

        if (path?.EndsWith("/ChatGPT.app/Contents/Resources/codex", StringComparison.OrdinalIgnoreCase) == true)
        {
            host = new MacHostIdentity(MacCodexHostKind.ChatGptBundledCodex, "chatgpt-bundled-codex");
            return true;
        }

        if (path?.Contains("/ChatGPT.app/Contents/MacOS/ChatGPT", StringComparison.OrdinalIgnoreCase) == true
            || (!string.IsNullOrWhiteSpace(bundleId) && bundleId.Contains("chatgpt", StringComparison.OrdinalIgnoreCase)))
        {
            host = new MacHostIdentity(MacCodexHostKind.ChatGptApplication, "chatgpt-application");
            return true;
        }

        host = default;
        return false;
    }

    private static string? GetProcessPath(int processId)
    {
        var path = new StringBuilder(4096);
        return proc_pidpath(processId, path, (uint)path.Capacity) > 0 ? path.ToString() : null;
    }

    private static bool HasCodexWindowMetadata(nint dictionary)
    {
        var nameValue = CFDictionaryGetValue(dictionary, GetString("kCGWindowName"));
        return nameValue != nint.Zero && IsCodexMetadata(ReadString(nameValue));
    }

    private static bool IsCodexMetadata(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Contains("codex", StringComparison.OrdinalIgnoreCase);

    private static string? GetBundleIdentifier(int processId)
    {
        var runningApplication = objc_msgSendWithInt(
            objc_getClass("NSRunningApplication"),
            sel_registerName("runningApplicationWithProcessIdentifier:"),
            processId);
        if (runningApplication == nint.Zero)
        {
            return null;
        }

        var identifier = objc_msgSend(runningApplication, sel_registerName("bundleIdentifier"));
        return identifier == nint.Zero ? null : ReadString(identifier);
    }

    private static bool TryGetInt(nint dictionary, string key, out int value)
    {
        value = 0;
        var number = CFDictionaryGetValue(dictionary, GetString(key));
        return number != nint.Zero && CFNumberGetValue(number, CfNumberSInt32, out value);
    }

    private static bool TryReadBoolean(nint element, string attribute, out bool value)
    {
        value = false;
        if (!TryCopyAttribute(element, attribute, out var result))
        {
            return false;
        }

        try
        {
            value = CFBooleanGetValue(result);
            return true;
        }
        finally
        {
            CFRelease(result);
        }
    }

    private static bool TryReadString(nint element, string attribute, out string? value)
    {
        value = null;
        if (!TryCopyAttribute(element, attribute, out var result))
        {
            return false;
        }

        try
        {
            value = ReadString(result);
            return value is not null;
        }
        finally
        {
            CFRelease(result);
        }
    }

    private static bool TryCopyAttribute(nint element, string attribute, out nint value) =>
        AXUIElementCopyAttributeValue(element, GetString(attribute), out value) == AxSuccess && value != nint.Zero;

    private static nint GetString(string value)
    {
        if (!Strings.TryGetValue(value, out var result))
        {
            result = CFStringCreateWithCString(nint.Zero, value, CfStringEncodingUtf8);
            Strings.Add(value, result);
        }

        return result;
    }

    private static string? ReadString(nint value)
    {
        var capacity = Math.Max(32, checked((int)CFStringGetLength(value) * 4 + 1));
        var buffer = new StringBuilder(capacity);
        return CFStringGetCString(value, buffer, buffer.Capacity, CfStringEncodingUtf8) ? buffer.ToString() : null;
    }

    private static string? GetCurrentBundlePath()
    {
        var processPath = Environment.ProcessPath;
        const string contentsPath = "/Contents/MacOS/";
        var index = processPath?.IndexOf(contentsPath, StringComparison.OrdinalIgnoreCase) ?? -1;
        return index > 0 ? processPath![..index] : null;
    }

    private static string? GetCurrentBundleIdentifier()
    {
        var bundle = objc_msgSend(objc_getClass("NSBundle"), sel_registerName("mainBundle"));
        var identifier = bundle == nint.Zero ? nint.Zero : objc_msgSend(bundle, sel_registerName("bundleIdentifier"));
        return identifier == nint.Zero ? null : ReadString(identifier);
    }

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern nint CGWindowListCopyWindowInfo(uint option, uint relativeToWindow);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CGRectMakeWithDictionaryRepresentation(nint dictionary, out MacRect rect);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern int CGGetDisplaysWithRect(MacRect rect, uint maxDisplays, [Out] uint[] displays, out uint displayCount);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern uint CGMainDisplayID();

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern MacRect CGDisplayBounds(uint display);

    [DllImport("/System/Library/Frameworks/CoreGraphics.framework/CoreGraphics")]
    private static extern nuint CGDisplayPixelsWide(uint display);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern nint CFArrayGetValueAtIndex(nint array, nint index);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern nint CFArrayGetCount(nint array);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern nint CFDictionaryGetValue(nint dictionary, nint key);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CFNumberGetValue(nint number, int numberType, out int value);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CFBooleanGetValue(nint boolean);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern nint CFStringCreateWithCString(nint allocator, string value, uint encoding);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern nint CFStringGetLength(nint value);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool CFStringGetCString(nint value, StringBuilder buffer, int bufferSize, uint encoding);

    [DllImport("/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation")]
    private static extern void CFRelease(nint value);

    [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool AXIsProcessTrusted();

    [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static extern nint AXUIElementCreateApplication(int processId);

    [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    private static extern int AXUIElementCopyAttributeValue(nint element, nint attribute, out nint value);

    [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool AXValueGetValue(nint value, int valueType, out MacPoint result);

    [DllImport("/System/Library/Frameworks/ApplicationServices.framework/ApplicationServices")]
    [return: MarshalAs(UnmanagedType.I1)]
    private static extern bool AXValueGetValue(nint value, int valueType, out MacSize result);

    [DllImport("/usr/lib/libproc.dylib", CharSet = CharSet.Ansi)]
    private static extern int proc_pidpath(int processId, StringBuilder buffer, uint bufferSize);

    [DllImport("/usr/lib/libobjc.A.dylib", CharSet = CharSet.Ansi)]
    private static extern nint objc_getClass(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", CharSet = CharSet.Ansi)]
    private static extern nint sel_registerName(string name);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSend(nint receiver, nint selector);

    [DllImport("/usr/lib/libobjc.A.dylib", EntryPoint = "objc_msgSend")]
    private static extern nint objc_msgSendWithInt(nint receiver, nint selector, int argument);
}
