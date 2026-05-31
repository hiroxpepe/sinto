// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Runtime.CompilerServices;

namespace Signo.Core.Effects;

/// <summary>
/// Stereo 4-stage all-pass phaser (MusicDSP first-order all-pass chain).
/// LFO sweeps the all-pass coefficient between minFreq and maxFreq.
/// Supports BPM sync (LFO rate from ARP BPM + note value).
/// CPU-light: 4 all-pass stages per channel, no oversampling.
/// </summary>
public sealed class Phaser : IInsertEffect
{
    readonly int _sr;
    readonly float[] _zL = new float[8];  // 4 stages × (x_prev, y_prev)
    readonly float[] _zR = new float[8];
    float _fbL = 0f, _fbR = 0f;  // feedback from previous output
    float _lfoPhase, _lfoPhaseInc, _depth, _resonance, _send;

    const float MIN_FREQ = 80f;
    const float MAX_FREQ = 8000f;

    public float       RateHz      { get; private set; }
    public bool        enabled     { get; set; } = true;
    public float       Send        { get => _send; set => _send = Math.Clamp(value, 0f, 1f); }
    public LfoWaveform LfoWaveform { get; private set; } = LfoWaveform.Sine;

    public void SetLfoWaveform(LfoWaveform w) => LfoWaveform = w;

    public Phaser(int sampleRate) { _sr = sampleRate; SetParams(0.5f, 0.5f, 0.5f, 0.5f); }

    public void SetParams(float rateHz, float depth, float resonance, float send)
    {
        RateHz       = Math.Max(0.01f, rateHz);
        _lfoPhaseInc = RateHz / _sr;
        _depth       = Math.Clamp(depth,     0f, 1f);
        _resonance   = Math.Clamp(resonance, 0f, 0.95f);
        _send        = Math.Clamp(send,      0f, 1f);
    }

    public void SetBpmSync(float bpm, NoteValue note)
        => SetParams(note.ToHz(bpm), _depth, _resonance, _send);

    public void Process(Span<float> buffer, int channels)
    {
        if (!enabled) return;
        int  frames = buffer.Length / channels;
        bool sqr    = LfoWaveform == LfoWaveform.Square;
        for (int f = 0; f < frames; f++) {
            int   idxL  = f * channels, idxR = channels > 1 ? idxL + 1 : idxL;
            bool  gateOn = !sqr || _lfoPhase < 0.5f;
            float lfoL   = sqr ? 0.5f : (MathF.Sin(_lfoPhase * TWO_PI) + 1f) * 0.5f;
            float lfoR   = sqr ? 0.5f : (MathF.Sin(_lfoPhase * TWO_PI + MathF.PI * 0.5f) + 1f) * 0.5f;
            _lfoPhase += _lfoPhaseInc;
            if (_lfoPhase >= 1f) _lfoPhase -= 1f;
            if (gateOn) {
                float freqL = MIN_FREQ + (MAX_FREQ - MIN_FREQ) * lfoL * _depth;
                float freqR = MIN_FREQ + (MAX_FREQ - MIN_FREQ) * lfoR * _depth;
                float inL   = buffer[idxL], inR = buffer[idxR];
                // Feedback: feed previous output back into allpass input (capped at 0.5*resonance)
                float fbInL = inL + _fbL * (_resonance * 0.5f);
                float fbInR = inR + _fbR * (_resonance * 0.5f);
                float wetL  = AllPass4(fbInL, FreqToCoeff(freqL), _zL);
                float wetR  = AllPass4(fbInR, FreqToCoeff(freqR), _zR);
                _fbL = Math.Clamp(wetL, -1f, 1f);
                _fbR = Math.Clamp(wetR, -1f, 1f);
                buffer[idxL] = Math.Clamp(inL + (wetL - inL) * _send, -1f, 1f);
                buffer[idxR] = Math.Clamp(inR + (wetR - inR) * _send, -1f, 1f);
            } else {
                Array.Clear(_zL); Array.Clear(_zR);
                _fbL = _fbR = 0f;
                buffer[idxL] = 0f;
                buffer[idxR] = 0f;
            }
        }
    }

    public void Process(float[] buffer, int offset, int count, int channels)
        => Process(buffer.AsSpan(offset, count), channels);

    public void Reset() { Array.Clear(_zL); Array.Clear(_zR); _lfoPhase = 0f; _fbL = _fbR = 0f; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    float FreqToCoeff(float freq) { float t = MathF.Tan(MathF.PI * freq / _sr); return (t - 1f) / (t + 1f); }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static float AllPass4(float x, float a, float[] z)
    {
        // First-order all-pass: y[n] = a*x[n] + x[n-1] - a*y[n-1]
        // z[i*2]   = x[n-1] (previous input)
        // z[i*2+1] = y[n-1] (previous output)
        for (int i = 0; i < 4; i++) {
            float xPrev = z[i * 2];
            float yPrev = z[i * 2 + 1];
            float y = a * x + xPrev - a * yPrev;
            z[i * 2]     = x;
            z[i * 2 + 1] = y;
            x = y;
        }
        return x;
    }

    const float TWO_PI = MathF.PI * 2f;
}
