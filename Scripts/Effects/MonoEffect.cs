// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Runtime.CompilerServices;

namespace Signo.Core.Effects;

/// <summary>Base for stereo effects that can be reduced to mono.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public abstract class MonoEffect : IEffect {
    public bool monoCompatible { get; set; }
    public bool enabled        { get; set; }
    public abstract void Process(Span<float> buffer, int channels);
    public abstract void Reset();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void ApplyMonoCompatibility(Span<float> buffer, int channels) {
        if (!monoCompatible || channels < 2) return;
        // Average L/R into both channels
        int frames = buffer.Length / channels;
        for (int f = 0; f < frames; f++) {
            int i = f * channels;
            float mix = (buffer[i] + buffer[i + 1]) * 0.5f;
            buffer[i]     = mix;
            buffer[i + 1] = mix;
        }
    }
}
