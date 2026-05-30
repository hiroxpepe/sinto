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

    public void SetFrequency(float frequencyHz, int sampleRate) {
        if (frequencyHz < 0.001f) frequencyHz = 0.001f;
        if (sampleRate <= 0) sampleRate = 44100;
        _phase_inc = (double)frequencyHz / sampleRate;
        if (_noise_seed == 0) _noise_seed = 0x12345678u;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Tick(in OscParams p) {
        float sample;
        double t  = _phase;       // [0, 1)
        double dt = _phase_inc;   // [0, 1)
        switch (p.Wave) {
            case WaveType.Sine:
                sample = Calc.SinFast(t * TWO_PI);
                break;
            case WaveType.Saw: {
                // Naive saw: 2*t - 1 ∈ [-1, 1)
                // polyBLEP subtraction at phase wrap (t=0/1)
                float naive = (float)(2.0 * t - 1.0);
                naive -= (float)PolyBlep(t, dt);
                sample = naive;
                break;
            }
            case WaveType.Square: {
                // Naive square: +1 if t < pw, else -1
                // polyBLEP at TWO transitions: t=0 (up) and t=pw (down)
                float pw = p.PulseWidth;
                float naive = t < pw ? 1f : -1f;
                naive += (float)PolyBlep(t, dt);
                double t2 = t - pw;
                if (t2 < 0.0) t2 += 1.0;
                naive -= (float)PolyBlep(t2, dt);
                sample = naive;
                break;
            }
            case WaveType.Triangle: {
                // Differentiated triangle = square. Use a leaky integrator on band-limited square.
                float pw = 0.5f;
                float sq = t < pw ? 1f : -1f;
                sq += (float)PolyBlep(t, dt);
                double t2 = t - pw;
                if (t2 < 0.0) t2 += 1.0;
                sq -= (float)PolyBlep(t2, dt);
                // Integrate (leaky to prevent DC drift)
                _last_triangle = (float)(_last_triangle * 0.9995 + sq * dt * 4.0);
                sample = _last_triangle;
                break;
            }
            case WaveType.Noise:
                if (_noise_seed == 0) _noise_seed = 0x12345678u;
                _noise_seed = _noise_seed * 1664525u + 1013904223u;
                sample = (int)_noise_seed / (float)int.MaxValue;
                break;
            default:
                sample = 0f;
                break;
        }
        _phase += _phase_inc;
        if (_phase >= 1.0) _phase -= 1.0;
        return sample * p.Level;
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
