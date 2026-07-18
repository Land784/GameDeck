using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Animation;

namespace GameDeck.App.Overlay;

/// <summary>
/// Thin adapter over the overlay card. All decisions live in
/// OverlayStateMachine/OverlayController; this class only renders. Never
/// call Activate() or Focus() here: the game must keep focus.
/// </summary>
public partial class OverlayWindow : Window
{
    private IntPtr _hwnd;

    public OverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            _hwnd = new WindowInteropHelper(this).Handle;
            WindowInterop.ApplyOverlayStyles(_hwnd);
        };
        MouseLeftButtonDown += (_, e) =>
        {
            if (IsClickThrough || e.ButtonState != MouseButtonState.Pressed) return;
            DragMove();
        };
    }

    public bool IsClickThrough { get; private set; } = true;

    public void SetClickThrough(bool clickThrough)
    {
        IsClickThrough = clickThrough;
        if (_hwnd != IntPtr.Zero)
            WindowInterop.SetClickThrough(_hwnd, clickThrough);
    }

    public void ShowTopRight()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Right - Width - 16;
        Top = area.Top + 16;
        Show();
        AssertTopmost();
    }

    public void AssertTopmost()
    {
        if (_hwnd != IntPtr.Zero)
            WindowInterop.AssertTopmost(_hwnd);
    }

    public void BeginFade(double targetOpacity, TimeSpan duration)
    {
        var animation = new DoubleAnimation(targetOpacity, duration)
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
        };
        BeginAnimation(OpacityProperty, animation);
    }
}
