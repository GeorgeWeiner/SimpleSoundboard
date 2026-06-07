using System.ComponentModel;
using System.Diagnostics;
using NAudio.Wave;

namespace SimpleSoundboard.Audio;

/// <summary>
/// One triggered sound that is currently playing. A single trigger may feed
/// several output devices (monitor + VB-Cable); this represents them as one
/// logical item with a name, a duration, and a live position for the UI.
/// </summary>
public sealed class Playback : INotifyPropertyChanged
{
    private readonly List<WasapiOut> _outputs = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly object _lock = new();
    private int _remaining;
    private bool _finished;

    public string Name { get; }
    public TimeSpan Duration { get; }

    /// <summary>Raised (off the UI thread) once every output has stopped.</summary>
    public event Action<Playback>? Finished;
    public event PropertyChangedEventHandler? PropertyChanged;

    internal Playback(string name, TimeSpan duration)
    {
        Name = name;
        Duration = duration;
    }

    /// <summary>Elapsed wall-clock position, capped at the clip's duration.</summary>
    public TimeSpan Position
    {
        get
        {
            if (_finished)
            {
                return Duration;
            }

            var elapsed = _clock.Elapsed;
            return elapsed < Duration ? elapsed : Duration;
        }
    }

    public double Progress => Duration.TotalSeconds <= 0
        ? 0
        : Math.Clamp(Position.TotalSeconds / Duration.TotalSeconds, 0, 1);

    public string PositionText => $"{Format(Position)} / {Format(Duration)}";

    private static string Format(TimeSpan t) => $"{(int)t.TotalMinutes}:{t.Seconds:00}";

    /// <summary>Called by a UI timer to refresh the bound progress/time.</summary>
    public void Tick()
    {
        OnChanged(nameof(Position));
        OnChanged(nameof(Progress));
        OnChanged(nameof(PositionText));
    }

    internal void Add(WasapiOut output)
    {
        lock (_lock)
        {
            _outputs.Add(output);
            _remaining++;
        }

        output.PlaybackStopped += (_, _) => OnOutputStopped(output);
    }

    /// <summary>Stops just this sound (all its device outputs).</summary>
    public void Stop()
    {
        WasapiOut[] snapshot;
        lock (_lock)
        {
            snapshot = _outputs.ToArray();
        }

        foreach (var output in snapshot)
        {
            output.Stop(); // raises PlaybackStopped -> OnOutputStopped
        }
    }

    private void OnOutputStopped(WasapiOut output)
    {
        output.Dispose();

        bool done = false;
        lock (_lock)
        {
            if (--_remaining <= 0 && !_finished)
            {
                _finished = true;
                done = true;
            }
        }

        if (done)
        {
            _clock.Stop();
            Finished?.Invoke(this);
        }
    }

    private void OnChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
