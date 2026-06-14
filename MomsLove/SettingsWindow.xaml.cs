using MomsLove.Core;
using MomsLove.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using DataFormats = System.Windows.DataFormats;
using DragDropEffects = System.Windows.DragDropEffects;
using MessageBox = System.Windows.MessageBox;

namespace MomsLove;

public partial class SettingsWindow : Window
{
    private readonly AppDataStore _store;
    private readonly AppConfig _config;
    private readonly ObservableCollection<RuleRow> _rules;
    private bool _ready;

    public Func<Task>? UsageChangedAsync { get; set; }
    public Func<Task>? ExitRequestedAsync { get; set; }

    public SettingsWindow(AppDataStore store, AppConfig config)
    {
        InitializeComponent();
        _store = store;
        _config = CloneConfig(config);
        foreach (var rule in _config.Rules)
        {
            AppIconService.EnsureIcon(rule);
        }

        _rules = new ObservableCollection<RuleRow>(_config.Rules.Select(RuleRow.FromRule));
        RuleList.ItemsSource = _rules;
        StartupCheckBox.IsChecked = _config.RunAtStartup;
        RefreshPolicyText();
        SaveStatusText.Text = "修改后点击保存设置生效";
        _ready = true;
    }

    public static bool EnsurePassword(Window owner, AppConfig config)
    {
        if (!config.Password.IsConfigured)
        {
            var create = new ConfirmPasswordWindow { Owner = owner };
            if (create.ShowDialog() != true)
            {
                return false;
            }

            try
            {
                config.Password = PasswordHasher.Create(create.Password);
                return true;
            }
            catch (ArgumentException ex)
            {
                MessageBox.Show(owner, ex.Message, "家长密码", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        var verify = new PasswordWindow("输入家长密码", "密码只用于进入设置，不能增加当天游戏次数。") { Owner = owner };
        if (verify.ShowDialog() != true)
        {
            return false;
        }

        if (PasswordHasher.Verify(config.Password, verify.Password))
        {
            return true;
        }

        MessageBox.Show(owner, "密码不正确。", "家长密码", MessageBoxButton.OK, MessageBoxImage.Warning);
        return false;
    }

    private async Task SaveAsync()
    {
        _config.Rules.Clear();
        _config.Rules.AddRange(_rules.Select(row => row.Rule));
        StartupService.SetEnabled(_config.RunAtStartup);
        await _store.SaveConfigAsync(_config);
        SaveStatusText.Text = $"已保存：{DateTime.Now:HH:mm:ss}";
    }

    private void RefreshPolicyText()
    {
        DailyCountText.Text = $"{_config.Policy.DailyPlayCount} 次";
        SessionText.Text = $"{_config.Policy.SessionMinutes} 分钟";
        CooldownText.Text = $"{_config.Policy.CooldownMinutes} 分钟";
        GraceText.Text = $"{_config.Policy.GraceMinutes} 分钟";
    }

    private void ChangePolicy(Action<ControlPolicy> change)
    {
        change(_config.Policy);
        _config.Policy.DailyPlayCount = Math.Clamp(_config.Policy.DailyPlayCount, 1, 12);
        _config.Policy.SessionMinutes = SnapToMinuteTier(_config.Policy.SessionMinutes, 240);
        _config.Policy.CooldownMinutes = SnapToMinuteTier(_config.Policy.CooldownMinutes, 240);
        _config.Policy.GraceMinutes = SnapToMinuteTier(_config.Policy.GraceMinutes, 60);
        RefreshPolicyText();
        MarkDirty();
    }

    private static int SnapToMinuteTier(int minutes, int max)
    {
        if (minutes <= 1)
        {
            return 1;
        }

        var snapped = (int)Math.Round(minutes / 5.0, MidpointRounding.AwayFromZero) * 5;
        return Math.Clamp(snapped, 5, max);
    }

    private static int PreviousMinuteTier(int minutes)
    {
        if (minutes <= 5)
        {
            return 1;
        }

        return ((minutes - 1) / 5) * 5;
    }

    private static int NextMinuteTier(int minutes, int max)
    {
        if (minutes < 5)
        {
            return 5;
        }

        return Math.Min(((minutes / 5) + 1) * 5, max);
    }

    private void DailyMinus_Click(object sender, RoutedEventArgs e) => ChangePolicy(p => p.DailyPlayCount--);
    private void DailyPlus_Click(object sender, RoutedEventArgs e) => ChangePolicy(p => p.DailyPlayCount++);
    private void SessionMinus_Click(object sender, RoutedEventArgs e) => ChangePolicy(p => p.SessionMinutes = PreviousMinuteTier(p.SessionMinutes));
    private void SessionPlus_Click(object sender, RoutedEventArgs e) => ChangePolicy(p => p.SessionMinutes = NextMinuteTier(p.SessionMinutes, 240));
    private void CooldownMinus_Click(object sender, RoutedEventArgs e) => ChangePolicy(p => p.CooldownMinutes = PreviousMinuteTier(p.CooldownMinutes));
    private void CooldownPlus_Click(object sender, RoutedEventArgs e) => ChangePolicy(p => p.CooldownMinutes = NextMinuteTier(p.CooldownMinutes, 240));
    private void GraceMinus_Click(object sender, RoutedEventArgs e) => ChangePolicy(p => p.GraceMinutes = PreviousMinuteTier(p.GraceMinutes));
    private void GracePlus_Click(object sender, RoutedEventArgs e) => ChangePolicy(p => p.GraceMinutes = NextMinuteTier(p.GraceMinutes, 60));

    private void Startup_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready)
        {
            return;
        }

        _config.RunAtStartup = StartupCheckBox.IsChecked == true;
        MarkDirty();
    }

    private void RuleEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (_ready)
        {
            MarkDirty();
        }
    }

