// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;

namespace Signo.Core.Effects;

/// <summary>
/// AW-3 style auto wah v2.
/// Filter: Cytomic/Mystran SVF (Linear Trapezoidal State Variable Filter).
///   Stable under fast cutoff changes, no click noise.
///   Ref: Andrew Simper "Solving the continuous SVF equations..."
///   https://cytomic.com/files/dsp/SvfLinearTrapOptimised2.pdf
/// Envelope follower: rectifier + 1-pole LP (1ms attack / 60ms release).
/// Up mode:   louder → cutoff sweeps up   (classic funk wah).
/// Down mode: louder → cutoff sweeps down (reverse wah).
/// Output: dry*(1-send) + (LP*0.5 + BP*0.5)*send.
/// </summary>
public sealed class AutoWah : IInsertEffect
{
    readonly int _sr;
    float _sensitivity, _freqBase, _peak, _send;
    WahMode _mode = WahMode.Up;

    float _env = 0f;
    readonly float _attackCoef, _releaseCoef;

    // SVF integrator state (per channel)
    float _s1L, _s2L;
    float _s1R, _s2R;

    const float MIN_FREQ = 200f;
    const float MAX_FREQ = 4000f;

    public bool  enabled { get; set; } = true;
    public float Send    { get => _send; set => _send = Math.Clamp(value, 0f, 1f); }

    public AutoWah(int sampleRate)
    {
        _sr          = sampleRate;
        _attackCoef  = MathF.Exp(-1f / (sampleRate * 0.001f));   // 1ms
        _releaseCoef = MathF.Exp(-1f / (sampleRate * 0.060f));   // 60ms
        SetParams(0.7f, 0.3f, 0.6f);
    }

    public void SetParams(float sensitivity, float freq, float peak)
    {
        _sensitivity = Math.Clamp(sensitivity, 0f, 1f);
        _freqBase    = Math.Clamp(freq,        0f, 1f);
        _peak        = Math.Clamp(peak,        0f, 1f);
    }

    public void SetMode(WahMode m) => _mode = m;

    public void Process(Span<float> buffer, int channels)
    {
        if (!enabled) return;
        int frames = buffer.Length / channels;
        for (int f = 0; f < frames; f++) {
            int   idxL = f * channels, idxR = channels > 1 ? idxL + 1 : idxL;
            float inL  = buffer[idxL], inR = buffer[idxR];

            // Envelope follower
            float level = (MathF.Abs(inL) + MathF.Abs(inR)) * 0.5f;
            float coef  = level > _env ? _attackCoef : _releaseCoef;
            _env = _env * coef + level * (1f - coef);

            // Cutoff frequency mapping
            float mod    = Math.Clamp(_env * _sensitivity * 3f, 0f, 1f);
            float t      = _mode == WahMode.Up
                ? _freqBase + mod * (1f - _freqBase)
                : (1f - _freqBase) * (1f - mod);
            t = Math.Clamp(t, 0f, 1f);
            float cutoff = MIN_FREQ + (MAX_FREQ - MIN_FREQ) * t * t;

            // Cytomic SVF coefficients
            // g = tan(π*fc/sr), k = 1/Q
            float g  = MathF.Tan(MathF.PI * cutoff / _sr);
            float k  = 2f - _peak * 1.9f;   // k: 2=no resonance, 0.1=high Q
            k        = Math.Max(0.1f, k);    // safety clamp
            float a1 = 1f / (1f + g * (g + k));
            float a2 = g * a1;
            float a3 = g * a2;

            // SVF process L (Cytomic linear trapezoidal)
            float v3L  = inL - _s2L;
            float v1L  = a1 * _s1L + a2 * v3L;
            float v2L  = _s2L + a2 * _s1L + a3 * v3L;
            _s1L       = 2f * v1L - _s1L;
            _s2L       = 2f * v2L - _s2L;
            float lpL  = v2L;
            float bpL  = v1L;

            // SVF process R
            float v3R  = inR - _s2R;
            float v1R  = a1 * _s1R + a2 * v3R;
            float v2R  = _s2R + a2 * _s1R + a3 * v3R;
            _s1R       = 2f * v1R - _s1R;
            _s2R       = 2f * v2R - _s2R;
            float lpR  = v2R;
            float bpR  = v1R;

            // Mix LP + BP for wah character, blend with dry
            float wetL = lpL * 0.5f + bpL * 0.5f;
            float wetR = lpR * 0.5f + bpR * 0.5f;

            buffer[idxL] = inL * (1f - _send) + wetL * _send;
            buffer[idxR] = inR * (1f - _send) + wetR * _send;
        }
    }

    public void Process(float[] buffer, int offset, int count, int channels)
        => Process(buffer.AsSpan(offset, count), channels);

    public void Reset()
    {
        _env = 0f;
        _s1L = _s2L = 0f;
        _s1R = _s2R = 0f;
    }
}
