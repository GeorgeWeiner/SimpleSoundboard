using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using SimpleSoundboard.Audio;

namespace SimpleSoundboard.Models;

/// <summary>One button on the soundboard: a display name and the file it plays.</summary>
public sealed class SoundClip : INotifyPropertyChanged
{
    private string _name = "";
    private bool _isFavorite;
    private bool _isLooping;

    /// <summary>Display name shown on the button (editable via rename).</summary>
    public string Name
    {
        get => _name;
        set => Set(ref _name, value);
    }

    public string FilePath { get; set; } = "";

    /// <summary>Category this sound belongs to. Empty = uncategorized (shown under "All").</summary>
    public string Category { get; set; } = "";

    /// <summary>Pinned favorites sort to the front of the board.</summary>
    public bool IsFavorite
    {
        get => _isFavorite;
        set => Set(ref _isFavorite, value);
    }

    /// <summary>When set, the sound repeats until stopped.</summary>
    public bool IsLooping
    {
        get => _isLooping;
        set => Set(ref _isLooping, value);
    }

    /// <summary>
    /// For bundled example sounds: the file name under Assets/Sounds. When set,
    /// the clip resolves its path at runtime (relative to the app), so it stays
    /// valid no matter where the app is installed. Null for user-added sounds.
    /// </summary>
    public string? BuiltInFile { get; set; }

    /// <summary>The actual file to play — a bundled default or a user file.</summary>
    [JsonIgnore]
    public string EffectivePath => string.IsNullOrEmpty(BuiltInFile)
        ? FilePath
        : Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds", BuiltInFile);

    /// <summary>Decoded audio, loaded lazily on first play and then reused.</summary>
    [JsonIgnore]
    public CachedSound? Cached { get; set; }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (!EqualityComparer<T>.Default.Equals(field, value))
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
