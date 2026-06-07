using NAudio.Vorbis;
using NAudio.Wave;

namespace SimpleSoundboard.Audio;

/// <summary>
/// Decodes an audio file fully into memory once, so it can be replayed
/// instantly and fed to several output devices at the same time.
/// Supports .wav, .mp3 (via NAudio) and .ogg (via NAudio.Vorbis).
/// </summary>
public sealed class CachedSound
{
    public float[] AudioData { get; }
    public WaveFormat WaveFormat { get; }

    /// <summary>Total play length, derived from the decoded sample count.</summary>
    public TimeSpan Duration => TimeSpan.FromSeconds(
        AudioData.Length / (double)(WaveFormat.SampleRate * WaveFormat.Channels));

    public CachedSound(string filePath)
    {
        var (sampleProvider, disposable) = CreateReader(filePath);
        try
        {
            WaveFormat = sampleProvider.WaveFormat;

            var data = new List<float>();
            var readBuffer = new float[WaveFormat.SampleRate * WaveFormat.Channels];
            int samplesRead;
            while ((samplesRead = sampleProvider.Read(readBuffer, 0, readBuffer.Length)) > 0)
            {
                data.AddRange(readBuffer.Take(samplesRead));
            }

            AudioData = data.ToArray();
        }
        finally
        {
            disposable.Dispose();
        }
    }

    private static (ISampleProvider, IDisposable) CreateReader(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".ogg")
        {
            var vorbis = new VorbisWaveReader(filePath);
            return (vorbis.ToSampleProvider(), vorbis);
        }

        // AudioFileReader handles wav and mp3 and is itself a sample provider.
        var reader = new AudioFileReader(filePath);
        return (reader, reader);
    }
}
