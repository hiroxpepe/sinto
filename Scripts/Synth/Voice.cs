// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Runtime.CompilerServices;

namespace Signo.Core.Synth;

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
    public float Osc1MasterLevel;
    public float Osc2MasterLevel;
    public float FilterEnvAmount; // 0 = no env modulation on cutoff
    public float PitchEnvAmount;  // 0 = no env modulation on pitch (default)
    int  _sample_rate;
    long _sample_index;
    // Frequency cache — only call SetFrequency when freq changes by meaningful amount
    float _current_freq1;
    float _current_freq2;

    public float currentAmplitude => AmpEnvelope.level;

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
        float freq = Portamento.currentFrequency;
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
        // Reset filter state — prevents previous note's residue affecting attack
        Filter.Reset();
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
        // ── Smoother target update ───────────────────────────────────────
        // SmoothedCutoff operates in [0,1] log-Hz space (Δ0.1 = 1 octave, perceptually uniform).
        SmoothedCutoff.SetTarget(filterCutoffBase);
        SmoothedResonance.SetTarget(filterResonanceBase);
        // ── Portamento ──────────────────────────────────────────────────
        float freq = Portamento.Tick();
        if (freq < 1f) freq = ActiveNote.FrequencyHz;
        // ── Pitch: Envelope and LFO ─────────────────────────────────────
        // PitchEnvelope: always tick, scaled by PitchEnvAmount.
        // PitchEnvAmount=0 (default) = no pitch modulation = no pitch drop on NoteOff
        float pitch_env = PitchEnvelope.Tick() * PitchEnvAmount;
        float lfo1_pitch = (lfo1Params.Destinations & LfoTarget.OSC1Pitch) != 0
                           ? lfo1Output * lfo1Params.Depth * 2f : 0f;
        float lfo2_pitch = (lfo2Params.Destinations & LfoTarget.OSC2Pitch) != 0
                           ? lfo2Output * lfo2Params.Depth * 2f : 0f;
        float total_pitch = pitch_env + lfo1_pitch + lfo2_pitch;
        // OSC frequency — only call SetFrequency when ΔHz > 0.05 (skip micro-changes)
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
        float o1  = Osc1.Tick(p1) * Osc1MasterLevel;
        float o2  = Osc2.Tick(Osc2Params) * Osc2MasterLevel;
        float mix = (o1 + o2) * 0.5f;
        // ── Filter ──────────────────────────────────────────────────────
        float filter_env = FilterEnvelope.Tick();
        float cutoff    = SmoothedCutoff.Tick();
        float resonance = SmoothedResonance.Tick();
        // Modulate cutoff via Envelope / LFO (in [0,1] space)
        // NOTE: filter_env amount controlled by FilterEnvAmount field (0 = no modulation)
        cutoff += filter_env * FilterEnvAmount;
        if ((lfo1Params.Destinations & LfoTarget.FilterCutoff) != 0)
            cutoff += lfo1Output * lfo1Params.Depth * 0.3f;
        if ((lfo2Params.Destinations & LfoTarget.FilterCutoff) != 0)
            cutoff += lfo2Output * lfo2Params.Depth * 0.3f;
        if      (cutoff < 0.001f) cutoff = 0.001f;
        else if (cutoff > 0.999f) cutoff = 0.999f;
        // Call SetParams every sample — Smoother already ensures smooth changes
        // No threshold gate needed (a gate would cause zipper noise)
        Filter.SetParams(cutoff, resonance, FilterMode, sr);
        float filtered = Filter.Process(mix, _sample_index);
        // ── Amp ─────────────────────────────────────────────────────────
        float amp = AmpEnvelope.Tick();
        if ((lfo1Params.Destinations & LfoTarget.Amp) != 0)
            amp *= 1f - lfo1Params.Depth * 0.5f * (1f + lfo1Output);
        if (amp < 0f) amp = 0f;
        if (AmpEnvelope.isDone) State = PlayState.Free;
        _sample_index++;
        return filtered * amp * ActiveNote.Velocity;
    }
}
