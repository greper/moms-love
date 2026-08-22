using MomsLove.Core;
using MomsLove.Services;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using MessageBox = System.Windows.MessageBox;

namespace MomsLove;

public partial class MainWindow : Window
{
    private static readonly AppLogger Logger = App.Logger;
    private readonly AppDataStore _store = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private readonly ObservableCollection<GameRow> _games = [];
    private readonly ProcessGuardService _processGuard;
    private readonly TimeOverlayWindow _timeOverlay = new();
    private readonly TrayIconService _trayIcon;
    private AppConfig _config = new();
    private PlaySessionManager? _sessionManager;
    private DateTimeOffset? _notifiedGraceSegmentEndsAt;
    private bool _allowExit;
    private bool _hasShownTrayHint;

    public MainWindow()
    {
        Logger.Write("主窗口初始化");
        InitializeComponent();
        _processGuard = new ProcessGuardService(this);
        _trayIcon = new TrayIconService(this);
        GameList.ItemsSource = _games;
        _timer.Tick += Timer_Tick;

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        if (version is not null)
        {
            VersionText.Text = $"v{version.Major}.{version.Minor}.{version.Build}";
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try { await ReloadStateAsync(); _timer.Start(); await TickAndSaveAsync(); Logger.Write("主窗口加载完成"); }
        catch (Exception ex) { Logger.Write("窗口加载失败", ex); MessageBox.Show(this, "应用初始化失败，详细信息已写入日志。", "妈妈的爱", MessageBoxButton.OK, MessageBoxImage.Error); }
    }

    private async void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!_allowExit)
        {
            e.Cancel = true;
            Hide();
            if (!_hasShownTrayHint)
            {
                _hasShownTrayHint = true;
                _trayIcon.ShowMinimizedHint();
            }

            return;
        }

        if (_sessionManager is not null)
        {
            await _store.SaveUsageAsync(_sessionManager.Usage);
        }

