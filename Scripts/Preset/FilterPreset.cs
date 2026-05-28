// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using Sinto.Core.Synth;

namespace Sinto.Core.Preset;

/// <summary>Serializable filter preset for .sinto JSON.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public sealed class FilterPreset
{
    public FilterKind mode      { get; init; } = FilterKind.Roland;
    public float      cutoff    { get; init; } = 1.0f;   // [0.001, 0.999]
    public float      resonance { get; init; } = 0.0f;   // [0.0, 1.0]
    public float      env_amt    { get; init; } = 0.0f;   // [-1.0, +1.0]
    public float      key_follow { get; init; } = 0.0f;   // [0.0, 1.0]

    public static readonly FilterPreset Default = new();
}
