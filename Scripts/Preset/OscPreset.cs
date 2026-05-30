// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using Signo.Core.Synth;

namespace Signo.Core.Preset;

/// <summary>Serializable oscillator preset for .signo JSON.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public sealed class OscPreset
{
    public WaveType   wave        { get; init; } = WaveType.Sine;
    public Interpolation interp      { get; init; } = Interpolation.Linear;
    public float      detune_cents { get; init; } = 0f;    // [-100, +100]
    public float      pulse_width  { get; init; } = 0.5f;  // [0.01, 0.99]
    public float      level       { get; init; } = 1.0f;  // [0.0, 1.0]

    public static readonly OscPreset Default = new();
}
