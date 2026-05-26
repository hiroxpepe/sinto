// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System.Runtime.InteropServices;

namespace Sinto.Core.Synth;

[StructLayout(LayoutKind.Sequential)]
public readonly struct LFOParams {
    public readonly LFOWave        Wave;
    // Rate and SyncNoteValue are mutually exclusive (TempoSync flag selects one).
    // Merged into a single float to eliminate redundant memory footprint across 32 voices.
    // When TempoSync=false: RateOrSync = frequency in Hz [0.01, 20.0]
    // When TempoSync=true:  RateOrSync = note value (e.g. 0.25 = 1/4 note)
    public readonly float          RateOrSync;
    public readonly float          Depth;
    public readonly bool           TempoSync;
    public readonly LFODestination Destinations;

    public LFOParams(LFOWave wave, float rateOrSync = 1.0f, float depth = 0.5f,
        bool tempoSync = false,
        LFODestination destinations = LFODestination.FilterCutoff)
        => throw new System.NotImplementedException();
}
