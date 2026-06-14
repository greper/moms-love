using MomsLove.Core;
using System.IO;
using System.Reflection;

namespace MomsLove.Services;

public static class RuleTargetResolver
{
    public static ControlRule CreateRule(string inputPath)
    {
        var fullPath = Path.GetFullPath(inputPath);
        if (Directory.Exists(fullPath))
        {
            return NewRule(Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar)), fullPath, ControlRuleType.Directory, "Folder");
        }

        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException("找不到拖入的文件。", fullPath);
        }

        var extension = Path.GetExtension(fullPath);
        if (extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase))
        {
            var target = ResolveShortcut(fullPath);
            var displayName = Path.GetFileNameWithoutExtension(fullPath);
            return NewRule(displayName, target, ControlRuleType.Shortcut, "Shortcut");
        }

        if (extension.Equals(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return NewRule(Path.GetFileNameWithoutExtension(fullPath), fullPath, ControlRuleType.Executable, "Game");
        }

        throw new InvalidOperationException("请拖入 .lnk 快捷方式、.exe 应用程序或目录。");
    }

    private static ControlRule NewRule(string displayName, string path, ControlRuleType type, string iconKey)
    {
        return new ControlRule
        {
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "未命名应用" : displayName,
            Path = path,
            Type = type,
            IconKey = iconKey,
            IsEnabled = true,
            AddedAt = DateTimeOffset.Now
        };
    }

    private static string ResolveShortcut(string shortcutPath)
    {
        var shellType = Type.GetTypeFromProgID("WScript.Shell")
            ?? throw new InvalidOperationException("当前系统无法解析快捷方式。");
        var shell = Activator.CreateInstance(shellType)
            ?? throw new InvalidOperationException("当前系统无法解析快捷方式。");

        try
        {
            var shortcut = shellType.InvokeMember(
                "CreateShortcut",
                BindingFlags.InvokeMethod,
                null,
                shell,
                [shortcutPath]);

            var targetPath = shortcut?.GetType().InvokeMember(
                "TargetPath",
                BindingFlags.GetProperty,
                null,
                shortcut,
                null) as string;

            if (string.IsNullOrWhiteSpace(targetPath))
            {
                throw new InvalidOperationException("快捷方式没有有效目标。");
            }

            return targetPath;
        }
        finally
        {
            if (shell is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }
    }
}
