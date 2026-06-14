using MomsLove.Core;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace MomsLove.Services;

public static class AppIconService
{
    private static readonly string IconDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "MomsLove",
        "icons");

    public static bool EnsureIcon(ControlRule rule)
    {
        if (rule.Type == ControlRuleType.Directory)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(rule.IconPath) && File.Exists(rule.IconPath))
        {
            return false;
        }

        if (!File.Exists(rule.Path))
        {
            return false;
        }

        try
        {
            Directory.CreateDirectory(IconDirectory);
            var iconPath = Path.Combine(IconDirectory, $"{rule.Id:N}.png");
            using var icon = Icon.ExtractAssociatedIcon(rule.Path);
            if (icon is null)
            {
                return false;
            }

            using var bitmap = icon.ToBitmap();
            bitmap.Save(iconPath, ImageFormat.Png);
            rule.IconPath = iconPath;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
