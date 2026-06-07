using Avalonia;
using SimpleSoundboard.Audio;

namespace SimpleSoundboard;

internal static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Contains("--audiotest"))
        {
            AudioDiagnostics.Run();
            return;
        }

        var reproIdx = Array.IndexOf(args, "--repro");
        if (reproIdx >= 0 && reproIdx + 1 < args.Length)
        {
            AudioDiagnostics.Repro(args[reproIdx + 1]);
            return;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
