using System.Runtime.InteropServices;
using System.Windows.Threading;
using GameDeck.Core.Hotkeys;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameDeck.App.Hotkeys;

/// <summary>
/// WH_KEYBOARD_LL fallback for games whose raw-input exclusive fullscreen
/// swallows RegisterHotKey (issue #3). This is an OS input hook; it never
/// touches the game process. The callback only feeds the shared matcher
/// and enqueues the dispatch. A matched combo's key (and its auto-repeats
/// and key-up) is consumed, mirroring what RegisterHotKey does, so the
/// foreground app does not also act on it; all other input passes through
/// untouched. Create on the UI thread (the hook needs its message loop,
/// and callbacks arrive on it).
/// </summary>
public sealed class KeyboardHookHost : IDisposable
{
    private const int WhKeyboardLl = 13;
    private const int WmKeydown = 0x0100;
    private const int WmKeyup = 0x0101;
    private const int WmSyskeydown = 0x0104;
    private const int WmSyskeyup = 0x0105;

    private readonly HotkeyFallbackMatcher _matcher;
    private readonly Action<HotkeyAction> _dispatch;
    private readonly Dispatcher _dispatcher;
    private readonly ILogger _logger;
    private readonly NativeMethods.LowLevelKeyboardProc _proc; // kept alive for the hook's lifetime
    private readonly HashSet<uint> _consumed = new(); // hook-thread only
    private IntPtr _hook;

    public KeyboardHookHost(HotkeyFallbackMatcher matcher, Action<HotkeyAction> dispatch, ILogger? logger = null)
    {
        _matcher = matcher;
        _dispatch = dispatch;
        _dispatcher = Dispatcher.CurrentDispatcher;
        _logger = logger ?? NullLogger.Instance;
        _proc = Callback;
        Install();
    }

    /// <summary>Windows can silently drop LL hooks (e.g. across unlock).</summary>
    public void Reinstall()
    {
        Uninstall();
        Install();
    }

    private void Install()
    {
        _hook = NativeMethods.SetWindowsHookEx(
            WhKeyboardLl, _proc, NativeMethods.GetModuleHandle(null), 0);
        if (_hook == IntPtr.Zero)
        {
            _logger.LogWarning(
                "Keyboard hook install failed (error {Error}); fullscreen hotkey fallback inactive",
                Marshal.GetLastWin32Error());
        }
    }

    private void Uninstall()
    {
        if (_hook == IntPtr.Zero) return;
        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
    }

    private IntPtr Callback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            // KBDLLHOOKSTRUCT.vkCode is the first DWORD.
            var vk = (uint)Marshal.ReadInt32(lParam);
            switch (wParam.ToInt32())
            {
                case WmKeydown or WmSyskeydown:
                    if (_matcher.OnKeyDown(vk, CurrentModifiers()) is { } action)
                    {
                        _consumed.Add(vk);
                        _dispatcher.BeginInvoke(() => _dispatch(action));
                        return new IntPtr(1);
                    }
                    if (_consumed.Contains(vk))
                        return new IntPtr(1); // auto-repeat of a consumed key
                    break;
                case WmKeyup or WmSyskeyup:
                    _matcher.OnKeyUp(vk);
                    if (_consumed.Remove(vk))
                        return new IntPtr(1);
                    break;
            }
        }
        return NativeMethods.CallNextHookEx(_hook, nCode, wParam, lParam);
    }

    // GetAsyncKeyState (physical state), not GetKeyState: the LL callback
    // runs before the input reaches any thread queue, so queue-relative
    // state would be stale for the modifiers.
    private static HotkeyModifiers CurrentModifiers()
    {
        var m = HotkeyModifiers.None;
        if (Down(0x11)) m |= HotkeyModifiers.Control;
        if (Down(0x12)) m |= HotkeyModifiers.Alt;
        if (Down(0x10)) m |= HotkeyModifiers.Shift;
        if (Down(0x5B) || Down(0x5C)) m |= HotkeyModifiers.Win;
        return m;
    }

    private static bool Down(int vk) => (NativeMethods.GetAsyncKeyState(vk) & 0x8000) != 0;

    public void Dispose() => Uninstall();

    private static class NativeMethods
    {
        public delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        public static extern IntPtr SetWindowsHookEx(
            int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll")]
        public static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern IntPtr GetModuleHandle(string? lpModuleName);

        [DllImport("user32.dll")]
        public static extern short GetAsyncKeyState(int vKey);
    }
}
