namespace SimpleSoundboard.Audio;

/// <summary>
/// Live-adjustable voice-changer parameters. The effect providers read these
/// every buffer, so changes apply in real time without restarting the mic.
/// Persisted in the soundboard config.
/// </summary>
public sealed class VoiceEffectSettings
{
    // Pitch (1.0 = unchanged, &lt;1 deeper, &gt;1 higher)
    public bool PitchEnabled { get; set; }
    public float Pitch { get; set; } = 1.0f;

    // Robot (ring modulation) — carrier frequency in Hz
    public bool RobotEnabled { get; set; }
    public float RobotFrequency { get; set; } = 60f;

    // Echo / delay
    public bool EchoEnabled { get; set; }
    public float EchoDelayMs { get; set; } = 220f;
    public float EchoFeedback { get; set; } = 0.4f;
    public float EchoMix { get; set; } = 0.5f;

    // Distortion / overdrive
    public bool DistortionEnabled { get; set; }
    public float DistortionDrive { get; set; } = 0.4f;
}