    private void ChangePassword_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new ConfirmPasswordWindow { Owner = this };
        dialog.Title = "修改家长密码";
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            _config.Password = PasswordHasher.Create(dialog.Password);
            MarkDirty();
            MessageBox.Show(this, "家长密码已修改，点击保存设置后生效。", "家长密码", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(this, ex.Message, "家长密码", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void Save_Click(object sender, RoutedEventArgs e)
    {
        await SaveAsync();
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
    }

    private async void ExitApp_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "确定要退出妈妈的爱吗？退出后将不再守护游戏时间。", "退出应用", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            return;
        }

        if (ExitRequestedAsync is not null)
        {
            await ExitRequestedAsync();
        }

        DialogResult = false;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is DependencyObject source && FindVisualParent<System.Windows.Controls.Button>(source) is not null)
        {
            return;
        }

        DragMove();
    }

    private static T? FindVisualParent<T>(DependencyObject source)
        where T : DependencyObject
    {
        var current = source;
        while (current is not null)
        {
            if (current is T match)
            {
                return match;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private async void ClearRest_Click(object sender, RoutedEventArgs e)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var usage = await _store.LoadUsageAsync(today);
        if (usage.State is PlayState.Cooldown or PlayState.GracePeriod)
        {
            usage.State = usage.SessionsStarted >= _config.Policy.DailyPlayCount
                ? PlayState.LockedForToday
                : PlayState.Idle;
            usage.SessionStartedAt = null;
            usage.GraceStartedAt = null;
            usage.CooldownUntil = null;
            await _store.SaveUsageAsync(usage);
            if (UsageChangedAsync is not null)
            {
                await UsageChangedAsync();
            }

            SaveStatusText.Text = "已清除当前休息时间";
            return;
        }

        SaveStatusText.Text = "当前没有休息时间需要清除";
    }

    private async void ResetTodayCount_Click(object sender, RoutedEventArgs e)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var usage = await _store.LoadUsageAsync(today);
        usage.SessionsStarted = 0;
        usage.State = PlayState.Idle;
        usage.SessionStartedAt = null;
        usage.GraceStartedAt = null;
        usage.CooldownUntil = null;
        usage.ActiveSessionMinutes = 0;
        usage.ActiveGraceMinutes = 0;
        usage.ActiveCooldownMinutes = 0;
        await _store.SaveUsageAsync(usage);
        if (UsageChangedAsync is not null)
        {
            await UsageChangedAsync();
        }

        SaveStatusText.Text = "已重置今日计数";
    }

    private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, System.Windows.DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return;
        }

        foreach (var path in (string[])e.Data.GetData(DataFormats.FileDrop))
        {
            try
            {
                var rule = RuleTargetResolver.CreateRule(path);
                AppIconService.EnsureIcon(rule);
                if (_rules.All(row => !row.Rule.Path.Equals(rule.Path, StringComparison.OrdinalIgnoreCase)))
                {
                    _rules.Add(RuleRow.FromRule(rule));
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "控制列表", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        MarkDirty();
    }

    private void MarkDirty()
    {
        SaveStatusText.Text = "有未保存的修改";
    }

    private static AppConfig CloneConfig(AppConfig config)
    {
        var json = JsonSerializer.Serialize(config);
        return JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
    }

    public sealed class RuleRow
    {
        private RuleRow(ControlRule rule)
        {
            Rule = rule;
        }

        public ControlRule Rule { get; }
        public string DisplayName => Rule.DisplayName;
        public bool IsEnabled
        {
            get => Rule.IsEnabled;
            set => Rule.IsEnabled = value;
        }

        public string KindLabel => Rule.Type switch
        {
            ControlRuleType.Directory => "目录",
            ControlRuleType.Shortcut => "快捷方式",
            _ => "应用程序"
        };

        public string IconPath => Rule.IconPath;
        public bool HasIcon => !string.IsNullOrWhiteSpace(Rule.IconPath) && File.Exists(Rule.IconPath);
        public string IconGlyph => Rule.Type == ControlRuleType.Directory ? "📁" : "□";

        public static RuleRow FromRule(ControlRule rule) => new(rule);
    }
}
