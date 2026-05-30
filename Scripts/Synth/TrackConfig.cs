// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Signo.Core.Synth;

/// <summary>Per-track voice reservation and stealing policy.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
[StructLayout(LayoutKind.Sequential)]
public readonly struct TrackConfig {
    public readonly int  ReservedVoices;
    public readonly int  Priority;
    public readonly bool Protected;

    public TrackConfig(int reservedVoices, int priority, bool isProtected) {
        ReservedVoices = reservedVoices;
        Priority       = priority;
        Protected      = isProtected;
    }

    // For audio-thread lookups, use GetConfig(trackId) — O(1) switch, no heap access.
    // IReadOnlyList is kept for iteration (e.g. serialization) but not for hot-path use.
    public static readonly System.Collections.Generic.IReadOnlyList<TrackConfig> DefaultConfigs =
        System.Array.AsReadOnly(new[] {
            new TrackConfig(2, 10, true),   // Track 0: Drum
            new TrackConfig(2, 10, true),   // Track 1: Percussion
            new TrackConfig(2, 8,  false),  // Track 2: Bass
            new TrackConfig(4, 5,  false),  // Track 3: Pad
            new TrackConfig(2, 7,  false),  // Track 4: Obligato 1
            new TrackConfig(2, 7,  false),  // Track 5: Obligato 2
            new TrackConfig(4, 6,  false),  // Track 6: Melody 1
            new TrackConfig(4, 6,  false),  // Track 7: Melody 2
        });

    /// <summary>
    /// O(1) lookup for audio hot path. No heap allocation. No array bounds check.
    /// TrackId is clamped to [0,7] — invalid values return a safe default.
    /// </summary>
    public static TrackConfig GetConfig(int trackId) => trackId switch {
        0 => new TrackConfig(2, 10, true),   // Drum      — Protected
        1 => new TrackConfig(2, 10, true),   // Perc      — Protected
        2 => new TrackConfig(2, 8,  false),  // Bass
        3 => new TrackConfig(4, 5,  false),  // Pad
        4 => new TrackConfig(2, 7,  false),  // Obligato1
        5 => new TrackConfig(2, 7,  false),  // Obligato2
        6 => new TrackConfig(4, 6,  false),  // Melody1
        7 => new TrackConfig(4, 6,  false),  // Melody2
        _ => new TrackConfig(0, 1,  false),  // Invalid trackId — safe default, lowest priority
    };
}
