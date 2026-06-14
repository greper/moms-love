using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace MomsLove;

public partial class TimeOverlayWindow : Window
{
    private bool _hasUserPosition;
    private bool _isBlinking;
    private DispatcherTimer? _blinkTimer;

    public TimeOverlayWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => MoveToTopCenter();
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

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _hasUserPosition = true;
        DragMove();
    }
}
