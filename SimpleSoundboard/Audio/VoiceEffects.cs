using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace SimpleSoundboard.Audio;

/// <summary>Builds the voice-changer effect chain over a source sample provider.</summary>
public static class VoiceEffects
{
    public static ISampleProvider BuildChain(ISampleProvider source, VoiceEffectSettings settings)
    {
        ISampleProvider p = new DistortionSampleProvider(source, settings);
        p = new PitchSampleProvider(p, settings);
        p = new RingModSampleProvider(p, settings);
        p = new EchoSampleProvider(p, settings);
        return p;
    }
}

/// <summary>Pitch shift via NAudio's SMB pitch shifter; factor 1.0 = bypass.</summary>
public sealed class PitchSampleProvider : ISampleProvider
{
    private readonly SmbPitchShiftingSampleProvider _shifter;
    private readonly VoiceEffectSettings _settings;

    public PitchSampleProvider(ISampleProvider source, VoiceEffectSettings settings)
    {
        _settings = settings;
        _shifter = new SmbPitchShiftingSampleProvider(source);
    }

    public WaveFormat WaveFormat => _shifter.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        _shifter.PitchFactor = _settings.PitchEnabled
            ? Math.Clamp(_settings.Pitch, 0.5f, 2.0f)
            : 1.0f;
        return _shifter.Read(buffer, offset, count);
    }
}

/// <summary>Ring modulation — multiplies the signal by a sine carrier for a robotic tone.</summary>
public sealed class RingModSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly VoiceEffectSettings _settings;
    private double _phase;

    public RingModSampleProvider(ISampleProvider source, VoiceEffectSettings settings)
    {
        _source = source;
        _settings = settings;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        if (!_settings.RobotEnabled)
        {
            return read;
        }

        int channels = WaveFormat.Channels;
        double increment = 2 * Math.PI * _settings.RobotFrequency / WaveFormat.SampleRate;
        for (int i = 0; i < read; i += channels)
        {
            float carrier = (float)Math.Sin(_phase);
            _phase += increment;
            if (_phase > 2 * Math.PI)
            {
                _phase -= 2 * Math.PI;
            }

            for (int c = 0; c < channels && i + c < read; c++)
            {
                buffer[offset + i + c] *= carrier;
            }
        }

        return read;
    }
}

/// <summary>Feedback delay / echo.</summary>
public sealed class EchoSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly VoiceEffectSettings _settings;
    private readonly float[] _delay;
    private readonly int _channels;
    private int _pos;

    public EchoSampleProvider(ISampleProvider source, VoiceEffectSettings settings)
    {
        _source = source;
        _settings = settings;
        _channels = source.WaveFormat.Channels;
        _delay = new float[source.WaveFormat.SampleRate * _channels]; // up to 1s
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        if (!_settings.EchoEnabled)
        {
            return read;
        }

        int delayFrames = Math.Clamp(
            (int)(_settings.EchoDelayMs / 1000f * WaveFormat.SampleRate),
            1, _delay.Length / _channels - 1);
        int delaySamples = delayFrames * _channels;
        float feedback = Math.Clamp(_settings.EchoFeedback, 0f, 0.95f);
        float mix = Math.Clamp(_settings.EchoMix, 0f, 1f);

        for (int i = 0; i < read; i++)
        {
            float dry = buffer[offset + i];
            int readPos = _pos - delaySamples;
            if (readPos < 0)
            {
                readPos += _delay.Length;
            }

            float echoed = _delay[readPos];
            _delay[_pos] = dry + feedback * echoed;
            _pos++;
            if (_pos >= _delay.Length)
            {
                _pos = 0;
            }

            buffer[offset + i] = dry + mix * echoed;
        }

        return read;
    }
}

/// <summary>Soft-clip distortion / overdrive (tanh waveshaper).</summary>
public sealed class DistortionSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private readonly VoiceEffectSettings _settings;

    public DistortionSampleProvider(ISampleProvider source, VoiceEffectSettings settings)
    {
        _source = source;
        _settings = settings;
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);
        if (!_settings.DistortionEnabled)
        {
            return read;
        }

        float drive = 1f + Math.Clamp(_settings.DistortionDrive, 0f, 1f) * 24f;
        for (int i = 0; i < read; i++)
        {
            buffer[offset + i] = (float)Math.Tanh(buffer[offset + i] * drive);
        }

        return read;
    }
}
