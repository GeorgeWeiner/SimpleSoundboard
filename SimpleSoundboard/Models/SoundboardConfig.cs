using System.Text.Json;

namespace SimpleSoundboard.Models;

/// <summary>Persisted soundboard state: the list of sounds plus user settings.</summary>
public sealed class SoundboardConfig
{
    public List<SoundClip> Sounds { get; set; } = new();

    /// <summary>Selected colour theme name (see Theming/Theme.cs).</summary>
    public string Theme { get; set; } = "Deadlock";

    /// <summary>User-defined categories, in tab order.</summary>
    public List<string> Categories { get; set; } = new();

    /// <summary>Last selected category tab, or null for "All".</summary>
    public string? SelectedCategory { get; set; }

    public double Volume { get; set; } = 80;

    /// <summary>Play to the local monitor device (so you hear it too).</summary>
    public bool RouteToMonitor { get; set; } = true;

    /// <summary>
    /// WASAPI endpoint id of the chosen monitor device, or null to follow the
    /// system default. Persisted so the picker remembers your headphones.
    /// </summary>
    public string? MonitorDeviceId { get; set; }

    public bool RouteToVbCable { get; set; } = true;

    /// <summary>Pipe the live microphone into VB-Cable alongside the sounds.</summary>
    public bool RouteMic { get; set; }

    /// <summary>WASAPI id of the chosen mic, or null to follow the system default.</summary>
    public string? MicDeviceId { get; set; }

    /// <summary>Voice-changer effect settings.</summary>
    public Audio.VoiceEffectSettings VoiceEffects { get; set; } = new();

    /// <summary>
    /// File names of bundled Assets/Sounds examples that have already been added
    /// to the board. Each default is seeded once; if the user removes it, it
    /// won't come back, and newly-shipped examples get added on next launch.
    /// </summary>
    public List<string> SeededDefaults { get; set; } = new();

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
