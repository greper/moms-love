using MomsLove.Core;
using System.Diagnostics;
using System.IO;
using System.Windows;
using MessageBox = System.Windows.MessageBox;

namespace MomsLove.Services;

public sealed class ProcessGuardService
{
    private static readonly TimeSpan NotificationCooldown = TimeSpan.FromSeconds(15);
    private readonly Window _owner;
    private readonly Dictionary<string, DateTimeOffset> _lastNotificationByPath = new(StringComparer.OrdinalIgnoreCase);

    public ProcessGuardService(Window owner)
    {
        _owner = owner;
    }

    public void Enforce(AppConfig config, PlayState state, DateTimeOffset now)
    {
        if (IsPlayAllowed(state))
        {
            return;
        }

        var rules = config.Rules
            .Where(rule => rule.IsEnabled)
            .Select(NormalizeRule)
            .Where(rule => !string.IsNullOrWhiteSpace(rule.Path))
            .ToList();

        if (rules.Count == 0)
        {
            return;
        }

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                var processPath = TryGetProcessPath(process);
                if (string.IsNullOrWhiteSpace(processPath))
                {
                    continue;
                }

                var matchedRule = rules.FirstOrDefault(rule => IsMatch(rule, processPath));
                if (matchedRule is null)
                {
                    continue;
                }

                if (TryStopProcess(process))
                {
                    NotifyBlocked(matchedRule, state, now);
                }
            }
        }
    }

    private static bool IsPlayAllowed(PlayState state)
    {
        return state is PlayState.Running or PlayState.GracePeriod;
    }

    public bool HasRunningGame(AppConfig config)
    {
        var rules = config.Rules
            .Where(rule => rule.IsEnabled)
            .Select(NormalizeRule)
            .Where(rule => !string.IsNullOrWhiteSpace(rule.Path))
            .ToList();

        if (rules.Count == 0)
        {
            return false;
        }

        foreach (var process in Process.GetProcesses())
        {
            using (process)
            {
                var processPath = TryGetProcessPath(process);
                if (string.IsNullOrWhiteSpace(processPath))
                {
                    continue;
                }

                if (rules.Any(rule => IsMatch(rule, processPath)))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static GuardRule NormalizeRule(ControlRule rule)
    {
        return new GuardRule(
            rule.DisplayName,
            NormalizePath(rule.Path),
            rule.Type);
    }

    private static bool IsMatch(GuardRule rule, string processPath)
    {
        var normalizedProcessPath = NormalizePath(processPath);
        if (rule.Type == ControlRuleType.Directory)
        {
            var directory = rule.Path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return normalizedProcessPath.StartsWith(directory + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }

        return normalizedProcessPath.Equals(rule.Path, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "";
        }

        try
        {
            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }

    private static string? TryGetProcessPath(Process process)
    {
        try
        {
            return process.MainModule?.FileName;
        }
        catch
        {
            return null;
        }
    }

    private static bool TryStopProcess(Process process)
    {
        try
        {
            if (process.HasExited)
            {
                return false;
            }

            if (process.CloseMainWindow() && process.WaitForExit(800))
            {
                return true;
            }

            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private void NotifyBlocked(GuardRule rule, PlayState state, DateTimeOffset now)
    {
        if (_lastNotificationByPath.TryGetValue(rule.Path, out var lastNotified)
            && now - lastNotified < NotificationCooldown)
        {
            return;
        }

        _lastNotificationByPath[rule.Path] = now;
        var reason = state == PlayState.Cooldown
            ? "现在是休息时间，先喝点水、看看远处。"
            : "现在还不是可以玩的时间。";

        _owner.Dispatcher.BeginInvoke(() =>
        {
            if (_owner.WindowState == WindowState.Minimized)
            {
                _owner.WindowState = WindowState.Normal;
            }

            _owner.Activate();
            MessageBox.Show(_owner, $"{reason}\n\n已关闭：{rule.DisplayName}", "妈妈的爱", MessageBoxButton.OK, MessageBoxImage.Information);
        });
    }

    private sealed record GuardRule(string DisplayName, string Path, ControlRuleType Type);
}
