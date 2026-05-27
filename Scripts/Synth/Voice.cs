// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System.Runtime.CompilerServices;

namespace Sinto.Core.Synth;

/// <summary>Single synthesizer voice. Struct — use ref var when mutating array elements.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public struct Voice {
#nullable enable
    public Note       ActiveNote;
    public PlayState  State;
    public int        VoiceIndex;
    public Oscillator Osc1;
    public Oscillator Osc2;
    public Envelope   AmpEnvelope;
    public Envelope   FilterEnvelope;
    public Envelope   PitchEnvelope;
    public Filter     Filter;
    public Smoother   SmoothedCutoff;
    public Smoother   SmoothedResonance;
    public Smoother   SmoothedAmpLevel;
    public Smoother   SmoothedPitchMod;
    public Portamento Portamento;
    public int        QuickReleaseSamplesRemaining;
    public bool       IsKeyHeld;
    public OscParams  Osc1Params;
    public OscParams  Osc2Params;
    public EnvParams  AmpEnvParams;
    public EnvParams  FilterEnvParams;
    public EnvParams  PitchEnvParams;
    public FilterKind FilterMode;
    private int  _sample_rate;
    private long _sample_index;
    // Frequency cache — only call SetFrequency when freq changes by meaningful amount
    private float _current_freq1;
    private float _current_freq2;

    public float CurrentAmplitude => AmpEnvelope.Level;

    public void NoteOn(in Note note, in OscParams osc1p, in OscParams osc2p,
        in EnvParams ampP, in EnvParams filterP, in EnvParams pitchP,
        float portamentoTime, int sampleRate) {
        if (sampleRate <= 0) sampleRate = 44100;
        _sample_rate    = sampleRate;
        ActiveNote      = note;
        State           = PlayState.Attack;
        IsKeyHeld       = true;
        Osc1Params      = osc1p;
        Osc2Params      = osc2p;
        AmpEnvParams    = ampP;
        FilterEnvParams = filterP;
        PitchEnvParams  = pitchP;
        // Portamento
        Portamento.SetTarget(note.FrequencyHz, portamentoTime, sampleRate);
        if (portamentoTime <= 0f) Portamento.SnapToTarget();
        float freq = Portamento.CurrentFrequency;
        if (freq < 1f) freq = note.FrequencyHz;
        float freq2 = freq * Calc.PitchRatioFast(osc2p.DetuneCents / 100f);
        Osc1.SetFrequency(freq, sampleRate);
        Osc2.SetFrequency(freq2, sampleRate);
        _current_freq1 = freq;
        _current_freq2 = freq2;
        // Trigger envelopes
        AmpEnvelope.NoteOn(ampP, sampleRate);
        FilterEnvelope.NoteOn(filterP, sampleRate);
        PitchEnvelope.NoteOn(pitchP, sampleRate);
        // Snap smoothers — prevents "pyun" artifact on voice steal
        SmoothedCutoff.SnapToTarget();
        SmoothedResonance.SnapToTarget();
        SmoothedAmpLevel.SnapToTarget();
        SmoothedPitchMod.SnapToTarget();
        _sample_index = 0L;
    }

    public void NoteOff() {
        AmpEnvelope.NoteOff();
        FilterEnvelope.NoteOff();
        PitchEnvelope.NoteOff();
        State     = PlayState.Release;
        IsKeyHeld = false;
    }

    public void StartQuickRelease(int sampleRate) {
        AmpEnvelope.StartQuickRelease(sampleRate);
        State = PlayState.QuickRelease;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Tick(float lfo1Output, float lfo2Output,
        float filterCutoffBase, float filterResonanceBase,
        in LfoParams lfo1Params, in LfoParams lfo2Params) {
        if (State == PlayState.Free) return 0f;
        int sr = _sample_rate > 0 ? _sample_rate : 44100;

        // ── Smoother ターゲット更新 ──────────────────────────────────────
        // ここが重要: ノブ値を Smoother に毎サンプル設定する
        // Smoother が IIR で滑らかに追従 → ジッパーノイズなし
        SmoothedCutoff.SetTarget(filterCutoffBase);
        SmoothedResonance.SetTarget(filterResonanceBase);

        // ── Portamento ──────────────────────────────────────────────────
        float freq = Portamento.Tick();
        if (freq < 1f) freq = ActiveNote.FrequencyHz;

        // ── Pitch: EnvelopeとLFO ────────────────────────────────────────
        float pitch_env  = PitchEnvelope.Tick();
        float lfo1_pitch = (lfo1Params.Destinations & LfoTarget.OSC1Pitch) != 0
                           ? lfo1Output * lfo1Params.Depth * 2f : 0f;
        float lfo2_pitch = (lfo2Params.Destinations & LfoTarget.OSC2Pitch) != 0
                           ? lfo2Output * lfo2Params.Depth * 2f : 0f;
        float total_pitch = pitch_env * 0.5f + lfo1_pitch + lfo2_pitch;

        // OSC frequency — SetFrequency は ΔHz > 0.05Hz のときのみ（マイクロ変化で毎サンプル呼ばない）
        float freq1 = freq * Calc.PitchRatioFast(total_pitch + Osc1Params.DetuneCents / 100f);
        float freq2 = freq * Calc.PitchRatioFast(total_pitch + Osc2Params.DetuneCents / 100f);
        if (freq1 < 1f) freq1 = 1f;
        if (freq2 < 1f) freq2 = 1f;
        float df1 = freq1 - _current_freq1; if (df1 < 0f) df1 = -df1;
        float df2 = freq2 - _current_freq2; if (df2 < 0f) df2 = -df2;
        if (df1 > 0.05f) { Osc1.SetFrequency(freq1, sr); _current_freq1 = freq1; }
        if (df2 > 0.05f) { Osc2.SetFrequency(freq2, sr); _current_freq2 = freq2; }

        // ── Oscillators ─────────────────────────────────────────────────
        OscParams p1 = Osc1Params;
        if ((lfo1Params.Destinations & LfoTarget.OSC1PWM) != 0) {
            float pw = p1.PulseWidth + lfo1Output * lfo1Params.Depth * 0.4f;
            if      (pw < 0.05f) pw = 0.05f;
            else if (pw > 0.95f) pw = 0.95f;
            p1 = new OscParams(p1.Wave, p1.Interp, p1.DetuneCents, pw, p1.Level);
        }
        float o1  = Osc1.Tick(p1);
        float o2  = Osc2.Tick(Osc2Params);
        float mix = (o1 + o2) * 0.5f;

        // ── Filter ──────────────────────────────────────────────────────
        float filter_env = FilterEnvelope.Tick();
        // Smoother を Tick して現在値取得（毎サンプル滑らかに変化）
        float cutoff    = SmoothedCutoff.Tick();
        float resonance = SmoothedResonance.Tick();
        // Envelope / LFO でカットオフを変調
        cutoff += filter_env * 0.5f;
        if ((lfo1Params.Destinations & LfoTarget.FilterCutoff) != 0)
            cutoff += lfo1Output * lfo1Params.Depth * 0.3f;
        if ((lfo2Params.Destinations & LfoTarget.FilterCutoff) != 0)
            cutoff += lfo2Output * lfo2Params.Depth * 0.3f;
        if      (cutoff < 0.001f) cutoff = 0.001f;
        else if (cutoff > 0.999f) cutoff = 0.999f;
        // SetParams は毎サンプル呼ぶ — Smoother が既に滑らかにしてくれてるため
        // 閾値ゲートは不要（= ジッパーノイズの原因になる）
        Filter.SetParams(cutoff, resonance, FilterMode, sr);
        float filtered = Filter.Process(mix, _sample_index);

        // ── Amp ─────────────────────────────────────────────────────────
        float amp = AmpEnvelope.Tick();
        if ((lfo1Params.Destinations & LfoTarget.Amp) != 0)
            amp *= 1f - lfo1Params.Depth * 0.5f * (1f + lfo1Output);
        if (amp < 0f) amp = 0f;
        if (AmpEnvelope.IsDone) State = PlayState.Free;

        _sample_index++;
        return filtered * amp * ActiveNote.Velocity;
    }
}
