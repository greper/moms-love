using System.Drawing;
using System.IO;
using System.Windows;
using Forms = System.Windows.Forms;
using WpfApplication = System.Windows.Application;

namespace MomsLove.Services;

public sealed class TrayIconService : IDisposable
{
    private readonly Window _mainWindow;
    private readonly Forms.NotifyIcon _notifyIcon;
    private bool _disposed;

    public TrayIconService(Window mainWindow)
    {
        _mainWindow = mainWindow;
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = LoadIcon(),
            Text = "妈妈的爱",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };
        _notifyIcon.DoubleClick += (_, _) => ShowMainWindow();
    }

    private static Icon LoadIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app-icon.ico");
        if (File.Exists(iconPath))
        {
            return new Icon(iconPath);
        }

        var resource = WpfApplication.GetResourceStream(new Uri("pack://application:,,,/Assets/app-icon.ico"));
        if (resource is not null)
        {
            return new Icon(resource.Stream);
        }

        return SystemIcons.Application;
    }

    public void ShowMainWindow()
    {
        if (_disposed)
        {
            return;
        }

        _mainWindow.Show();
        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.Activate();
    }

    public void ShowMinimizedHint()
    {
        if (_disposed)
        {
            return;
        }

        _notifyIcon.BalloonTipTitle = "妈妈的爱还在运行";
        _notifyIcon.BalloonTipText = "已最小化到托盘，双击图标可以打开主界面。";
        _notifyIcon.ShowBalloonTip(2500);
    }

    private Forms.ContextMenuStrip BuildMenu()
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("打开主界面", null, (_, _) => ShowMainWindow());
        menu.Items.Add(new Forms.ToolStripSeparator());
        return menu;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}
