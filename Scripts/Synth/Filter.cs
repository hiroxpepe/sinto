// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Runtime.CompilerServices;
using Sinto.Core.Audio;

namespace Sinto.Core.Synth;

public enum FilterKind : byte { Roland = 0, Moog = 1 }

/// <summary>
/// Bilinear 4-pole Moog ladder filter, faithful to the MusicDSP original
/// (musicdsp.org id=24): r = resonance * (t2+6*t1) / (t2-6*t1) for the
/// frequency-dependent resonance normalization, a linear feedback path
/// (x = input - r*stage[3]), and the band-limited sigmoid stage[3] -= stage[3]^3/6
/// that self-limits the self-oscillation amplitude. Self-oscillation onset is not
/// a single fixed RESO; it ranges ~0.6 (low/mid cutoff) to ~0.95 (top cutoff).
/// Moog: stage[3] tap (-24dB/oct). Roland: stage[1] tap (-12dB/oct).
/// </summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public struct Filter {
    // ln(11000/20): maps CUTOFF [0,1] to fc [20Hz, 11kHz].
    // fc_max capped at 11kHz so self-oscillation (~fc*2) stays just under Nyquist,
    // eliminating the aliasing fold that caused the 'step' artifact at CUTOFF 95-100.
    // Sub-bass growl is unaffected (floor stays at 20Hz).
    const float LN_FC_MAX = 6.30991827822652f;
    const float ROLAND_SCALE = 0.70f;

    // Stage states (4-pole ladder)
    float _s1, _s2, _s3, _s4;
    // Bilinear delay states (previous input to each stage)
    float _d0, _d1, _d2, _d3;
    // Bilinear coefficients
    float _p;   // stage gain  = cn*(1.8-0.8*cn)
    float _ks;  // stage k     = 2*sin(cn*PI/2)-1
    float _r;   // resonance = user*(t2+6t1)/(t2-6t1)  (MusicDSP original, no extra scaling)
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
        float cutoff_hz = 20f * MathF.Exp(LN_FC_MAX * cutoff);
        // Bilinear normalized cutoff [0,1]: cn = 2*fc/SR.
        float cn = 2.0f * cutoff_hz / sampleRate;
        if (cn > 1.0f) cn = 1.0f;
        // MusicDSP bilinear stage coefficients.
        float p  = cn * (1.8f - 0.8f * cn);
        float ks = 2.0f * MathF.Sin(cn * MathF.PI * 0.5f) - 1.0f;
        // MusicDSP original resonance normalization (musicdsp.org id=24).
        // Frequency-dependent compensation; NO extra scaling (we previously had a
        // /0.75 factor that incorrectly lowered the oscillation onset).
        float t1 = (1.0f - p) * 1.386249f;
        float t2 = 12.0f + t1 * t1;
        float r  = resonance * (t2 + 6.0f * t1) / (t2 - 6.0f * t1);
        _p = p; _ks = ks; _r = r;
        _mode = mode;
        // Kick-start self-oscillation when resonance is above oscillation threshold.
        if (resonance > 0.76f && _s1 == 0f && _s2 == 0f && _s3 == 0f && _s4 == 0f)
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
        // Both modes run the identical bilinear ladder; only the output tap differs.
        // Moog: stage[3] (s4, -24dB/oct). Roland: stage[1] (s2, -12dB/oct) * ROLAND_SCALE.
        float output = TickLadder(protected_input);
        if (float.IsNaN(output) || float.IsInfinity(output)) output = 0f;
        if      (output >  1.9f) output =  1.9f;
        else if (output < -1.9f) output = -1.9f;
        return output;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    float TickLadder(float input) {
        // MusicDSP original 4-pole bilinear ladder (musicdsp.org id=24), faithful:
        //   x = input - resonance * stage[3]            (LINEAR feedback)
        //   four cascaded bilinear one-poles
        //   stage[3] -= stage[3]^3 / 6                  (band-limited sigmoid,
        //                                                self-limits oscillation)
        float x  = input - _r * _s4;                     // linear feedback
        float n1 = x  * _p + _d0 * _p - _ks * _s1;
        float n2 = n1 * _p + _d1 * _p - _ks * _s2;
        float n3 = n2 * _p + _d2 * _p - _ks * _s3;
        float n4 = n3 * _p + _d3 * _p - _ks * _s4;
        n4 -= (n4 * n4 * n4) * (1.0f / 6.0f);            // clipping band-limited sigmoid
        _d0 = x; _d1 = n1; _d2 = n2; _d3 = n3;
        _s1 = n1; _s2 = n2; _s3 = n3; _s4 = n4;
        // Output tap selects the filter slope (-24dB/oct vs -12dB/oct).
        return _mode == FilterKind.Roland ? _s2 * ROLAND_SCALE : _s4;
    }

    public void Reset() {
        _s1 = _s2 = _s3 = _s4 = 0f;
        _d0 = _d1 = _d2 = _d3 = 0f;
        _cache_valid = false;
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////
    // public static Methods [verb]

    /// <summary>
    /// Computes filter coefficients for diagnostics/UI logging without mutating state.
    /// Uses the exact same math as SetParams so UI logs always match the running filter.
    /// </summary>
    public static Diagnostics Diagnose(float cutoff, float resonance,
        FilterKind mode, int sampleRate) {
        if (sampleRate <= 0) sampleRate = 44100;
        if      (cutoff    < 0.001f) cutoff    = 0.001f;
        else if (cutoff    > 0.999f) cutoff    = 0.999f;
        if      (resonance < 0f)     resonance = 0f;
        else if (resonance > 1f)     resonance = 1f;
        float cutoff_hz = 20f * MathF.Exp(LN_FC_MAX * cutoff);
        float cn = 2.0f * cutoff_hz / sampleRate;
        if (cn > 1.0f) cn = 1.0f;
        float p  = cn * (1.8f - 0.8f * cn);
        float t1 = (1.0f - p) * 1.386249f;
        float t2 = 12.0f + t1 * t1;
        float r  = resonance * (t2 + 6.0f * t1) / (t2 - 6.0f * t1);
        // MusicDSP original normalization (matches SetParams exactly). Onset is
        // NOT a single fixed RESO: measured onset r ranges ~1.4 (top cutoff) to
        // ~2.5 (low/mid). r >= 1.4 is the lowest onset across the range, so we
        // report it as a conservative "may oscillate" indicator only.
        bool oscillates = r >= 1.4f;
        return new Diagnostics(cutoff_hz, p, r, oscillates);
    }

    ///////////////////////////////////////////////////////////////////////////////////////////////
    // inner Classes

    /// <summary>Read-only filter coefficient snapshot for diagnostics/UI logging.</summary>
    /// <author>h.adachi (STUDIO MeowToon)</author>
    public readonly struct Diagnostics {
        public readonly float cutoffHz;
        public readonly float p;
        public readonly float rNorm;
        public readonly bool  oscillates;
        public Diagnostics(float cutoff_hz, float p_coeff, float r_norm, bool oscillates) {
            cutoffHz = cutoff_hz; p = p_coeff; rNorm = r_norm; this.oscillates = oscillates;
        }
    }
}
