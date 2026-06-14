using System.Windows;
using System.Windows.Input;

namespace MomsLove;

public partial class TimeUpWindow : Window
{
    public TimeUpWindow()
    {
        InitializeComponent();
    }

    public bool RestNow { get; private set; }

    private void RestNow_Click(object sender, RoutedEventArgs e)
    {
        RestNow = true;
        DialogResult = true;
    }

    private void Wait_Click(object sender, RoutedEventArgs e)
    {
        RestNow = false;
        DialogResult = true;
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        RestNow = false;
        DialogResult = true;
    }
}
