// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System.Runtime.InteropServices;

namespace Sinto.Core.Synth;

[StructLayout(LayoutKind.Sequential)]
public readonly struct EnvParams {
    public readonly float Attack;
    public readonly float Decay;
    public readonly float Sustain;
    public readonly float Release;

    public static readonly EnvParams Default     = new(0.01f, 0.1f,  0.8f, 0.2f);
    public static readonly EnvParams Percussive  = new(0.001f, 0.1f, 0.0f, 0.1f);
    public static readonly EnvParams Pad         = new(0.5f,  0.3f,  0.9f, 1.0f);

    public EnvParams(float attack = 0.01f, float decay = 0.1f,
        float sustain = 0.8f, float release = 0.2f)
        => throw new System.NotImplementedException();
}
