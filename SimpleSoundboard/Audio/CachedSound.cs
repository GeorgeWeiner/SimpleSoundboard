using Concentus.Oggfile;
using Concentus.Structs;
using NAudio.Vorbis;
using NAudio.Wave;

namespace SimpleSoundboard.Audio;

/// <summary>
/// Decodes an audio file fully into memory once, so it can be replayed
/// instantly and fed to several output devices at the same time.
/// Supports .wav, .mp3 (NAudio), Ogg Vorbis (NAudio.Vorbis) and Ogg Opus
/// (Concentus) — Opus is common for Discord/exported .ogg files.
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

    private static readonly byte[] OpusHeadMarker = "OpusHead"u8.ToArray();

    private static (ISampleProvider, IDisposable) CreateReader(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        if (ext == ".ogg")
        {
            // .ogg may carry Vorbis or Opus — pick the decoder by what's inside.
            if (IsOpus(filePath))
            {
                return CreateOpusReader(filePath);
            }

            var vorbis = new VorbisWaveReader(filePath);
            return (vorbis.ToSampleProvider(), vorbis);
        }

        // AudioFileReader handles wav and mp3 and is itself a sample provider.
        var reader = new AudioFileReader(filePath);
        return (reader, reader);
    }

    /// <summary>Decodes an Ogg Opus file fully into a float sample provider (48 kHz).</summary>
    private static (ISampleProvider, IDisposable) CreateOpusReader(string filePath)
    {
        int channels = ReadOpusChannels(filePath);
        using var stream = File.OpenRead(filePath);
        var decoder = OpusDecoder.Create(48000, channels);
        var ogg = new OpusOggReadStream(decoder, stream);

        var samples = new List<float>();
        while (ogg.HasNextPacket)
        {
            short[] packet = ogg.DecodeNextPacket();
            if (packet is null)
            {
                continue;
            }

            foreach (var sample in packet)
            {
                samples.Add(sample / 32768f);
            }
        }

        var provider = new MemorySampleProvider(samples.ToArray(), 48000, channels);
        return (provider, provider);
    }

    private static bool IsOpus(string filePath)
    {
        var header = ReadHeader(filePath);
        return IndexOf(header, OpusHeadMarker) >= 0;
    }

    private static int ReadOpusChannels(string filePath)
    {
        var header = ReadHeader(filePath);
        int idx = IndexOf(header, OpusHeadMarker);
        // OpusHead layout: "OpusHead"(8) + version(1) + channelCount(1)
        int channelIndex = idx + OpusHeadMarker.Length + 1;
        return idx >= 0 && channelIndex < header.Length ? header[channelIndex] : 2;
    }

    private static byte[] ReadHeader(string filePath)
    {
        var header = new byte[256];
        using var stream = File.OpenRead(filePath);
        int read = stream.Read(header, 0, header.Length);
        return read == header.Length ? header : header[..read];
    }

    private static int IndexOf(byte[] haystack, byte[] needle)
    {
        for (int i = 0; i <= haystack.Length - needle.Length; i++)
        {
            bool match = true;
            for (int j = 0; j < needle.Length; j++)
            {
                if (haystack[i + j] != needle[j])
                {
                    match = false;
                    break;
                }
            }

            if (match)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>An in-memory float sample provider (used for decoded Opus audio).</summary>
    private sealed class MemorySampleProvider : ISampleProvider, IDisposable
    {
        private readonly float[] _data;
        private int _position;

        public MemorySampleProvider(float[] data, int sampleRate, int channels)
        {
            _data = data;
            WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, channels);
        }

        public WaveFormat WaveFormat { get; }

        public int Read(float[] buffer, int offset, int count)
        {
            int n = Math.Min(count, _data.Length - _position);
            if (n <= 0)
            {
                return 0;
            }

            Array.Copy(_data, _position, buffer, offset, n);
            _position += n;
            return n;
        }

        public void Dispose()
        {
        }
    }
}
