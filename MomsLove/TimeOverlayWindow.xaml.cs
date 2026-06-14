using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;

namespace MomsLove;

public partial class TimeOverlayWindow : Window
{
    private bool _hasUserPosition;
    private bool _isBlinking;
    private DispatcherTimer? _blinkTimer;
    private DispatcherTimer? _keepOnTopTimer;

    public TimeOverlayWindow()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            MoveToTopCenter();
            StartKeepOnTop();
        };
        Closed += (_, _) => StopKeepOnTop();
    }

    public void UpdateTimer(TimeSpan remaining, TimeSpan total, bool isGracePeriod)
    {
        OverlayTimeText.Text = FormatTime(remaining);
        OverlayTimeText.Foreground = isGracePeriod
            ? System.Windows.Media.Brushes.DarkOrange
            : System.Windows.Media.Brushes.DarkSlateBlue;

        if (remaining.TotalSeconds <= 40 && remaining.TotalSeconds > 0)
        {
            StartBlink();
        }
        else
        {
            StopBlink();
        }

        if (!_hasUserPosition)
        {
            MoveToTopCenter();
        }
    }

    public new void Hide()
    {
        StopBlink();
        base.Hide();
    }

    private void StartBlink()
    {
        if (_isBlinking)
        {
            return;
        }

        _isBlinking = true;
        _blinkTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _blinkTimer.Tick += (_, _) =>
        {
            OverlayBorder.Opacity = OverlayBorder.Opacity > 0.5 ? 0.25 : 1.0;
        };
        _blinkTimer.Start();
    }

    private void StopBlink()
    {
        if (!_isBlinking)
        {
            return;
        }

        _isBlinking = false;
        _blinkTimer?.Stop();
        _blinkTimer = null;
        OverlayBorder.Opacity = 1.0;
    }

    private static string FormatTime(TimeSpan time)
    {
        var totalSeconds = Math.Max(0, (int)Math.Ceiling(time.TotalSeconds));
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    private void MoveToTopCenter()
    {
        var area = SystemParameters.WorkArea;
        Left = area.Left + (area.Width - Width) / 2;
        Top = area.Top + 12;
    }

    private void StartKeepOnTop()
    {
        _keepOnTopTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _keepOnTopTimer.Tick += KeepOnTop_Tick;
        _keepOnTopTimer.Start();
    }

    private void StopKeepOnTop()
    {
        _keepOnTopTimer?.Stop();
        _keepOnTopTimer = null;
    }

    private void KeepOnTop_Tick(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero)
        {
            SetWindowPos(handle, HWND_TOPMOST, 0, 0, 0, 0,
                SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
        }
    }

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    private const uint SWP_NOMOVE = 0x0002;
    private const uint SWP_NOSIZE = 0x0001;
    private const uint SWP_NOACTIVATE = 0x0010;

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _hasUserPosition = true;
        DragMove();
    }
}