        _timeOverlay.Close();
        _trayIcon.Dispose();
    }

    public void ExitFromTray()
    {
        _allowExit = true;
        Close();
    }

    private async void Timer_Tick(object? sender, EventArgs e)
    {
        try { await TickAndSaveAsync(); } catch (Exception ex) { Logger.Write("计时器处理失败", ex); }
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private async Task TickAndSaveAsync()
    {
        if (_sessionManager is null)
        {
            return;
        }

        if (_sessionManager.Usage.State == PlayState.Idle
            && _processGuard.HasRunningGame(_config)
            && _sessionManager.TryStart(DateTimeOffset.Now))
        {
        }

        var now = DateTimeOffset.Now;
        var previousState = _sessionManager.Usage.State;
        var snapshot = _sessionManager.Tick(now);
        if (snapshot.State != previousState) Logger.Write($"游戏状态变更：{previousState} -> {snapshot.State}");
        Render(snapshot);
        _processGuard.Enforce(_config, snapshot.State, now);

        if (snapshot.State == PlayState.GracePeriod
            && snapshot.StateEndsAt is DateTimeOffset graceSegmentEndsAt
            && _notifiedGraceSegmentEndsAt != graceSegmentEndsAt)
        {
            _notifiedGraceSegmentEndsAt = graceSegmentEndsAt;
            ShowTimeUpDialog();
        }

        await _store.SaveUsageAsync(_sessionManager.Usage);
    }

    private void Render(PlaySnapshot snapshot)
    {
        IdlePanel.Visibility = snapshot.State == PlayState.Idle ? Visibility.Visible : Visibility.Collapsed;
        RunningPanel.Visibility = snapshot.State is PlayState.Running or PlayState.GracePeriod ? Visibility.Visible : Visibility.Collapsed;
        CooldownPanel.Visibility = snapshot.State == PlayState.Cooldown ? Visibility.Visible : Visibility.Collapsed;
        LockedPanel.Visibility = snapshot.State == PlayState.LockedForToday ? Visibility.Visible : Visibility.Collapsed;

        IdleSessionText.Text = FormatMinutes(_config.Policy.SessionMinutes);
        IdleCountBadge.Text = snapshot.RemainingSessions.ToString();
        IdleTitleText.Text = $"今天还可以玩 {snapshot.RemainingSessions} 次";
        IdleSubText.Text = $"下一次休息 {_config.Policy.CooldownMinutes} 分钟";

        RunningTimeText.Text = FormatTime(snapshot.RemainingInState);
        RenderBurningRope(snapshot);
        RunningNoteText.Text = "玩完这一局就准备休息";
        RunningTitleText.Text = snapshot.State == PlayState.GracePeriod ? "准备收尾啦" : "选择今天要玩的游戏";

        if (snapshot.State == PlayState.GracePeriod)
        {
            RunningSubtitleText.Text = $"尽快在 {Math.Ceiling(snapshot.RemainingInState.TotalMinutes):F0} 分钟内完成收尾操作，否则直接关闭游戏";
            RunningSubtitleText.Foreground = System.Windows.Media.Brushes.OrangeRed;
        }
        else
        {
            RunningSubtitleText.Text = "这一轮已经开始计时。点击下面的游戏图标就能打开，多个游戏也算同一轮。";
            RunningSubtitleText.Foreground = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#607b8a"));
        }

        TitleSessionText.Text = $"今天还剩 {snapshot.RemainingSessions} 次机会";
        GameCountText.Text = $"{_games.Count} 个可用";

        CooldownTimeText.Text = FormatTime(snapshot.RemainingInState);
        RenderTimeOverlay(snapshot);
    }

    private void RenderBurningRope(PlaySnapshot snapshot)
    {
        const double ropeStart = 14;
        const double ropeEnd = 166;
        const double flameHalfWidth = 14;

        var totalMinutes = snapshot.State == PlayState.GracePeriod
            ? _sessionManager?.Usage.ActiveGraceMinutes ?? _config.Policy.GraceMinutes
            : _sessionManager?.Usage.ActiveSessionMinutes ?? _config.Policy.SessionMinutes;
        var totalSeconds = Math.Max(1, totalMinutes * 60.0);
        var remainingSeconds = Math.Clamp(snapshot.RemainingInState.TotalSeconds, 0, totalSeconds);
        var burnedFraction = 1 - remainingSeconds / totalSeconds;
        var flameCenter = ropeStart + (ropeEnd - ropeStart) * burnedFraction;

        BurnedRopeLine.X2 = flameCenter;
        AshRopeLine.X2 = flameCenter;
        Canvas.SetLeft(RopeFlame, flameCenter - flameHalfWidth);
        Canvas.SetLeft(RopeSparkOne, flameCenter - 20);
        Canvas.SetLeft(RopeSparkTwo, flameCenter - 9);
    }

    private void RenderTimeOverlay(PlaySnapshot snapshot)
    {
        if (snapshot.State is not (PlayState.Running or PlayState.GracePeriod))
        {
            _timeOverlay.Hide();
            return;
        }

        var totalMinutes = snapshot.State == PlayState.GracePeriod
            ? _sessionManager?.Usage.ActiveGraceMinutes ?? _config.Policy.GraceMinutes
            : _sessionManager?.Usage.ActiveSessionMinutes ?? _config.Policy.SessionMinutes;

        _timeOverlay.UpdateTimer(
            snapshot.RemainingInState,
            TimeSpan.FromMinutes(Math.Max(1, totalMinutes)),
            snapshot.State == PlayState.GracePeriod);

        if (!_timeOverlay.IsVisible)
        {
            _timeOverlay.Show();
        }
    }

    private async void StartPlay_Click(object sender, RoutedEventArgs e)
    {
        if (_sessionManager is null)
        {
            return;
        }

        if (!_sessionManager.TryStart(DateTimeOffset.Now))
        {
            MessageBox.Show(this, "今天的游戏次数已经用完啦。", "妈妈的爱", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        else Logger.Write("开始游戏会话");

        await TickAndSaveAsync();
    }

    private async void RestNow_Click(object sender, RoutedEventArgs e)
    {
        if (_sessionManager is null)
        {
            return;
        }

        if (MessageBox.Show(this, "确定不玩了吗？这轮游戏时间会立刻结束。", "妈妈的爱", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
        {
            return;
        }

        _sessionManager.FinishNow(DateTimeOffset.Now);
        Logger.Write("手动结束游戏会话");
        await TickAndSaveAsync();
    }

    private async void Settings_Click(object sender, RoutedEventArgs e)
    {
        if (!SettingsWindow.EnsurePassword(this, _config))
        {
            return;
        }

        var settings = new SettingsWindow(_store, _config) { Owner = this };
        settings.UsageChangedAsync = ReloadStateAsync;
        settings.ExitRequestedAsync = async () =>
        {
            _allowExit = true;
            if (_sessionManager is not null)
            {
                await _store.SaveUsageAsync(_sessionManager.Usage);
            }

            _timeOverlay.Close();
            _trayIcon.Dispose();
            Close();
        };
        var saved = settings.ShowDialog() == true;
        Logger.Write(saved ? "保存设置" : "取消设置");
        await ReloadStateAsync();
        if (saved)
        {
            _sessionManager?.UpdatePolicy(_config.Policy);
        }

        await TickAndSaveAsync();
    }

    private async Task ReloadStateAsync()
    {
        Logger.Write("加载配置和使用状态");
        _config = await _store.LoadConfigAsync();
        var iconUpdated = false;
        foreach (var rule in _config.Rules)
        {
            iconUpdated |= AppIconService.EnsureIcon(rule);
        }

        if (iconUpdated)
        {
            await _store.SaveConfigAsync(_config);
        }

        var usage = await _store.LoadUsageAsync(DateOnly.FromDateTime(DateTime.Now));
        _sessionManager = new PlaySessionManager(usage, _config.Policy);
        RefreshGames();
        Render(_sessionManager.Tick(DateTimeOffset.Now));
    }

    private void ShowTimeUpDialog()
    {
        if (_sessionManager is null)
        {
            return;
        }

        var dialog = new TimeUpWindow { Owner = this };
        dialog.ShowDialog();
        if (dialog.RestNow)
        {
            _sessionManager.FinishNow(DateTimeOffset.Now);
        }
    }

    private void RefreshGames()
    {
        _games.Clear();
        foreach (var rule in _config.Rules.Where(rule => rule.IsEnabled))
        {
            _games.Add(new GameRow(rule));
        }
    }

    private void OpenGame_Click(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not GameRow row)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = row.Rule.Path,
                UseShellExecute = true
            });
            Logger.Write($"启动游戏：{row.Rule.Path}");
        }
        catch (Exception ex)
        {
            Logger.Write($"启动游戏失败：{row.Rule.Path}", ex);
            MessageBox.Show(this, $"打不开这个游戏：{ex.Message}", "妈妈的爱", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private static string FormatMinutes(int minutes)
    {
        return $"{minutes:00}:00";
    }

    private static string FormatTime(TimeSpan time)
    {
        var totalSeconds = Math.Max(0, (int)Math.Ceiling(time.TotalSeconds));
        return $"{totalSeconds / 60:00}:{totalSeconds % 60:00}";
    }

    public sealed class GameRow
    {
        public GameRow(ControlRule rule)
        {
            Rule = rule;
        }

        public ControlRule Rule { get; }
        public string DisplayName => Rule.DisplayName;
        public string IconPath => Rule.IconPath;
        public bool HasIcon => !string.IsNullOrWhiteSpace(Rule.IconPath) && File.Exists(Rule.IconPath);
        public string IconGlyph => Rule.Type == ControlRuleType.Directory ? "📁" : "□";
    }
}
