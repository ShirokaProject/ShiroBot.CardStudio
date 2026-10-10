using Avalonia;

namespace CardStudio;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .ConfigureFonts(SystemFontConfiguration.Apply)
            .LogToTrace();
}
