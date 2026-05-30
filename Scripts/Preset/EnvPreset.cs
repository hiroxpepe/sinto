// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable

namespace Signo.Core.Preset;

/// <summary>Serializable ADSR envelope preset for .signo JSON.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public sealed class EnvPreset
{
    public float attack  { get; init; } = 0.01f;  // [0.001, 10.0]
    public float decay   { get; init; } = 0.1f;   // [0.001, 10.0]
    public float sustain { get; init; } = 0.8f;   // [0.0, 1.0]
    public float release { get; init; } = 0.2f;   // [0.001, 20.0]

    public static readonly EnvPreset Default     = new();
    public static readonly EnvPreset Percussive  = new() { attack=0.001f, decay=0.1f,  sustain=0f,   release=0.1f };
    public static readonly EnvPreset Pad         = new() { attack=0.5f,   decay=0.3f,  sustain=0.9f, release=1.0f };
}
