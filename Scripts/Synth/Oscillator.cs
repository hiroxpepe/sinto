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
    // Paul Kellet pink-noise filter state (9-pole improved version).
    float _pk0, _pk1, _pk2, _pk3, _pk4, _pk5, _pk6, _pk7, _pk8;
    // DC blocker for Triangle
    float _dcX, _dcY;
    // Xorshift128 state for white noise
    uint _x0, _x1, _x2, _x3;
    // DPW state for shaped SAW
    double _dpwPrevSaw;
    float _sawDcX, _sawDcY;

    public void SetFrequency(float frequencyHz, int sampleRate) {
        if (frequencyHz < 0.001f) frequencyHz = 0.001f;
        if (sampleRate <= 0) sampleRate = 44100;
        _phase_inc = (double)frequencyHz / sampleRate;
        if (_x0 == 0) { _x0 = 0x12345678u; _x1 = 0x87654321u; _x2 = 0xABCDEF01u; _x3 = 0x10FEDCBAu; }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Tick(in OscParams p) {
        double t  = _phase;       // [0, 1)
        double dt = _phase_inc;   // [0, 1)
        WaveType w = p.Wave;
        // Phase distortion (shape): remap the cycle so the front half is
        // compressed (shape<0.5) or the back half (shape>0.5).
        // ts spans the full [0,1) so all wave generators work correctly.
        // A soft-knee (cubic) on the pivot keeps the centre region gentle.
        double ts = t;
        float s = p.Shape;
        bool shaped = false;
        if (s != 0.5f && (w & (WaveType.Saw | WaveType.Triangle | WaveType.Sine)) != 0) {
            double d = s - 0.5;
            double pivot = 0.5 + d * Math.Abs(d) * 3.0;
            pivot = pivot < 0.01 ? 0.01 : pivot > 0.99 ? 0.99 : pivot;
            ts = t < pivot
                ? (t / pivot) * 0.5
                : 0.5 + ((t - pivot) / (1.0 - pivot)) * 0.5;
            shaped = true;
        }
        float sum = 0f;
        int active = 0;
        if ((w & WaveType.Sine) != 0)     { sum += GenSine(ts);                       active++; }
        if ((w & WaveType.Saw) != 0)      { sum += shaped ? GenSawNaive(ts) : GenSaw(t, dt);           active++; }
        if ((w & WaveType.Square) != 0)   { sum += GenSquare(t, dt, p.PulseWidth);    active++; }
        if ((w & WaveType.Triangle) != 0) { sum += shaped ? GenTriNaive(ts)  : GenTriangle(t, dt);     active++; }
        if ((w & WaveType.Noise) != 0)    { sum += GenNoise();                        active++; }
        if ((w & WaveType.Pink) != 0)     { sum += GenPink();                         active++; }
        float sample = active > 1 ? sum / active : sum;

        _phase += _phase_inc;
        if (_phase >= 1.0) _phase -= 1.0;
        return sample * p.Level;
    }

    // Naive SAW for shaped phase + DC blocker + clamp.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    float GenSawNaive(double ts) {
        float raw = (float)(2.0 * ts - 1.0);
        if (raw >  1f) raw =  1f;
        if (raw < -1f) raw = -1f;
        float dcOut = raw - _sawDcX + 0.998f * _sawDcY;
        _sawDcX = raw; _sawDcY = dcOut;
        if (dcOut >  1f) dcOut =  1f;
        if (dcOut < -1f) dcOut = -1f;
        return dcOut;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static float GenTriNaive(double t) {
        float v = (float)(t < 0.5 ? 4.0 * t - 1.0 : 3.0 - 4.0 * t);
        if (v >  1f) v =  1f;
        if (v < -1f) v = -1f;
        return v;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    float GenSine(double t) => Calc.SinFast(t * TWO_PI);

    // DPW-2 SAW (MusicDSP: Discrete-time synthesis of the sawtooth waveform).
    // v[n] = t^2 - t (parabolic), saw = (v[n] - v[n-1]) / dt, scaled to ±1.
    // +10dB SNR improvement over naive, same CPU cost as polyBLEP.
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    float GenSaw(double t, double dt) {
        double v = t * t - t;
        double diff = v - _dpwPrevSaw;
        _dpwPrevSaw = v;
        if (diff > 0.5) diff -= 1.0;
        if (diff < -0.5) diff += 1.0;
        double scale = dt > 0 ? 1.0 / dt : 0;
        float s = (float)(diff * scale);
        // Clamp to ±1 — DPW can overshoot slightly at wrap points
        if (s >  1f) s =  1f;
        if (s < -1f) s = -1f;
        return s;
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
        float raw = _last_triangle;
        float dcOut = raw - _dcX + 0.998f * _dcY;
        _dcX = raw; _dcY = dcOut;
        if (dcOut >  1f) dcOut =  1f;
        if (dcOut < -1f) dcOut = -1f;
        return dcOut;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    float GenNoise() {
        // Xorshift128 (period 2^128-1)
        if (_x0 == 0) { _x0 = 0x12345678u; _x1 = 0x87654321u; _x2 = 0xABCDEF01u; _x3 = 0x10FEDCBAu; }
        uint t = _x0 ^ (_x0 << 11);
        _x0 = _x1; _x1 = _x2; _x2 = _x3;
        _x3 = _x3 ^ (_x3 >> 19) ^ t ^ (t >> 8);
        return (int)_x3 / (float)int.MaxValue;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    float GenPink() {
        // Paul Kellet 9-pole improved pink-noise filter (white -> -3 dB/oct).
        float white = GenNoise();
        _pk0 = 0.99886f * _pk0 + white * 0.0555179f;
        _pk1 = 0.99332f * _pk1 + white * 0.0750759f;
        _pk2 = 0.96900f * _pk2 + white * 0.1538520f;
        _pk3 = 0.86650f * _pk3 + white * 0.3104856f;
        _pk4 = 0.55000f * _pk4 + white * 0.5329522f;
        _pk5 = -0.7616f * _pk5 - white * 0.0168980f;
        _pk7 = 0.99621f * _pk7 + white * 0.0343144f;
        _pk8 = 0.96900f * _pk8 + white * 0.0231510f;
        float pink = _pk0 + _pk1 + _pk2 + _pk3 + _pk4 + _pk5 + _pk6 + _pk7 + _pk8 + white * 0.5362f;
        _pk6 = white * 0.115926f;
        return pink * 0.09f;
    }

    /// <summary>2nd-order polyBLEP (MusicDSP). Better aliasing suppression than 1st-order.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static double PolyBlep(double t, double dt) {
        if (t < dt) {
            double x = t / dt - 1.0;
            return -x * x;
        }
        if (t > 1.0 - dt) {
            double x = (t - 1.0) / dt + 1.0;
            return x * x;
        }
        return 0.0;
    }
}
