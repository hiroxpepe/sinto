// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using Signo.Core.Synth;

namespace Signo.Core.Preset;

/// <summary>Serializable LFO preset for .signo JSON.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public sealed class LfoPreset
{
    public LfoWave        wave         { get; init; } = LfoWave.Sine;
    public float          rate_or_sync   { get; init; } = 1.0f;  // Hz or note value
    public float          depth        { get; init; } = 0.0f;  // [0.0, 1.0]
    public bool           tempo_sync    { get; init; } = false;
    public LfoTarget destinations { get; init; } = LfoTarget.None;

    public static readonly LfoPreset Default = new();
}
