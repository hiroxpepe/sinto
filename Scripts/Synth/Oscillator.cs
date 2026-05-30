// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Runtime.CompilerServices;

namespace Signo.Core.Synth;

/// <summary>Band-limited oscillator using polyBLEP for Saw/Square aliasing reduction.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public struct Oscillator {
    const double TWO_PI    = 6.283185307179586;
    const double INV_TWO_PI = 0.15915494309189534;

    double _phase;       // [0, 1) — normalized phase for polyBLEP convenience
    double _phase_inc;   // dt per sample [0, 1)
    uint   _noise_seed;
    float  _last_triangle; // for triangle integrator
    // Paul Kellet pink-noise filter state.
    float _pk0, _pk1, _pk2, _pk3, _pk4, _pk5, _pk6;

    public void SetFrequency(float frequencyHz, int sampleRate) {
        if (frequencyHz < 0.001f) frequencyHz = 0.001f;
        if (sampleRate <= 0) sampleRate = 44100;
        _phase_inc = (double)frequencyHz / sampleRate;
        if (_noise_seed == 0) _noise_seed = 0x12345678u;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Tick(in OscParams p) {
        double t  = _phase;       // [0, 1)
        double dt = _phase_inc;   // [0, 1)
        WaveType w = p.Wave;
        float sum = 0f;
        int active = 0;
        if ((w & WaveType.Sine) != 0)     { sum += GenSine(t);          active++; }
        if ((w & WaveType.Saw) != 0)      { sum += GenSaw(t, dt);       active++; }
        if ((w & WaveType.Square) != 0)   { sum += GenSquare(t, dt, p.PulseWidth); active++; }
        if ((w & WaveType.Triangle) != 0) { sum += GenTriangle(t, dt);  active++; }
        if ((w & WaveType.Noise) != 0)    { sum += GenNoise();          active++; }
        if ((w & WaveType.Pink) != 0)     { sum += GenPink();           active++; }
        // Power-based normalisation (÷√n): stacked waveforms keep their loudness
        // far better than ÷n while still leaving headroom against clipping.
        float sample = active > 1 ? sum / MathF.Sqrt(active) : sum;

        _phase += _phase_inc;
        if (_phase >= 1.0) _phase -= 1.0;
        return sample * p.Level;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    float GenSine(double t) => Calc.SinFast(t * TWO_PI);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    float GenSaw(double t, double dt) {
        float naive = (float)(2.0 * t - 1.0);
        naive -= (float)PolyBlep(t, dt);
        return naive;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    float GenSquare(double t, double dt, float pw) {
        float naive = t < pw ? 1f : -1f;
        naive += (float)PolyBlep(t, dt);
        double t2 = t - pw;
        if (t2 < 0.0) t2 += 1.0;
        naive -= (float)PolyBlep(t2, dt);
        return naive;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    float GenTriangle(double t, double dt) {
        float pw = 0.5f;
        float sq = t < pw ? 1f : -1f;
        sq += (float)PolyBlep(t, dt);
        double t2 = t - pw;
        if (t2 < 0.0) t2 += 1.0;
        sq -= (float)PolyBlep(t2, dt);
        _last_triangle = (float)(_last_triangle * 0.9995 + sq * dt * 4.0);
        return _last_triangle;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    float GenNoise() {
        if (_noise_seed == 0) _noise_seed = 0x12345678u;
        _noise_seed = _noise_seed * 1664525u + 1013904223u;
        return (int)_noise_seed / (float)int.MaxValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    float GenPink() {
        // Paul Kellet's economical pink-noise filter (white -> -3 dB/oct).
        float white = GenNoise();
        _pk0 = 0.99886f * _pk0 + white * 0.0555179f;
        _pk1 = 0.99332f * _pk1 + white * 0.0750759f;
        _pk2 = 0.96900f * _pk2 + white * 0.1538520f;
        _pk3 = 0.86650f * _pk3 + white * 0.3104856f;
        _pk4 = 0.55000f * _pk4 + white * 0.5329522f;
        _pk5 = -0.7616f * _pk5 - white * 0.0168980f;
        float pink = _pk0 + _pk1 + _pk2 + _pk3 + _pk4 + _pk5 + _pk6 + white * 0.5362f;
        _pk6 = white * 0.115926f;
        return pink * 0.11f; // scale toward [-1, 1]
    }

    /// <summary>polyBLEP: polynomial band-limited step. Corrects discontinuities.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static double PolyBlep(double t, double dt) {
        // t and dt are normalized to [0, 1)
        if (t < dt) {
            double x = t / dt;
            return x + x - x * x - 1.0;
        }
        if (t > 1.0 - dt) {
            double x = (t - 1.0) / dt;
            return x * x + x + x + 1.0;
        }
        return 0.0;
    }
}
