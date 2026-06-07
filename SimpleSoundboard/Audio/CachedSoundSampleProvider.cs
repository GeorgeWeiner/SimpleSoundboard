using NAudio.Wave;

namespace SimpleSoundboard.Audio;

/// <summary>
/// Reads from an already-decoded <see cref="CachedSound"/>. A fresh instance
/// is created per playback (and per output device) so the same sound can be
/// triggered repeatedly and overlap with itself.
/// </summary>
public sealed class CachedSoundSampleProvider : ISampleProvider
{
    private readonly CachedSound _sound;
    private long _position;

    public CachedSoundSampleProvider(CachedSound sound)
    {
        _sound = sound;
    }

    public WaveFormat WaveFormat => _sound.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        long available = _sound.AudioData.Length - _position;
        int samplesToCopy = (int)Math.Min(available, count);
        if (samplesToCopy <= 0)
        {
            return 0;
        }

        // NOTE: do NOT use Array.Copy here. NAudio hands us the float[] view of a
        // WaveBuffer, whose underlying array object is actually a byte[] (union via
        // [FieldOffset(0)]). Array.Copy checks the real array type and throws
        // ArrayTypeMismatchException (float[] -> byte[]). Element-wise stores are
        // exactly how WaveBuffer is meant to be written, so copy by hand.
        var source = _sound.AudioData;
        long position = _position;
        for (int i = 0; i < samplesToCopy; i++)
        {
            buffer[offset + i] = source[position + i];
        }

        _position += samplesToCopy;
        return samplesToCopy;
    }
}
