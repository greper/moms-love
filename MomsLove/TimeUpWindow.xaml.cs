using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace MomsLove;

public partial class TimeUpWindow : Window
{
    private readonly DispatcherTimer _autoRestTimer = new DispatcherTimer
    {
        Interval = TimeSpan.FromMinutes(1)
    };

    public TimeUpWindow()
    {
        InitializeComponent();
        _autoRestTimer.Tick += AutoRestTimer_Tick;
        Loaded += (_, _) => _autoRestTimer.Start();
    }

    public bool RestNow { get; private set; }

    private void AutoRestTimer_Tick(object? sender, EventArgs e)
    {
        _autoRestTimer.Stop();
        RestNow_Click(this, new RoutedEventArgs());
    }

    private void RestNow_Click(object sender, RoutedEventArgs e)
    {
        _autoRestTimer.Stop();
        RestNow = true;
        DialogResult = true;
    }

    private void Wait_Click(object sender, RoutedEventArgs e)
    {
        _autoRestTimer.Stop();
        RestNow = false;
        DialogResult = true;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _autoRestTimer.Stop();
        RestNow = false;
        DialogResult = true;
    }
}
