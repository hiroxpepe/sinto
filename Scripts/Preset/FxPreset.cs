// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using Sinto.Core.Synth;

namespace Sinto.Core.Preset;

/// <summary>Serializable effects preset for .sinto JSON.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public sealed class FxPreset
{
    // Chorus
    public int   chorus_mode  { get; init; } = 1;     // 1 or 2
    public float chorus_rate  { get; init; } = 0.5f;
    public float chorus_depth { get; init; } = 0.3f;
    public float chorus_mix   { get; init; } = 0.0f;

    // Reverb
    public float reverb_room_size { get; init; } = 0.5f;
    public float reverb_damping  { get; init; } = 0.5f;
    public float reverb_mix      { get; init; } = 0.0f;

    // Delay
    public float delay_time     { get; init; } = 0.25f;  // seconds
    public float delay_feedback { get; init; } = 0.3f;   // [0.0, 0.95]
    public float delay_mix      { get; init; } = 0.0f;
    public bool  delay_tempo_sync{ get; init; } = false;

    // Retro
    public RetroMode retro_mode { get; init; } = RetroMode.Clean;

    public static readonly FxPreset Default = new();
}
