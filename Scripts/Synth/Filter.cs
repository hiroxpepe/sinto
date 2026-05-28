// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Runtime.CompilerServices;
using Sinto.Core.Audio;

namespace Sinto.Core.Synth;

public enum FilterKind : byte { Roland = 0, Moog = 1 }

/// <summary>
/// MusicDSP bilinear Moog ladder filter with frequency-dependent resonance normalization.
/// Reference: ddiakopoulos/MoogLadders MusicDSPModel.h (unlicensed / public domain).
/// resonance = user_r * (t2+6*t1) / (t2-6*t1) compensates for the bilinear gain drop
/// at low cutoff, so self-oscillation is audible across the full CUTOFF range.
/// Moog: stage[3] tap (-24dB/oct). Roland: stage[1] tap (-12dB/oct).
/// </summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public struct Filter {
    const float LN1000  = 6.90775527898214f; // ln(1000)
    const float T1_SCALE = 1.386249f;
    const float T2_BASE  = 12.0f;
    const float ROLAND_SCALE = 0.70f;

    // Stage states (4-pole ladder)
    float _s1, _s2, _s3, _s4;
    // Bilinear delay states (previous input to each stage)
    float _d0, _d1, _d2, _d3;
    // Bilinear coefficients
    float _p;   // stage gain  = cn*(1.8-0.8*cn)
    float _ks;  // stage k     = 2*sin(cn*PI/2)-1
    float _r;   // resonance   = user*(t2+6t1)/(t2-6t1)
    FilterKind _mode;
    // Cache
    float      _cached_cutoff;
    float      _cached_resonance;
    int        _cached_sr;
    FilterKind _cached_mode;
    bool       _cache_valid;

    public void SetParams(float cutoff, float resonance,
        FilterKind mode, int sampleRate) {
        if (sampleRate <= 0) sampleRate = 44100;
        if      (cutoff    < 0.001f) cutoff    = 0.001f;
        else if (cutoff    > 0.999f) cutoff    = 0.999f;
        if      (resonance < 0f)     resonance = 0f;
        else if (resonance > 1f)     resonance = 1f;
        if (_cache_valid &&
            cutoff     == _cached_cutoff    &&
            resonance  == _cached_resonance &&
            mode       == _cached_mode      &&
            sampleRate == _cached_sr) return;

        // Standard exponential mapping: 20Hz..20kHz for both Moog and Roland.
        float cutoff_hz = 20f * MathF.Exp(LN1000 * cutoff);
        // Bilinear normalized cutoff [0,1]: cn = 2*fc/SR.
        float cn = 2.0f * cutoff_hz / sampleRate;
        if (cn > 1.0f) cn = 1.0f;
        // MusicDSP bilinear coefficients.
        float p  = cn * (1.8f - 0.8f * cn);
        float ks = 2.0f * MathF.Sin(cn * MathF.PI * 0.5f) - 1.0f;
        // Frequency-dependent resonance normalization (MusicDSP MusicDSPModel.h).
        // At low cutoff t1 is large → (t2+6t1)/(t2-6t1) >> 1 → resonance boosted
        // so self-oscillation stays audible regardless of cutoff frequency.
        float t1 = (1.0f - p) * T1_SCALE;
        float t2 = T2_BASE + t1 * t1;
        float r  = resonance * (t2 + 6.0f * t1) / (t2 - 6.0f * t1);
        _p = p; _ks = ks; _r = r;
        _mode = mode;
        // Kick-start self-oscillation when resonance is above oscillation threshold.
        if (resonance > 0.78f && _s1 == 0f && _s2 == 0f && _s3 == 0f && _s4 == 0f)
            _s1 = 0.05f;
        _cached_cutoff    = cutoff;
        _cached_resonance = resonance;
        _cached_mode      = mode;
        _cached_sr        = sampleRate;
        _cache_valid      = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Process(float input, long sampleIndex) {
        float protected_input = Denormal.Protect(input, sampleIndex);
        float output = _mode == FilterKind.Roland
            ? ProcessRoland(protected_input)
            : ProcessMoog(protected_input);
        if (float.IsNaN(output) || float.IsInfinity(output)) output = 0f;
        if      (output >  1.9f) output =  1.9f;
        else if (output < -1.9f) output = -1.9f;
        return output;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    float ProcessMoog(float input) {
        // MusicDSP bilinear Moog 4-pole ladder.
        // Feedback uses tanh for soft saturation / stability at high resonance.
        // stage[i] = prev*p + delay[i]*p - ks*stage[i]   (bilinear one-pole)
        // delay[i] = output of stage[i-1] from previous sample.
        float x  = input - _r * Calc.TanhFast(_s4);
        float n1 = x  * _p + _d0 * _p - _ks * _s1;
        float n2 = n1 * _p + _d1 * _p - _ks * _s2;
        float n3 = n2 * _p + _d2 * _p - _ks * _s3;
        float n4 = n3 * _p + _d3 * _p - _ks * _s4;
        _d0 = x; _d1 = n1; _d2 = n2; _d3 = n3;
        _s1 = n1; _s2 = n2; _s3 = n3; _s4 = n4;
        return _s4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    float ProcessRoland(float input) {
        // IR3109-style: same bilinear ladder, output tapped at stage[1] (-12dB/oct).
        float x  = input - _r * Calc.TanhFast(_s4);
        float n1 = x  * _p + _d0 * _p - _ks * _s1;
        float n2 = n1 * _p + _d1 * _p - _ks * _s2;
        float n3 = n2 * _p + _d2 * _p - _ks * _s3;
        float n4 = n3 * _p + _d3 * _p - _ks * _s4;
        _d0 = x; _d1 = n1; _d2 = n2; _d3 = n3;
        _s1 = n1; _s2 = n2; _s3 = n3; _s4 = n4;
        return _s2 * ROLAND_SCALE;
    }

    public void Reset() {
        _s1 = _s2 = _s3 = _s4 = 0f;
        _d0 = _d1 = _d2 = _d3 = 0f;
        _cache_valid = false;
    }
}
