// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Runtime.CompilerServices;

namespace Signo.Core.Synth;

/// <summary>
/// First-order (-6 dB/oct) resonance-less high-pass filter, placed before the
/// main filter (DCO -> HPF -> DCF). Cutoff 0 = off (pass-all). Mild low-end
/// roll-off in the spirit of the Juno-106 HPF.
/// </summary>
public struct HighPassFilter {
#nullable enable
    float _a;          // coefficient (0 = pass-all)
    float _prev_in;
    float _prev_out;
    bool  _active;

    /// <summary>Set cutoff in Hz. 0 (or below ~1 Hz) disables the filter.</summary>
    public void SetCutoff(float cutoffHz, int sampleRate) {
        if (sampleRate <= 0) sampleRate = 44100;
        if (cutoffHz < 1f) { _active = false; _a = 0f; return; }
        _active = true;
        float dt = 1f / sampleRate;
        float rc = 1f / (2f * MathF.PI * cutoffHz);
        _a = rc / (rc + dt);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Process(float x) {
        if (!_active) return x;
        // y[n] = a * (y[n-1] + x[n] - x[n-1])
        float y = _a * (_prev_out + x - _prev_in);
        _prev_in  = x;
        _prev_out = y;
        return y;
    }

    public void Reset() {
        _prev_in = 0f; _prev_out = 0f;
    }
}
