// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Runtime.CompilerServices;

namespace Signo.Core.Effects;

/// <summary>
/// Stereo BBD-style flanger. LFO waveform: Sine (smooth sweep) or Square
/// (gate flanger — BF-3 GATE/PAN equivalent, rhythmic on/off locked to BPM).
/// </summary>
public sealed class Flanger : IInsertEffect
{
    readonly int     _sr;
    readonly float[] _bufL;
    readonly float[] _bufR;
    readonly int     _bufSize;
    int    _writeL, _writeR;
    float  _lfoPhase, _lfoPhaseInc, _depth, _feedback, _send;

    const float MIN_DELAY_S = 0.0005f;
    const float MAX_DELAY_S = 0.005f;

    public float           RateHz      { get; private set; }
    public bool            enabled     { get; set; } = true;
    public LfoWaveform     LfoWaveform { get; private set; } = LfoWaveform.Sine;
    FlangerStepMode _stepMode = FlangerStepMode.Off;
    int             _stepRate = 50;
    public FlangerStepMode StepMode    => _stepMode;
    public void SetStepMode(FlangerStepMode m) => _stepMode = m;
    public void SetStepRate(int r)             => _stepRate = Math.Clamp(r, 0, 100);
    public void ResetPhase()                   => _lfoPhase = 0f;
    public float Send { get => _send; set => _send = Math.Clamp(value, 0f, 1f); }

    public Flanger(int sampleRate)
    {
        _sr      = sampleRate;
        _bufSize = (int)(MAX_DELAY_S * sampleRate) + 4;
        _bufL    = new float[_bufSize];
        _bufR    = new float[_bufSize];
        SetParams(0.5f, 0.5f, 0.5f, 0.5f);
    }

    public void SetParams(float rateHz, float depth, float feedback, float send)
    {
        RateHz       = Math.Max(0.01f, rateHz);
        _lfoPhaseInc = RateHz / _sr;
        _depth       = Math.Clamp(depth,    0f, 1f);
        _feedback    = Math.Clamp(feedback, 0f, 0.95f);
        _send        = Math.Clamp(send,     0f, 1f);
    }

    public void SetBpmSync(float bpm, NoteValue note)
        => SetParams(note.ToHz(bpm), _depth, _feedback, _send);

    public void SetLfoWaveform(LfoWaveform w) => LfoWaveform = w;

    public void Process(Span<float> buffer, int channels)
    {
        if (!enabled) return;
        int   frames = buffer.Length / channels;
        bool  sqr    = LfoWaveform == LfoWaveform.Square;
        float cS     = (MIN_DELAY_S + MAX_DELAY_S) * 0.5f;
        float rS     = (MAX_DELAY_S - MIN_DELAY_S) * 0.5f * _depth;
        float steps  = 2f + (_stepRate / 100f) * 30f; // 2..32
        for (int f = 0; f < frames; f++) {
            int   idxL = f * channels;
            int   idxR = channels > 1 ? idxL + 1 : idxL;
            float inL  = buffer[idxL], inR = buffer[idxR];

            // LFO phase: quantise for Step mode, raw otherwise
            float phase = _stepMode == FlangerStepMode.Step
                ? MathF.Floor(_lfoPhase * steps) / steps
                : _lfoPhase;

            // Square gate (LfoWaveform.Square)
            float gate = sqr ? (_lfoPhase < 0.5f ? 1f : 0f) : 1f;
            float lfoL = sqr ? 1f : MathF.Sin(phase * TWO_PI);
            float lfoR = sqr ? 1f : MathF.Sin(phase * TWO_PI + 2.618f);
            _lfoPhase += _lfoPhaseInc;
            if (_lfoPhase >= 1f) _lfoPhase -= 1f;

            float wetL = ReadInterp(_bufL, _writeL, (cS + lfoL * rS) * _sr);
            float wetR = ReadInterp(_bufR, _writeR, (cS + lfoR * rS) * _sr);
            if (gate > 0f) { _bufL[_writeL] = inL + wetL * _feedback; _bufR[_writeR] = inR + wetR * _feedback; }
            else           { _bufL[_writeL] = 0f;                      _bufR[_writeR] = 0f; }
            _writeL = (_writeL + 1) % _bufSize;
            _writeR = (_writeR + 1) % _bufSize;

            switch (_stepMode) {
                case FlangerStepMode.Step:
                    buffer[idxL] = inL + wetL * _send;
                    buffer[idxR] = inR + wetR * _send;
                    break;
                case FlangerStepMode.Gate1: {
                    // Gate OFF = complete silence, matching Phaser Gate (Square LFO) reference.
                    bool g1 = _lfoPhase < 0.5f;
                    if (g1) {
                        buffer[idxL] = inL + wetL * _send;
                        buffer[idxR] = inR + wetR * _send;
                    } else {
                        // Gate OFF = complete silence. Clear delay buffer (matches Phaser Gate).
                        Array.Clear(_bufL); Array.Clear(_bufR);
                        buffer[idxL] = 0f;
                        buffer[idxR] = 0f;
                    }
                    break;
                }
                case FlangerStepMode.Gate2:
                    // abrupt L/R pan by raw LFO phase
                    float p2L = _lfoPhase < 0.5f ? 1f : 0f;
                    buffer[idxL] = inL + wetL * p2L * _send;
                    buffer[idxR] = inR + wetR * (1f - p2L) * _send;
                    break;
                case FlangerStepMode.Gate3:
                    // smooth L/R pan by raw LFO phase
                    float p3L = (MathF.Sin(_lfoPhase * TWO_PI) + 1f) * 0.5f;
                    buffer[idxL] = inL + wetL * p3L * _send;
                    buffer[idxR] = inR + wetR * (1f - p3L) * _send;
                    break;
                default:
                    buffer[idxL] = inL + wetL * _send * gate;
                    buffer[idxR] = inR + wetR * _send * gate;
                    break;
            }
        }
    }

    public void Process(float[] buffer, int offset, int count, int channels)
        => Process(buffer.AsSpan(offset, count), channels);

    public void Reset() { Array.Clear(_bufL); Array.Clear(_bufR); _lfoPhase = 0f; }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    float ReadInterp(float[] buf, int write, float delaySamples)
    {
        int d0 = (int)delaySamples; float frac = delaySamples - d0;
        int i0 = (write - d0 - 1 + _bufSize) % _bufSize;
        int i1 = (i0 - 1 + _bufSize) % _bufSize;
        return buf[i0] * (1f - frac) + buf[i1] * frac;
    }

    const float TWO_PI = MathF.PI * 2f;
}
