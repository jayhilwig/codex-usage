using System.Runtime.InteropServices;

namespace CodexUsage.Desktop.Platform;

internal static class WindowsOverlayInterop
{
    private const int GwlpHwndParent = -8;
    private const int GwlExStyle = -20;
    private const long WsExToolWindow = 0x00000080L;
    private const long WsExNoActivate = 0x08000000L;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpShowWindow = 0x0040;

    public static void ConfigureHud(nint hudWindow, nint codexWindow)
    {
        if (!OperatingSystem.IsWindows() || hudWindow == nint.Zero)
        {
            return;
        }

        SetWindowLongPtr(hudWindow, GwlpHwndParent, codexWindow);
        var styles = GetWindowLongPtr(hudWindow, GwlExStyle).ToInt64();
        SetWindowLongPtr(hudWindow, GwlExStyle, new nint(styles | WsExToolWindow | WsExNoActivate));
    }

    public static void Position(nint window, int x, int y, int width, int height)
    {
        if (!OperatingSystem.IsWindows() || window == nint.Zero)
        {
            return;
        }

        SetWindowPos(window, nint.Zero, x, y, width, height, SwpNoActivate | SwpNoZOrder | SwpShowWindow);
    }

    private static nint SetWindowLongPtr(nint window, int index, nint newValue)
        => nint.Size == 8
            ? SetWindowLongPtr64(window, index, newValue)
            : new nint(SetWindowLong32(window, index, newValue.ToInt32()));

    private static nint GetWindowLongPtr(nint window, int index)
        => nint.Size == 8
            ? GetWindowLongPtr64(window, index)
            : new nint(GetWindowLong32(window, index));

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr64(nint window, int index, nint newValue);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(nint window, int index, int newValue);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern nint GetWindowLongPtr64(nint window, int index);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW")]
    private static extern int GetWindowLong32(nint window, int index);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        nint window,
        nint insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);
}
