using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SimpleSoundboard.Audio;

/// <summary>
/// Continuously captures a microphone and plays it to an output device
/// (VB-Cable), so other apps listening on "CABLE Output" hear the live mic
/// mixed with soundboard sounds. WASAPI shared mode mixes both render streams
/// at the cable, so this runs alongside the sound playback without conflict.
/// </summary>
public sealed class MicPassthrough : IDisposable
{
    private WasapiCapture? _capture;
    private WasapiOut? _output;

    public bool IsActive => _capture is not null;

    /// <summary>Starts piping <paramref name="input"/> into <paramref name="output"/>.</summary>
    public void Start(MMDevice input, MMDevice output)
    {
        Stop();

        var capture = new WasapiCapture(input);
        var buffer = new BufferedWaveProvider(capture.WaveFormat)
        {
            DiscardOnBufferOverflow = true,
            BufferDuration = TimeSpan.FromSeconds(2)
        };
        capture.DataAvailable += (_, e) => buffer.AddSamples(e.Buffer, 0, e.BytesRecorded);

        // Match the output's channel count (a mono mic into a stereo cable).
        ISampleProvider source = buffer.ToSampleProvider();
        var mixFormat = output.AudioClient.MixFormat;
        if (source.WaveFormat.Channels == 1 && mixFormat.Channels >= 2)
        {
            source = new MonoToStereoSampleProvider(source);
        }

        var render = new WasapiOut(output, AudioClientShareMode.Shared, true, 100);
        render.Init(source.ToWaveProvider());

        capture.StartRecording();
        render.Play();

        _capture = capture;
        _output = render;
    }

    public void Stop()
    {
        if (_capture is not null)
        {
            try { _capture.StopRecording(); } catch { /* ignore */ }
            _capture.Dispose();
            _capture = null;
        }

        if (_output is not null)
        {
            try { _output.Stop(); } catch { /* ignore */ }
            _output.Dispose();
            _output = null;
        }
    }

    public void Dispose() => Stop();
}
