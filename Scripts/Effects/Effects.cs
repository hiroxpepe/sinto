#nullable enable
using System;

namespace Sinto.Core.Effects;

public sealed class Effects
{
    public Chorus     Chorus  { get; }
    public Reverb Reverb { get; }
    public Delay   Delay   { get; }
    public Retro   Retro   { get; }
    public bool MonoCompatible { get; set; }

    public Effects(int sampleRate = 44100) {
        Chorus = new Chorus(sampleRate);
        Reverb = new Reverb();
        Delay  = new Delay(sampleRate);
        Retro  = new Retro();
    }
    public void Process(Span<float> buffer, int channels)
        => throw new NotImplementedException();
    public void SetBPM(float bpm)
        => throw new NotImplementedException();
    public void Reset()
        => throw new NotImplementedException();

    // Master soft clipper — applied after all effects
    // Prevents 32-voice summation from exceeding [-1, 1]
    // Uses TanhFast: maps ℝ → (-1, 1) with smooth saturation
    public void ApplySoftClip(Span<float> buffer)
        => throw new NotImplementedException();
}
