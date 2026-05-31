// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Runtime.CompilerServices;

namespace Signo.Core.Effects;

/// <summary>
/// TR-2 style tremolo: LFO modulates amplitude.
/// Triangle: smooth swell. Square: hard on/off gate.
/// IInsertEffect: buffer[i] = dry * (1 - depth * gain_mod) + wet * Send.
/// MusicDSP ref: amplitude modulation with bipolar LFO (-1..+1) → gain = 1 - depth*(1-lfo)*0.5
/// </summary>
public sealed class Tremolo : IInsertEffect
{
    readonly int _sr;
    float _lfoPhase, _lfoPhaseInc;
    float _depth, _send;
    TremoloWaveform _wave = TremoloWaveform.Triangle;

    public float           RateHz   { get; private set; }
    public bool            enabled  { get; set; } = true;
    public float           Send     { get => _send; set => _send = Math.Clamp(value, 0f, 1f); }
    public TremoloWaveform Waveform => _wave;

    public Tremolo(int sampleRate)
    {
        _sr = sampleRate;
        SetParams(4f, 0.5f);
    }

    public void SetParams(float rateHz, float depth)
    {
        RateHz       = Math.Max(0.01f, rateHz);
        _lfoPhaseInc = RateHz / _sr;
        _depth       = Math.Clamp(depth, 0f, 1f);
    }

    public void SetWaveform(TremoloWaveform w) => _wave = w;

    public void SetBpmSync(float bpm, NoteValue note)
        => SetParams(note.ToHz(bpm), _depth);

    public void Process(Span<float> buffer, int channels)
    {
        if (!enabled) return;
        int frames = buffer.Length / channels;
        for (int f = 0; f < frames; f++) {
            int idxL = f * channels, idxR = channels > 1 ? idxL + 1 : idxL;
            // LFO: triangle = smooth, square = hard gate
            float lfo = _wave == TremoloWaveform.Square
                ? (_lfoPhase < 0.5f ? 1f : -1f)
                : (1f - 4f * MathF.Abs(_lfoPhase - 0.5f)); // triangle -1..+1
            _lfoPhase += _lfoPhaseInc;
            if (_lfoPhase >= 1f) _lfoPhase -= 1f;
            // gain = 1 - depth*(1-lfo)*0.5  →  ranges from (1-depth) to 1
            float gain = 1f - _depth * (1f - lfo) * 0.5f;
            gain = Math.Max(0f, gain);
            float inL = buffer[idxL], inR = buffer[idxR];
            // Insert: dry * gain, wet contribution via Send
            buffer[idxL] = inL * (1f - _send) + inL * gain * _send;
            buffer[idxR] = inR * (1f - _send) + inR * gain * _send;
        }
    }

    public void Process(float[] buffer, int offset, int count, int channels)
        => Process(buffer.AsSpan(offset, count), channels);

    public void Reset() => _lfoPhase = 0f;
}
