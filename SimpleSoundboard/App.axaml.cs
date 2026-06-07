using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform;

namespace SimpleSoundboard;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            // Keep the process alive when the window is closed (it hides to tray),
            // so sounds and the mic passthrough keep running in the background.
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;

            var window = new MainWindow();
            desktop.MainWindow = window;
            SetupTrayIcon(desktop, window);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private void SetupTrayIcon(IClassicDesktopStyleApplicationLifetime desktop, MainWindow window)
    {
        var show = new NativeMenuItem("Show");
        show.Click += (_, _) => window.ShowFromTray();

        var quit = new NativeMenuItem("Quit");
        quit.Click += (_, _) =>
        {
            window.AllowClose();
            desktop.Shutdown();
        };

        var menu = new NativeMenu();
        menu.Items.Add(show);
        menu.Items.Add(quit);

        var tray = new TrayIcon
        {
            Icon = window.Icon ?? TryLoadIcon(),
            ToolTipText = "Simple Soundboard",
            Menu = menu,
            IsVisible = true
        };
        tray.Clicked += (_, _) => window.ShowFromTray();

        TrayIcon.SetIcons(this, new TrayIcons { tray });
    }

    private static WindowIcon? TryLoadIcon()
    {
        try
        {
            return new WindowIcon(AssetLoader.Open(new Uri("avares://SimpleSoundboard/Assets/logo.ico")));
        }
        catch
        {
            return null;
        }
    }
}
