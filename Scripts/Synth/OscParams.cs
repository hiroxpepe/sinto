// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System.Runtime.InteropServices;

namespace Sinto.Core.Synth;

[StructLayout(LayoutKind.Sequential)]
public readonly struct OscParams {
    public readonly WaveType   Wave;
    public readonly Interpolation Interp;
    public readonly float      DetuneCents;
    public readonly float      PulseWidth;
    public readonly float      Level;

    public OscParams(WaveType wave, Interpolation interp = Interpolation.Linear,
        float detuneCents = 0f, float pulseWidth = 0.5f, float level = 1.0f)
        => throw new System.NotImplementedException();
}
