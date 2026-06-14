using System.Windows;
using System.Windows.Input;

namespace MomsLove;

public partial class PasswordWindow : Window
{
    public PasswordWindow(string title, string hint)
    {
        InitializeComponent();
        TitleText.Text = title;
        HintText.Text = hint;
    }

    public string Password => PasswordBox.Password;

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PasswordBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        DragMove();
    }
}
