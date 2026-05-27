// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System.Runtime.InteropServices;

namespace Sinto.Core.Synth;

/// <summary>LFO parameters. Immutable. RateOrSync field merged for memory efficiency.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
[StructLayout(LayoutKind.Sequential)]
public readonly struct LfoParams {
#nullable enable
    public readonly LfoWave   Wave;
    public readonly float     RateOrSync;
    public readonly float     Depth;
    public readonly bool      TempoSync;
    public readonly LfoTarget Destinations;

    public LfoParams(LfoWave wave, float rateOrSync = 1.0f, float depth = 0.5f,
        bool tempoSync = false,
        LfoTarget destinations = LfoTarget.FilterCutoff) {
        // Clamp Depth to [0, 1]
        if      (depth < 0f) depth = 0f;
        else if (depth > 1f) depth = 1f;
        Wave         = wave;
        RateOrSync   = rateOrSync;
        Depth        = depth;
        TempoSync    = tempoSync;
        Destinations = destinations;
    }
}
