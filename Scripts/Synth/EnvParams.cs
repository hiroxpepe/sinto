// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System.Runtime.InteropServices;

namespace Signo.Core.Synth;

/// <summary>ADSR envelope parameters. Immutable.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
[StructLayout(LayoutKind.Sequential)]
public readonly struct EnvParams {
#nullable enable
    public readonly float Attack;
    public readonly float Decay;
    public readonly float Sustain;
    public readonly float Release;

    public static readonly EnvParams Default     = new(0.01f,  0.1f, 0.8f, 0.2f);
    public static readonly EnvParams Percussive  = new(0.001f, 0.1f, 0.0f, 0.1f);
    public static readonly EnvParams Pad         = new(0.5f,   0.3f, 0.9f, 1.0f);

    public EnvParams(float attack = 0.01f, float decay = 0.1f,
        float sustain = 0.8f, float release = 0.2f) {
        // Clamp Attack to [0.001, 10.0]. Use Envelope's bypass for true zero-attack.
        if      (attack < 0.001f) attack = 0.001f;
        else if (attack > 10f)    attack = 10f;
        // Clamp Decay to [0.001, 10.0]
        if      (decay  < 0.001f) decay  = 0.001f;
        else if (decay  > 10f)    decay  = 10f;
        // Clamp Sustain to [0, 1]
        if      (sustain < 0f) sustain = 0f;
        else if (sustain > 1f) sustain = 1f;
        // Clamp Release to [0.001, 20.0]
        if      (release < 0.001f) release = 0.001f;
        else if (release > 20f)    release = 20f;
        Attack  = attack;
        Decay   = decay;
        Sustain = sustain;
        Release = release;
    }
}
