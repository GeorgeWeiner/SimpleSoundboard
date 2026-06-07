using NAudio.Wave;

namespace SimpleSoundboard.Audio;

/// <summary>
/// Reads from an already-decoded <see cref="CachedSound"/>. A fresh instance
/// is created per playback (and per output device) so the same sound can be
/// triggered repeatedly and overlap with itself. When <c>loop</c> is set it
/// wraps back to the start instead of ending.
/// </summary>
public sealed class CachedSoundSampleProvider : ISampleProvider
{
    private readonly CachedSound _sound;
    private readonly bool _loop;
    private long _position;

    public CachedSoundSampleProvider(CachedSound sound, bool loop = false)
    {
        _sound = sound;
        _loop = loop;
    }

    public WaveFormat WaveFormat => _sound.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        var source = _sound.AudioData;
        if (source.Length == 0)
        {
            return 0;
        }

        // NOTE: do NOT use Array.Copy here. NAudio hands us the float[] view of a
        // WaveBuffer, whose underlying array object is actually a byte[] (union via
        // [FieldOffset(0)]). Array.Copy checks the real array type and throws
        // ArrayTypeMismatchException (float[] -> byte[]). Element-wise stores are
        // exactly how WaveBuffer is meant to be written, so copy by hand.
        int filled = 0;
        while (filled < count)
        {
            long available = source.Length - _position;
            if (available <= 0)
            {
                if (!_loop)
                {
                    break;
                }

                _position = 0; // wrap to the start
                available = source.Length;
            }

            int chunk = (int)Math.Min(count - filled, available);
            for (int i = 0; i < chunk; i++)
            {
                buffer[offset + filled + i] = source[_position + i];
            }

            _position += chunk;
            filled += chunk;
        }

        return filled;
    }
}
