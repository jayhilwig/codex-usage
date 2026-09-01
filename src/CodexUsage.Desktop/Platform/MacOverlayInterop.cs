using System.Runtime.InteropServices;

namespace CodexUsage.Desktop.Platform;

internal static class MacOverlayInterop
{
    private const string ObjectiveCLibrary = "/usr/lib/libobjc.A.dylib";
    private static readonly nuint CanJoinAllSpaces = (nuint)1 << 0;
    private static readonly nuint Transient = (nuint)1 << 3;
    private static readonly nuint FullScreenAuxiliary = (nuint)1 << 8;

    public static void ConfigureHud(nint window)
    {
        if (!OperatingSystem.IsMacOS() || window == nint.Zero)
        {
            return;
        }

        var collectionBehavior = sel_registerName("collectionBehavior");
        var setCollectionBehavior = sel_registerName("setCollectionBehavior:");
        var setHidesOnDeactivate = sel_registerName("setHidesOnDeactivate:");
        var setExcludedFromWindowsMenu = sel_registerName("setExcludedFromWindowsMenu:");
        var existingBehavior = SendUnsignedInteger(window, collectionBehavior);
        SendUnsignedIntegerArgument(
            window,
            setCollectionBehavior,
            existingBehavior | CanJoinAllSpaces | Transient | FullScreenAuxiliary);
        SendBoolArgument(window, setHidesOnDeactivate, 0);
        SendBoolArgument(window, setExcludedFromWindowsMenu, 1);
    }

    [DllImport(ObjectiveCLibrary)]
    private static extern nint sel_registerName([MarshalAs(UnmanagedType.LPUTF8Str)] string name);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern nuint SendUnsignedInteger(nint receiver, nint selector);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void SendUnsignedIntegerArgument(nint receiver, nint selector, nuint value);

    [DllImport(ObjectiveCLibrary, EntryPoint = "objc_msgSend")]
    private static extern void SendBoolArgument(nint receiver, nint selector, byte value);
}
