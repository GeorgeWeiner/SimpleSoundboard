using System.Text.Json.Serialization;
using SimpleSoundboard.Audio;

namespace SimpleSoundboard.Models;

/// <summary>One button on the soundboard: a display name and the file it plays.</summary>
public sealed class SoundClip
{
    public string Name { get; set; } = "";
    public string FilePath { get; set; } = "";

    /// <summary>Decoded audio, loaded lazily on first play and then reused.</summary>
    [JsonIgnore]
    public CachedSound? Cached { get; set; }
}
