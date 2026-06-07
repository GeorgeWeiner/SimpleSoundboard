using System.Text.Json;

namespace SimpleSoundboard.Models;

/// <summary>Persisted soundboard state: the list of sounds plus user settings.</summary>
public sealed class SoundboardConfig
{
    public List<SoundClip> Sounds { get; set; } = new();
    public double Volume { get; set; } = 80;

    /// <summary>Play to the local monitor device (so you hear it too).</summary>
    public bool RouteToMonitor { get; set; } = true;

    /// <summary>
    /// WASAPI endpoint id of the chosen monitor device, or null to follow the
    /// system default. Persisted so the picker remembers your headphones.
    /// </summary>
    public string? MonitorDeviceId { get; set; }

    public bool RouteToVbCable { get; set; } = true;

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private static string ConfigPath
    {
        get
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SimpleSoundboard");
            Directory.CreateDirectory(dir);
            return Path.Combine(dir, "sounds.json");
        }
    }

    public static SoundboardConfig Load()
    {
        try
        {
            if (File.Exists(ConfigPath))
            {
                var json = File.ReadAllText(ConfigPath);
                return JsonSerializer.Deserialize<SoundboardConfig>(json) ?? new SoundboardConfig();
            }
        }
        catch
        {
            // Corrupt or unreadable config: fall back to defaults rather than crash.
        }

        return new SoundboardConfig();
    }

    public void Save()
    {
        try
        {
            File.WriteAllText(ConfigPath, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch
        {
            // Best-effort persistence; ignore write failures.
        }
    }
}
