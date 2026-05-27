// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Runtime.CompilerServices;
using Sinto.Core.Audio;

namespace Sinto.Core.Synth;

public enum FilterKind : byte { Roland = 0, Moog = 1 }

/// <summary>Filter: Moog 4-pole ladder + Roland one-pole. Denormal-safe, cached coefficients.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public struct Filter {
#nullable enable
    private const float TWO_PI = 6.28318530717958f;
    private const float LN1000 = 6.90775527898214f; // ln(1000) for exp() instead of pow()

    // State variables
    private float _s1, _s2, _s3, _s4;
    private float _k;          // resonance feedback (internal, ×4.5 scaled)
    private float _cutoff_g;   // cutoff frequency coefficient
    private FilterKind _mode;
    // Cache for cheap SetParams: avoid recomputing MathF.Exp every sample
    private float _cached_cutoff;
    private float _cached_resonance;
    private int   _cached_sr;
    private FilterKind _cached_mode;
    private bool  _cache_valid;

    public void SetParams(float cutoff, float resonance,
        FilterKind mode, int sampleRate) {
        if (sampleRate <= 0) sampleRate = 44100;
        // Clamp inputs
        if      (cutoff < 0.001f) cutoff = 0.001f;
        else if (cutoff > 0.999f) cutoff = 0.999f;
        if      (resonance < 0f) resonance = 0f;
        else if (resonance > 1f) resonance = 1f;
        // Fast path: parameters unchanged → reuse cached coefficients
        if (_cache_valid &&
            cutoff    == _cached_cutoff &&
            resonance == _cached_resonance &&
            mode      == _cached_mode &&
            sampleRate == _cached_sr) {
            return;
        }
        // Slow path: recompute coefficients
        // exp(ln(1000) * cutoff) is identical to MathF.Pow(1000f, cutoff) but ~2x faster
        float cutoff_hz = 20f * MathF.Exp(LN1000 * cutoff);
        float g = TWO_PI * cutoff_hz / sampleRate;
        if (g > 0.99f) g = 0.99f;
        _cutoff_g = g;
        // Moog resonance: scale user [0,1] → internal [0, 4.5]
        float k = resonance * 4.5f;
        if (k > 4.5f) k = 4.5f;
        _k = k;
        _mode = mode;
        // Kick-start self-oscillation on first high-resonance setup
        if (mode == FilterKind.Moog && k > 3.5f && _s1 == 0f && _s2 == 0f && _s3 == 0f && _s4 == 0f) {
            _s1 = 0.05f;
        }
        // Cache result
        _cached_cutoff    = cutoff;
        _cached_resonance = resonance;
        _cached_mode      = mode;
        _cached_sr        = sampleRate;
        _cache_valid      = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Process(float input, long sampleIndex) {
        // Denormal protection on input — flows into IIR feedback, prevents subnormal trap
        float protected_input = Denormal.Protect(input, sampleIndex);
        float output;
        switch (_mode) {
            case FilterKind.Moog:
                output = ProcessMoog(protected_input);
                break;
            case FilterKind.Roland:
                output = ProcessRoland(protected_input);
                break;
            default:
                output = protected_input;
                break;
        }
        // Safety clamp against divergence (NaN/Inf/runaway)
        if (float.IsNaN(output) || float.IsInfinity(output)) output = 0f;
        if      (output >  1.9f) output =  1.9f;
        else if (output < -1.9f) output = -1.9f;
        return output;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float ProcessMoog(float input) {
        // Moog 4-pole linear ladder. Self-oscillation at k=4.0+.
        float u = input - _k * _s4;
        _s1 += _cutoff_g * (u  - _s1);
        _s2 += _cutoff_g * (_s1 - _s2);
        _s3 += _cutoff_g * (_s2 - _s3);
        _s4 += _cutoff_g * (_s3 - _s4);
        // Hard clamp internal state to prevent runaway while allowing audible oscillation
        if      (_s4 >  1.5f) _s4 =  1.5f;
        else if (_s4 < -1.5f) _s4 = -1.5f;
        return _s4;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private float ProcessRoland(float input) {
        // Simple two-pole with resonance feedback (Juno-106 style)
        _s1 += _cutoff_g * (input - _s1 + _k * 0.25f * (_s1 - _s2));
        _s2 += _cutoff_g * (_s1 - _s2);
        return _s2;
    }

    public void Reset() {
        _s1 = _s2 = _s3 = _s4 = 0f;
        _cache_valid = false;
    }
}
