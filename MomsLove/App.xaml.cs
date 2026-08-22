using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Threading;
using MomsLove.Core;

namespace MomsLove;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    public static AppLogger Logger { get; } = new();

    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += (_, args) => { Logger.Write("Dispatcher 未处理异常", args.Exception); args.Handled = true; };
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Logger.Write("AppDomain 未处理异常", args.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, args) => { Logger.Write("未观察到的 Task 异常", args.Exception); args.SetObserved(); };
        Logger.Write("应用启动");
        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Logger.Write($"应用退出，代码={e.ApplicationExitCode}");
        base.OnExit(e);
    }
}
