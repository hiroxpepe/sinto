// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;

namespace Sinto.Core.Effects;

public sealed class RetroFilter : IEffect
{
    public Sinto.Core.Synth.RetroMode Mode    { get; set; }
    public bool                        Enabled { get; set; }

    // DOWNSAMPLING DESIGN: use Sample & Hold, NOT buffer shrinking.
    // Bad:  write 256 valid samples → 768 remaining slots contain previous frame's data → NOISE.
    // Good: hold each sample for N slots (N = 44100/targetRate).
    //   N64 mode: N=2 (22050Hz) → each sample repeated 2 times → buffer stays 44100Hz length.
    //   PS1 mode: N=4 (11025Hz) → each sample repeated 4 times → buffer stays 44100Hz length.
    // This preserves the host-requested buffer length while producing the lo-fi aesthetic.
    public void Process(Span<float> buffer, int channels)
        => throw new NotImplementedException();

    public void Reset() => throw new NotImplementedException();

    private void ProcessN64(Span<float> buffer)
        => throw new NotImplementedException();

    private void ProcessPS1(Span<float> buffer)
        => throw new NotImplementedException();

    private static float AdpcmWaveshape(float x)
        => MathF.Round(x * 16f) / 16f * 0.7f + x * 0.3f;
}
