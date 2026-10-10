using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;

namespace CardStudio;

public sealed partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Window
            {
                Title = "CardStudio",
                Icon = new WindowIcon(AssetLoader.Open(new Uri("avares://CardStudio.Desktop/Assets/app-icon.png"))),
                Width = 1480,
                Height = 900,
                MinWidth = 1360,
                MinHeight = 700,
                Content = new CardStudioView()
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new CardStudioView();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
