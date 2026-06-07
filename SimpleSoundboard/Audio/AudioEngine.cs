using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using SimpleSoundboard.Diagnostics;

namespace SimpleSoundboard.Audio;

/// <summary>One selectable WASAPI render endpoint.</summary>
public sealed class OutputDevice
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public bool IsDefault { get; init; }

    /// <summary>The live endpoint we actually play to. Runtime-only.</summary>
    public required MMDevice Device { get; init; }
}

/// <summary>
/// Plays cached sounds to one or more output devices simultaneously, which is
/// how a sound lands in both VB-Cable (for other apps) and your speakers.
/// Uses WASAPI; WasapiOut resamples internally if a clip's format doesn't
/// match the device, so any wav/mp3/ogg just plays.
/// </summary>
public sealed class AudioEngine : IDisposable
{
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly List<IWavePlayer> _active = new();
    private readonly object _lock = new();

    /// <summary>Raised when a new sound starts (on the calling thread).</summary>
    public event Action<Playback>? PlaybackStarted;

    /// <summary>The current Windows default playback device (speakers/headphones).</summary>
    public OutputDevice GetDefaultDevice()
    {
        var device = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        return new OutputDevice
        {
            Id = device.ID,
            Name = $"Default ({device.FriendlyName})",
            IsDefault = true,
            Device = device
        };
    }

    /// <summary>Enumerates all active render endpoints.</summary>
    public List<OutputDevice> GetOutputDevices()
    {
        var devices = new List<OutputDevice>();
        foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            devices.Add(new OutputDevice
            {
                Id = device.ID,
                Name = device.FriendlyName,
                Device = device
            });
        }

        return devices;
    }

    /// <summary>
    /// The standard VB-CABLE input endpoint, or null if it isn't installed.
    /// VB-CABLE exposes its capture side ("CABLE Output") to other apps; we play
    /// into its render side, "CABLE Input". We deliberately prefer that exact
    /// device over VB-Audio's other products (e.g. "CABLE In 16ch", Voicemeeter),
    /// which are different cables other apps usually aren't listening to.
    /// </summary>
    public static OutputDevice? FindVbCable(IEnumerable<OutputDevice> devices)
    {
        var list = devices.ToList();
        return list.FirstOrDefault(d => d.Name.StartsWith("CABLE Input", StringComparison.OrdinalIgnoreCase))
            ?? list.FirstOrDefault(d => d.Name.Contains("CABLE Input", StringComparison.OrdinalIgnoreCase))
            ?? list.FirstOrDefault(d => d.Name.Contains("VB-Audio", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Plays <paramref name="sound"/> to each device at the given volume (0..1).
    /// Returns a <see cref="Playback"/> handle representing this one trigger so
    /// callers can show progress and stop it individually.
    /// </summary>
    public Playback Play(CachedSound sound, IEnumerable<OutputDevice> devices, float volume, string name = "")
    {
        var playback = new Playback(name, sound.Duration);

        foreach (var device in devices.GroupBy(d => d.Id).Select(g => g.First()))
        {
            var output = new WasapiOut(device.Device, AudioClientShareMode.Shared, true, 100);
            var source = new CachedSoundSampleProvider(sound);
            var volumed = new VolumeSampleProvider(source) { Volume = volume };
            output.Init(volumed.ToWaveProvider());

            Log.Write($"  -> device='{device.Name}' vol={volume:0.00} " +
                      $"srcFmt={sound.WaveFormat.Encoding} {sound.WaveFormat.SampleRate}Hz " +
                      $"{sound.WaveFormat.Channels}ch samples={sound.AudioData.Length}");

            // Engine-level tracking for StopAll. The Playback owns disposal.
            output.PlaybackStopped += (_, e) =>
            {
                Log.Write(e.Exception is null
                    ? $"  playback completed: '{device.Name}'"
                    : $"  PLAYBACK ERROR on '{device.Name}': {e.Exception.GetType().Name}: {e.Exception.Message}");

                lock (_lock)
                {
                    _active.Remove(output);
                }
            };

            lock (_lock)
            {
                _active.Add(output);
            }

            playback.Add(output);
            output.Play();
            Log.Write($"  Play() called, state={output.PlaybackState} on '{device.Name}'");
        }

        PlaybackStarted?.Invoke(playback);
        return playback;
    }

    /// <summary>Stops every currently-playing sound.</summary>
    public void StopAll()
    {
        IWavePlayer[] snapshot;
        lock (_lock)
        {
            snapshot = _active.ToArray();
        }

        foreach (var output in snapshot)
        {
            output.Stop(); // raises PlaybackStopped, which disposes + removes it
        }
    }

    public void Dispose()
    {
        StopAll();
        _enumerator.Dispose();
    }
}
