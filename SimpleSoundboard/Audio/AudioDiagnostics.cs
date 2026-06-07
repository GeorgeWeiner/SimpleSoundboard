using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SimpleSoundboard.Audio;

/// <summary>
/// Headless audio self-test. Run with: dotnet run -- --audiotest
/// Plays a 440 Hz tone to the default device three different ways and reports
/// whether playback errored, so we can tell which WASAPI path is silent.
/// </summary>
public static class AudioDiagnostics
{
    public static void Run()
    {
        using var enumerator = new MMDeviceEnumerator();

        Console.WriteLine("== Render endpoints ==");
        foreach (var d in enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            Console.WriteLine($"  {d.FriendlyName}  | state={d.State} | mix={Describe(d.AudioClient.MixFormat)}");
        }

        var def = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
        Console.WriteLine($"\nDefault: {def.FriendlyName}");
        var mix = def.AudioClient.MixFormat;
        Console.WriteLine($"Mix format: {Describe(mix)}\n");

        TryPlay("A) raw 44.1k stereo float, WASAPI auto-resample, eventSync=true",
            def, useEventSync: true,
            () => new SignalGenerator(44100, 2) { Gain = 0.25, Frequency = 440, Type = SignalGeneratorType.Sin });

        TryPlay("B) raw 44.1k stereo float, WASAPI auto-resample, eventSync=false",
            def, useEventSync: false,
            () => new SignalGenerator(44100, 2) { Gain = 0.25, Frequency = 440, Type = SignalGeneratorType.Sin });

        TryPlay("C) pre-matched to mix format, eventSync=true",
            def, useEventSync: true,
            () =>
            {
                ISampleProvider src = new SignalGenerator(mix.SampleRate, mix.Channels)
                    { Gain = 0.25, Frequency = 440, Type = SignalGeneratorType.Sin };
                return src;
            });

        // D) Exercise the REAL app path: write a WAV, decode via CachedSound,
        //    play through AudioEngine to default device + VB-Cable.
        Console.WriteLine("-- D) full engine path: CachedSound -> AudioEngine.Play");
        var wavPath = Path.Combine(Path.GetTempPath(), "soundboard_selftest.wav");
        WriteSineWav(wavPath, seconds: 1.5, freq: 440);
        var cached = new CachedSound(wavPath);
        Console.WriteLine($"   decoded: {Describe(cached.WaveFormat)}, {cached.AudioData.Length} samples " +
                          $"({cached.AudioData.Length / (double)(cached.WaveFormat.SampleRate * cached.WaveFormat.Channels):0.00}s)");
        Console.WriteLine($"   peak amplitude: {cached.AudioData.Select(Math.Abs).DefaultIfEmpty(0).Max():0.000}");

        using var engine = new AudioEngine();
        var targets = new List<OutputDevice> { engine.GetDefaultDevice() };
        var vb = AudioEngine.FindVbCable(engine.GetOutputDevices());
        Console.WriteLine($"   default target: {targets[0].Name}");
        Console.WriteLine($"   vb-cable target: {(vb is null ? "NOT FOUND" : vb.Name)}");
        if (vb is not null)
        {
            targets.Add(vb);
        }

        engine.Play(cached, targets, volume: 0.8f);
        Console.WriteLine("   playing to both for 2s...");
        Thread.Sleep(2000);
        engine.StopAll();

        Console.WriteLine("\nDone.");
    }

    private static void TryPlay(string label, MMDevice device, bool useEventSync, Func<ISampleProvider> makeSource)
    {
        Console.WriteLine($"-- {label}");
        Exception? error = null;
        var stopped = new ManualResetEventSlim();
        WasapiOut? output = null;
        try
        {
            output = new WasapiOut(device, AudioClientShareMode.Shared, useEventSync, 100);
            var source = makeSource();
            Console.WriteLine($"   source format: {Describe(source.WaveFormat)}");
            output.Init(source.ToWaveProvider());
            output.PlaybackStopped += (_, e) =>
            {
                error = e.Exception;
                stopped.Set();
            };
            output.Play();
            Console.WriteLine($"   PlaybackState after Play(): {output.PlaybackState}");
            Thread.Sleep(1500);
            output.Stop();
            stopped.Wait(1000);
            Console.WriteLine(error is null
                ? "   result: OK (no exception)"
                : $"   result: ERROR {error.GetType().Name}: {error.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   THREW {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            output?.Dispose();
        }

        Console.WriteLine();
    }

    /// <summary>Reproduce the ArrayTypeMismatch by decoding a real file and reading it.</summary>
    public static void Repro(string path)
    {
        Console.WriteLine($"Repro on: {path}");
        var cached = new CachedSound(path);
        Console.WriteLine($"  WaveFormat: {Describe(cached.WaveFormat)}");
        Console.WriteLine($"  AudioData runtime type: {cached.AudioData.GetType()}");
        Console.WriteLine($"  AudioData.Length: {cached.AudioData.Length}");

        var provider = new CachedSoundSampleProvider(cached);
        var buffer = new float[480];
        Console.WriteLine($"  buffer runtime type: {buffer.GetType()}");
        try
        {
            int n = provider.Read(buffer, 0, buffer.Length);
            Console.WriteLine($"  Read OK, returned {n} samples, buffer[0..3]={buffer[0]:0.000},{buffer[1]:0.000},{buffer[2]:0.000}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  Read THREW {ex.GetType().Name}: {ex.Message}");
        }

        Console.WriteLine("  playing 2s through real engine to default device...");
        using var engine = new AudioEngine();
        engine.Play(cached, new[] { engine.GetDefaultDevice() }, 0.6f);
        Thread.Sleep(2000);
        engine.StopAll();
        Console.WriteLine("  (check log.txt for completed vs PLAYBACK ERROR)");
    }

    private static void WriteSineWav(string path, double seconds, double freq)
    {
        var format = WaveFormat.CreateIeeeFloatWaveFormat(44100, 2);
        using var writer = new WaveFileWriter(path, format);
        int total = (int)(seconds * format.SampleRate);
        var buffer = new float[2];
        for (int i = 0; i < total; i++)
        {
            float sample = (float)(0.3 * Math.Sin(2 * Math.PI * freq * i / format.SampleRate));
            buffer[0] = sample;
            buffer[1] = sample;
            writer.WriteSamples(buffer, 0, 2);
        }
    }

    private static string Describe(WaveFormat f) =>
        $"{f.Encoding} {f.SampleRate}Hz {f.Channels}ch {f.BitsPerSample}bit";
}
