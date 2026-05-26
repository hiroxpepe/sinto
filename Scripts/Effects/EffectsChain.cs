#nullable enable
using System;

namespace Sinto.Core.Effects;

public sealed class EffectsChain
{
    public BBDChorus     Chorus  { get; }
    public FreeverbReverb Reverb { get; }
    public StereoDelay   Delay   { get; }
    public RetroFilter   Retro   { get; }
    public bool MonoCompatible { get; set; }

    public EffectsChain(int sampleRate = 44100) {
        Chorus = new BBDChorus(sampleRate);
        Reverb = new FreeverbReverb();
        Delay  = new StereoDelay(sampleRate);
        Retro  = new RetroFilter();
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
