namespace SimpleSoundboard.Diagnostics;

/// <summary>
/// Dead-simple file logger so we can see what the GUI does at runtime
/// (a WinExe has no console). Writes to %AppData%\SimpleSoundboard\log.txt.
/// </summary>
public static class Log
{
    private static readonly object Gate = new();
    public static readonly string LogPath = BuildPath();

    private static string BuildPath()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "SimpleSoundboard");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "log.txt");
    }

    public static void Write(string message)
    {
        try
        {
            lock (Gate)
            {
                File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff}  {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // logging must never crash the app
        }
    }
}
