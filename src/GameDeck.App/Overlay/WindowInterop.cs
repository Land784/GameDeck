using System.Runtime.InteropServices;

namespace GameDeck.App.Overlay;

/// <summary>
/// Thin Win32 helpers for the overlay window. Deliberately shallow: these
/// calls are imperative by nature and wrapping them deeper would only hide
/// the docs you need when debugging window styles.
/// </summary>
internal static class WindowInterop
{
    private const int GwlExstyle = -20;
    private const long WsExTransparent = 0x20;
    private const long WsExToolWindow = 0x80;
    private const long WsExLayered = 0x80000;
    private const long WsExNoActivate = 0x8000000;

    private static readonly IntPtr HwndTopmost = new(-1);
    private const uint SwpNoSize = 0x1;
    private const uint SwpNoMove = 0x2;
    private const uint SwpNoActivate = 0x10;

    /// <summary>
    /// Tool-window (no Alt-Tab entry), never-activate (never steals focus
    /// from the game), layered, and click-through by default.
    /// </summary>
    public static void ApplyOverlayStyles(IntPtr hwnd)
    {
        var ex = GetWindowLongPtr(hwnd, GwlExstyle).ToInt64();
        ex |= WsExToolWindow | WsExNoActivate | WsExLayered | WsExTransparent;
        SetWindowLongPtr(hwnd, GwlExstyle, new IntPtr(ex));
    }

    public static void SetClickThrough(IntPtr hwnd, bool clickThrough)
    {
        var ex = GetWindowLongPtr(hwnd, GwlExstyle).ToInt64();
        ex = clickThrough ? ex | WsExTransparent : ex & ~WsExTransparent;
        SetWindowLongPtr(hwnd, GwlExstyle, new IntPtr(ex));
    }

    public static void AssertTopmost(IntPtr hwnd) =>
        SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);
}
