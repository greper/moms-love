using System.Windows;
using System.Windows.Input;

namespace MomsLove;

public partial class ConfirmPasswordWindow : Window
{
    public ConfirmPasswordWindow()
    {
        InitializeComponent();
    }

    public string Password => PasswordBox.Password;

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        PasswordBox.Focus();
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        if (PasswordBox.Password != ConfirmBox.Password)
        {
            ErrorText.Text = "两次输入的密码不一样。";
            ConfirmBox.Focus();
            ConfirmBox.SelectAll();
            return;
        }

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
