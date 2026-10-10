using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;

namespace CardStudio;

internal static class SystemFontConfiguration
{
    public static void Apply(FontManager fontManager)
    {
        var installed = fontManager.SystemFonts.Select(x => x.Name);
        var platform = OperatingSystem.IsMacOS() ? OSPlatform.OSX
            : OperatingSystem.IsWindows() ? OSPlatform.Windows : OSPlatform.Linux;
        var family = SelectFamily(installed, platform);
        if (family is null) return;

        var font = new FontFamily(family);
        // Material's own family list overrides inherited/default fonts. Override
        // its typography resources while keeping explicit AXAML fonts available.
        Application.Current!.Resources["Md3FontFamilyPlain"] = font;
        Application.Current.Resources["Md3FontFamilyBrand"] = font;
    }

    internal static string? SelectFamily(IEnumerable<string> installed, OSPlatform platform)
    {
        // A family covering both Chinese and Latin avoids different line metrics
        // for mixed text (14.8万) and numeric-only text (7,305).
        string[] preferred = platform == OSPlatform.OSX ? ["PingFang SC", "Heiti SC"]
            : platform == OSPlatform.Windows ? ["Microsoft YaHei UI", "Microsoft YaHei", "DengXian"]
            : [];
        string[] cjk = ["Noto Sans CJK SC", "Noto Sans SC", "Source Han Sans SC",
            "WenQuanYi Micro Hei", "WenQuanYi Zen Hei", "Droid Sans Fallback"];
        var available = installed.ToArray();
        foreach (var family in preferred.Concat(cjk))
        {
            var match = available.FirstOrDefault(x =>
                string.Equals(x, family, StringComparison.OrdinalIgnoreCase));
            if (match is not null) return match;
        }

        // A missing CJK family should not replace the working platform default.
        return null;
    }
}
