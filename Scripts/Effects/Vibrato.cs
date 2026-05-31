// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Runtime.CompilerServices;

namespace Signo.Core.Effects;

/// <summary>
/// VB-2 style vibrato: BBD bucket-brigade short delay modulated by sine LFO.
/// Pure pitch modulation — no dry mixing (full wet only, send controls blend).
/// MusicDSP ref: variable delay line, delay = centre + depth * sin(phase)
/// Centre = 5ms, max depth = ±4ms.
/// </summary>
public sealed class Vibrato : IInsertEffect
{
    readonly int     _sr;
    readonly float[] _bufL;
    readonly float[] _bufR;
    readonly int     _bufSize;
    int   _writeL, _writeR;
    float _lfoPhase, _lfoPhaseInc, _depth, _send;

    const float CENTRE_S  = 0.005f;  // 5ms centre delay
    const float MAX_MOD_S = 0.004f;  // ±4ms max modulation depth

    public float RateHz { get; private set; }
    public bool  enabled { get; set; } = true;
    public float Send    { get => _send; set => _send = Math.Clamp(value, 0f, 1f); }

    public Vibrato(int sampleRate)
    {
        _sr      = sampleRate;
        _bufSize = (int)((CENTRE_S + MAX_MOD_S) * sampleRate) + 4;
        _bufL    = new float[_bufSize];
        _bufR    = new float[_bufSize];
        SetParams(4f, 0.5f);
    }

    public void SetParams(float rateHz, float depth)
    {
        RateHz       = Math.Max(0.01f, rateHz);
        _lfoPhaseInc = RateHz / _sr;
        _depth       = Math.Clamp(depth, 0f, 1f);
    }

    public void Process(Span<float> buffer, int channels)
    {
        if (!enabled) return;
        int frames = buffer.Length / channels;
        for (int f = 0; f < frames; f++) {
            int   idxL = f * channels, idxR = channels > 1 ? idxL + 1 : idxL;
            float inL  = buffer[idxL], inR = buffer[idxR];
            // Sine LFO — stereo offset 90° for width
            float lfoL = MathF.Sin(_lfoPhase * TWO_PI);
            float lfoR = MathF.Sin(_lfoPhase * TWO_PI + MathF.PI * 0.5f);
            _lfoPhase += _lfoPhaseInc;
            if (_lfoPhase >= 1f) _lfoPhase -= 1f;
            float delayL = (CENTRE_S + lfoL * MAX_MOD_S * _depth) * _sr;
            float delayR = (CENTRE_S + lfoR * MAX_MOD_S * _depth) * _sr;
            // Write into delay buffer
            _bufL[_writeL] = inL;
            _bufR[_writeR] = inR;
            float wetL = ReadInterp(_bufL, _writeL, delayL);
            float wetR = ReadInterp(_bufR, _writeR, delayR);
            _writeL = (_writeL + 1) % _bufSize;
            _writeR = (_writeR + 1) % _bufSize;
            // Insert: blend dry and wet via Send
            buffer[idxL] = inL * (1f - _send) + wetL * _send;
            buffer[idxR] = inR * (1f - _send) + wetR * _send;
        }
    }

    public void Process(float[] buffer, int offset, int count, int channels)
        => Process(buffer.AsSpan(offset, count), channels);

    public void Reset() { Array.Clear(_bufL); Array.Clear(_bufR); _lfoPhase = 0f; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    float ReadInterp(float[] buf, int write, float delaySamples)
    {
        int   d0   = (int)delaySamples; float frac = delaySamples - d0;
        int   i0   = (write - d0 - 1 + _bufSize) % _bufSize;
        int   i1   = (i0 - 1 + _bufSize) % _bufSize;
        return buf[i0] * (1f - frac) + buf[i1] * frac;
    }

    const float TWO_PI = MathF.PI * 2f;
}
