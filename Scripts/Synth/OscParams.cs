// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System.Runtime.InteropServices;

namespace Signo.Core.Synth;

/// <summary>Oscillator parameters. Immutable.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
[StructLayout(LayoutKind.Sequential)]
public readonly struct OscParams {
#nullable enable
    public readonly WaveType      Wave;
    public readonly Interpolation Interp;
    public readonly float         DetuneCents;
    public readonly float         PulseWidth;
    public readonly float         Shape;   // 0..1, 0.5 = neutral; phase-distortion for SAW/TRI/SIN
    public readonly float         Level;

    public OscParams(WaveType wave, Interpolation interp = Interpolation.Linear,
        float detuneCents = 0f, float pulseWidth = 0.5f, float shape = 0.5f, float level = 1.0f) {
        if      (pulseWidth < 0.01f) pulseWidth = 0.01f;
        else if (pulseWidth > 0.99f) pulseWidth = 0.99f;
        if      (shape < 0f) shape = 0f;
        else if (shape > 1f) shape = 1f;
        if      (level < 0f) level = 0f;
        else if (level > 1f) level = 1f;
        Wave        = wave;
        Interp      = interp;
        DetuneCents = detuneCents;
        PulseWidth  = pulseWidth;
        Shape       = shape;
        Level       = level;
    }
}
