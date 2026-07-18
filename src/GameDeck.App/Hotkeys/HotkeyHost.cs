using System.Runtime.InteropServices;
using System.Windows.Interop;
using GameDeck.Core.Hotkeys;

namespace GameDeck.App.Hotkeys;

/// <summary>
/// Global hotkeys via RegisterHotKey on a message-only window. Must be
/// created on the UI thread (the thread that owns the message loop).
/// WM_HOTKEY handling returns immediately; media commands are fire-and-forget.
/// </summary>
public sealed class HotkeyHost : IDisposable
{
    private const int WmHotkey = 0x0312;
    private const uint ModNoRepeat = 0x4000;
    private static readonly IntPtr HwndMessage = new(-3);

    private readonly HwndSource _source;
    private readonly List<HotkeyAction> _registered = new();
    private IReadOnlyList<HotkeyBinding> _lastBindings = Array.Empty<HotkeyBinding>();

    public event Action<HotkeyAction>? HotkeyPressed;

    /// <summary>Actions whose combo was already taken by another app.</summary>
    public IReadOnlyList<HotkeyAction> Conflicts { get; private set; } = Array.Empty<HotkeyAction>();

    public HotkeyHost()
    {
        var parameters = new HwndSourceParameters("GameDeckHotkeys")
        {
            ParentWindow = HwndMessage,
            WindowStyle = 0,
            Width = 0,
            Height = 0,
        };
        _source = new HwndSource(parameters);
        _source.AddHook(WndProc);
    }

    public IReadOnlyList<HotkeyAction> Register(IEnumerable<HotkeyBinding> bindings)
    {
        UnregisterAll();
        _lastBindings = bindings.ToList();

        var conflicts = new List<HotkeyAction>();
        foreach (var binding in _lastBindings)
        {
            var ok = NativeMethods.RegisterHotKey(
                _source.Handle,
                (int)binding.Action,
                (uint)binding.Modifiers | ModNoRepeat,
                binding.VirtualKey);

            if (ok)
                _registered.Add(binding.Action);
            else
                conflicts.Add(binding.Action);
        }

        Conflicts = conflicts;
        return Conflicts;
    }

    /// <summary>
    /// Re-registers the last binding set. Windows can silently drop
    /// registrations across lock/unlock and shell restarts.
    /// </summary>
    public IReadOnlyList<HotkeyAction> ReRegister() => Register(_lastBindings);

    private void UnregisterAll()
    {
        foreach (var action in _registered)
            NativeMethods.UnregisterHotKey(_source.Handle, (int)action);
        _registered.Clear();
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmHotkey && Enum.IsDefined((HotkeyAction)wParam.ToInt32()))
        {
            HotkeyPressed?.Invoke((HotkeyAction)wParam.ToInt32());
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        UnregisterAll();
        _source.RemoveHook(WndProc);
        _source.Dispose();
    }

    private static class NativeMethods
    {
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnregisterHotKey(IntPtr hWnd, int id);
    }
}
