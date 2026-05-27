// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;

namespace Sinto.Core.Effects;

public sealed class Chorus : MonoEffect
{
    // Instance buffers (NOT static — data race between BGM and SFX engines)
    private readonly float[] _delayBufL;
    private readonly float[] _delayBufR;

    public int   Mode  { get; set; } = 1;
    public float Rate  { get; set; } = 0.5f;
    public float Depth { get; set; } = 0.4f;
    public float Mix   { get; set; } = 0.5f;

    public Chorus(int sampleRate = 44100) {
        _delayBufL = new float[sampleRate]; // instance — not static
        _delayBufR = new float[sampleRate];
    }
    public override void Process(Span<float> buffer, int channels)
        => throw new NotImplementedException();
    public override void Reset()
        => throw new NotImplementedException();
}
