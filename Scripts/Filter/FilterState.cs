// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System.Runtime.CompilerServices;

namespace Sinto.Core.Filter;

public enum FilterMode : byte { Roland = 0, Moog = 1 }

public struct FilterState {
    private float _s1, _s2, _s3, _s4;
    private float _k;
    private float _resonance;
    private FilterMode _mode;

    // Resonance scaling: user input is [0.0, 1.0].
    // Moog ladder self-oscillates near internal value 4.0.
    // MUST scale: internalResonance = MathF.Min(MathF.Max(resonance * 4f, 0f), 3.99f)
    // Without * 4f: max internal resonance = 1.0 → filter NEVER self-oscillates.
    public void SetParams(float cutoff, float resonance,
        FilterMode mode, int sampleRate)
        => throw new System.NotImplementedException();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Process(float input, long sampleIndex)
        => throw new System.NotImplementedException();

    public void Reset()
        => throw new System.NotImplementedException();
}
