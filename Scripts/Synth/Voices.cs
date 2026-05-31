// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;

namespace Signo.Core.Synth;

/// <summary>Voice manager (polyphony). 32-voice pool with priority-based stealing.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public sealed class Voices {
    readonly Voice[] _voices;
    int   _max_voices;
    int   _sample_rate;
    float _current_bpm;
    float _filter_cutoff_base;
    float _filter_resonance_base;
    float _portamento_time;
    FilterKind _filter_mode;
    bool  _sustain_pedal_down;
    float _osc1_level = 1.0f;
    float _osc2_level = 0.5f;
    float _detune_cents = 0f;
    int   _osc1_octave = 0;
    int   _osc2_octave = 0;
    float _hpf_cutoff_hz = 0f;
    float _dca_level = 1.0f;
    float _filter_env_amount;
    EnvParams _filter_env_params = new EnvParams(0.01f, 0.3f, 0f, 0.2f);
    LfoParams _lfo1_params;
    LfoParams _lfo2_params;
    Lfo _lfo1;
    Lfo _lfo2;

    public int maxVoices    => _max_voices;
    public int activeVoices {
        get {
            int count = 0;
            for (int i = 0; i < _max_voices; i++)
                if (_voices[i].State != PlayState.Free) count++;
            return count;
        }
    }

    public Voices(int maxVoices = 32, int sampleRate = 44100) {
        if (maxVoices <= 0) maxVoices = 32;
        if (sampleRate <= 0) sampleRate = 44100;
        _voices = new Voice[maxVoices];
        _max_voices = maxVoices;
        _sample_rate = sampleRate;
        _current_bpm = 120f;
        _filter_cutoff_base = 0.7f;
        _filter_resonance_base = 0.0f;
        _portamento_time = 0f;
        _filter_mode = FilterKind.Roland;
        _sustain_pedal_down = false;
        _lfo1_params = new LfoParams(LfoWave.Sine, 1f, 0f, false, LfoTarget.None);
        _lfo2_params = new LfoParams(LfoWave.Sine, 1f, 0f, false, LfoTarget.None);
        _lfo1.Initialize(_lfo1_params, sampleRate, _current_bpm);
        _lfo2.Initialize(_lfo2_params, sampleRate, _current_bpm);
        // Initialize smoothers in each voice
        for (int i = 0; i < maxVoices; i++) {
            ref var v = ref _voices[i];
            v.VoiceIndex        = i;
            v.Osc1MasterLevel   = 1.0f;
            v.Osc2MasterLevel   = 0.5f;
            v.DcaLevel          = 1.0f;
            v.PitchEnvAmount    = 0f;
            v.FilterEnvAmount   = 0f;
            // Cutoff smoothing at 60Hz (~8ms settle): direct response without zipper noise.
            // Other params stay at 20Hz where slower smoothing is inaudible.
            v.SmoothedCutoff    = new Smoother(_filter_cutoff_base, 60f, sampleRate);
            v.SmoothedResonance = new Smoother(_filter_resonance_base, 20f, sampleRate);
            v.SmoothedAmpLevel  = new Smoother(1f, 20f, sampleRate);
            v.SmoothedPitchMod  = new Smoother(0f, 20f, sampleRate);
        }
    }

    public void NoteOn(in Note note, in OscParams osc1p, in OscParams osc2p,
        in EnvParams ampP, in EnvParams filterP, in EnvParams pitchP) {
        // Find a free voice first
        int idx = FindFreeVoice();
        if (idx < 0) {
            // Steal — try to find voice we can take
            idx = FindStealVictim(note.TrackId, note.Priority);
            if (idx < 0) return; // Could not steal
        }
        ref var v = ref _voices[idx];
        v.Osc1MasterLevel = _osc1_level;
        v.Osc2MasterLevel = _osc2_level;
        v.Osc1Octave = _osc1_octave;
        v.Osc2Octave = _osc2_octave;
        v.Hpf.SetCutoff(_hpf_cutoff_hz, _sample_rate);
        v.Hpf.Reset();
        v.SmoothedCutoff.SetTarget(_filter_cutoff_base);
        v.SmoothedResonance.SetTarget(_filter_resonance_base);
        v.NoteOn(note, osc1p, osc2p, ampP, _filter_env_params, pitchP, _portamento_time, _sample_rate);
        v.DcaLevel = _dca_level; // after NoteOn so the managed value wins over the fallback
    }

    public void NoteOff(int midiNote, int trackId) {
        // Release ALL matching voices (prevents Hanging Note)
        for (int i = 0; i < _max_voices; i++) {
            ref var v = ref _voices[i];
            if (v.State != PlayState.Free &&
                v.ActiveNote.MidiNote == midiNote &&
                v.ActiveNote.TrackId  == trackId) {
                if (_sustain_pedal_down) {
                    // Defer release — just mark key as not held
                    v.IsKeyHeld = false;
                } else {
                    v.NoteOff();
                }
            }
        }
    }

    public void AllNotesOff() {
        for (int i = 0; i < _max_voices; i++) {
            ref var v = ref _voices[i];
            if (v.State != PlayState.Free) v.NoteOff();
        }
    }

    public void SetMaxVoices(int newMax) {
        if (newMax < 1) newMax = 1;
        if (newMax > _voices.Length) newMax = _voices.Length;
        // If reducing, quick-release voices beyond new limit
        for (int i = newMax; i < _max_voices; i++) {
            ref var v = ref _voices[i];
            if (v.State != PlayState.Free) v.StartQuickRelease(_sample_rate);
        }
        _max_voices = newMax;
    }

    public void SetBPM(float bpm) {
        _current_bpm = bpm;
        _lfo1.SetBPM(bpm, _lfo1_params, _sample_rate);
        _lfo2.SetBPM(bpm, _lfo2_params, _sample_rate);
    }

    public void SetOscLevels(float osc1Level, float osc2Level, float detuneCents) {
        _osc1_level = osc1Level < 0f ? 0f : (osc1Level > 1f ? 1f : osc1Level);
        _osc2_level = osc2Level < 0f ? 0f : (osc2Level > 1f ? 1f : osc2Level);
        _detune_cents = detuneCents;
        // Apply immediately to all currently playing voices
        for (int i = 0; i < _max_voices; i++) {
            _voices[i].Osc1MasterLevel = _osc1_level;
            _voices[i].Osc2MasterLevel = _osc2_level;
            // Rebuild Osc2Params with new detune — OscParams is readonly struct
            ref var v = ref _voices[i];
            v.Osc2Params = new OscParams(v.Osc2Params.Wave, v.Osc2Params.Interp,
                detuneCents, v.Osc2Params.PulseWidth, v.Osc2Params.Level);
        }
    }

    /// <summary>Wave shape per oscillator (0..1, 0.5 = neutral). Applies to SAW/TRI/SIN.</summary>
    public void SetShape(float osc1Shape, float osc2Shape) {
        for (int i = 0; i < _max_voices; i++) {
            ref var v = ref _voices[i];
            v.Osc1Params = new OscParams(v.Osc1Params.Wave, v.Osc1Params.Interp,
                v.Osc1Params.DetuneCents, v.Osc1Params.PulseWidth, osc1Shape, v.Osc1Params.Level);
            v.Osc2Params = new OscParams(v.Osc2Params.Wave, v.Osc2Params.Interp,
                v.Osc2Params.DetuneCents, v.Osc2Params.PulseWidth, osc2Shape, v.Osc2Params.Level);
        }
    }

    /// <summary>Square pulse width per oscillator (0.01..0.99). Applies to live voices.</summary>
    public void SetPulseWidth(float osc1Pw, float osc2Pw) {
        for (int i = 0; i < _max_voices; i++) {
            ref var v = ref _voices[i];
            v.Osc1Params = new OscParams(v.Osc1Params.Wave, v.Osc1Params.Interp,
                v.Osc1Params.DetuneCents, osc1Pw, v.Osc1Params.Shape, v.Osc1Params.Level);
            v.Osc2Params = new OscParams(v.Osc2Params.Wave, v.Osc2Params.Interp,
                v.Osc2Params.DetuneCents, osc2Pw, v.Osc2Params.Shape, v.Osc2Params.Level);
        }
    }

    public void SetFilterEnv(float attack, float decay, float sustain, float release) {
        _filter_env_params = new EnvParams(attack, decay, sustain, release);
    }

    public void SetFilterEnvAmount(float amount) {
        if (amount < 0f) amount = 0f;
        if (amount > 1f) amount = 1f;
        _filter_env_amount = amount;
        for (int i = 0; i < _max_voices; i++) {
            _voices[i].FilterEnvAmount = amount;
        }
    }

    public void SetFilterParams(float cutoff, float resonance, FilterKind mode) {
        _filter_cutoff_base = cutoff;
        _filter_resonance_base = resonance;
        _filter_mode = mode;
        // Propagate filter mode to all voices (must update Voice.FilterMode field
        // because Voice.Tick reads its own FilterMode, not the manager's)
        for (int i = 0; i < _max_voices; i++) {
            _voices[i].FilterMode = mode;
        }
    }

    public void SetPortamentoTime(float seconds) => _portamento_time = seconds;

    public void SetSustainPedal(bool down) {
        bool was_down = _sustain_pedal_down;
        _sustain_pedal_down = down;
        if (was_down && !down) {
            // Pedal released — fire all deferred NoteOffs (voices with !IsKeyHeld)
            for (int i = 0; i < _max_voices; i++) {
                ref var v = ref _voices[i];
                if (v.State != PlayState.Free && !v.IsKeyHeld) {
                    v.NoteOff();
                }
            }
        }
    }

    public float GetVoiceCurrentCutoff(int midiNote, int trackId) {
        for (int i = 0; i < _max_voices; i++) {
            ref var v = ref _voices[i];
            if (v.State != PlayState.Free &&
                v.ActiveNote.MidiNote == midiNote &&
                v.ActiveNote.TrackId  == trackId) {
                return v.SmoothedCutoff.current;
            }
        }
        return -1f;
    }

    /// <summary>Test/observation: cutoff after env + LFO modulation (effective).</summary>
    public float GetVoiceEffectiveCutoff(int midiNote, int trackId) {
        for (int i = 0; i < _max_voices; i++) {
            ref var v = ref _voices[i];
            if (v.State != PlayState.Free &&
                v.ActiveNote.MidiNote == midiNote &&
                v.ActiveNote.TrackId  == trackId) {
                return v.effectiveCutoff;
            }
        }
        return -1f;
    }

    /// <summary>Test/observation: current portamento frequency of an active voice (-1 if none).</summary>
    public float GetVoiceCurrentFrequency(int midiNote, int trackId) {
        for (int i = 0; i < _max_voices; i++) {
            ref var v = ref _voices[i];
            if (v.State != PlayState.Free &&
                v.ActiveNote.MidiNote == midiNote &&
                v.ActiveNote.TrackId  == trackId) {
                return v.Portamento.currentFrequency;
            }
        }
        return -1f;
    }

    /// <summary>Per-oscillator octave (range): -1 = 16', 0 = 8', +1 = 4'.</summary>
    public void SetOscRange(int osc1Octave, int osc2Octave) {
        _osc1_octave = osc1Octave;
        _osc2_octave = osc2Octave;
        for (int i = 0; i < _max_voices; i++) {
            _voices[i].Osc1Octave = osc1Octave;
            _voices[i].Osc2Octave = osc2Octave;
        }
    }

    /// <summary>HPF cutoff in Hz (0 = off). Applied to all voices.</summary>
    public void SetHpfCutoff(float cutoffHz) {
        _hpf_cutoff_hz = cutoffHz;
        for (int i = 0; i < _max_voices; i++)
            _voices[i].Hpf.SetCutoff(cutoffHz, _sample_rate);
    }

    /// <summary>DCA master output level 0..1. Applied to all voices immediately.</summary>
    public void SetDcaLevel(float level) {
        _dca_level = level < 0f ? 0f : (level > 1f ? 1f : level);
        for (int i = 0; i < _max_voices; i++)
            _voices[i].DcaLevel = _dca_level;
    }

    /// <summary>Route LFO1 to filter cutoff with the given depth (0..1) and rate (Hz).</summary>
    public void SetLfoToCutoff(float depth, float rateHz) {
        _lfo1_params = new LfoParams(_lfo1_params.Wave, rateHz, depth,
            _lfo1_params.TempoSync, LfoTarget.FilterCutoff);
        _lfo1.Initialize(_lfo1_params, _sample_rate, _current_bpm);
    }

    /// <summary>Route LFO2 to amplitude with the given depth (0..1) and rate (Hz).</summary>
    public void SetLfoToAmp(float depth, float rateHz) {
        _lfo2_params = new LfoParams(_lfo2_params.Wave, rateHz, depth,
            _lfo2_params.TempoSync, LfoTarget.Amp);
        _lfo2.Initialize(_lfo2_params, _sample_rate, _current_bpm);
    }

    /// <summary>LFO waveform shape (applied to both LFO1 and LFO2).</summary>
    public void SetLfoWave(LfoWave wave) {
        _lfo1_params = new LfoParams(wave, _lfo1_params.RateOrSync, _lfo1_params.Depth,
            _lfo1_params.TempoSync, _lfo1_params.Destinations);
        _lfo2_params = new LfoParams(wave, _lfo2_params.RateOrSync, _lfo2_params.Depth,
            _lfo2_params.TempoSync, _lfo2_params.Destinations);
        _lfo1.Initialize(_lfo1_params, _sample_rate, _current_bpm);
        _lfo2.Initialize(_lfo2_params, _sample_rate, _current_bpm);
    }

    /// <summary>Test/observation: effective oscillator frequency (oscIndex 0 or 1; -1 if none).</summary>
    public float GetVoiceOscFrequency(int midiNote, int trackId, int oscIndex) {
        for (int i = 0; i < _max_voices; i++) {
            ref var v = ref _voices[i];
            if (v.State != PlayState.Free &&
                v.ActiveNote.MidiNote == midiNote &&
                v.ActiveNote.TrackId  == trackId) {
                return oscIndex == 0 ? v.currentOsc1Frequency : v.currentOsc2Frequency;
            }
        }
        return -1f;
    }

    /// <summary>Test/observation: whether an active voice is currently gliding (false if none).</summary>
    public bool GetVoiceIsGliding(int midiNote, int trackId) {
        for (int i = 0; i < _max_voices; i++) {
            ref var v = ref _voices[i];
            if (v.State != PlayState.Free &&
                v.ActiveNote.MidiNote == midiNote &&
                v.ActiveNote.TrackId  == trackId) {
                return v.Portamento.isGliding;
            }
        }
        return false;
    }

    public bool IsNoteActive(int midiNote, int trackId) {
        for (int i = 0; i < _max_voices; i++) {
            ref var v = ref _voices[i];
            // Active = Attack/Decay/Sustain (NOT Release / QuickRelease / Free)
            if (v.ActiveNote.MidiNote == midiNote &&
                v.ActiveNote.TrackId  == trackId &&
                (v.State == PlayState.Attack ||
                 v.State == PlayState.Decay  ||
                 v.State == PlayState.Sustain)) {
                return true;
            }
        }
        return false;
    }

    public void RenderSamples(Span<float> buffer, int channels) {
        // Zero buffer first
        buffer.Clear();
        if (channels < 1) channels = 1;
        int frames = buffer.Length / channels;
        // Master volume normalization: 1/8 = -18dB headroom for 8+ simultaneous notes
        const float MASTER_GAIN = 0.125f;
        // Compute LFO values once per buffer (not per sample, for performance)
        // For per-sample LFO modulation, would tick LFO inside the frame loop
        for (int f = 0; f < frames; f++) {
            float lfo1_out = _lfo1.Tick(_lfo1_params);
            float lfo2_out = _lfo2.Tick(_lfo2_params);
            float mix = 0f;
            for (int v = 0; v < _max_voices; v++) {
                ref var voice = ref _voices[v];
                if (voice.State == PlayState.Free) continue;
                mix += voice.Tick(lfo1_out, lfo2_out,
                    _filter_cutoff_base, _filter_resonance_base,
                    _lfo1_params, _lfo2_params);
            }
            mix *= MASTER_GAIN;
            // Soft clip to ensure [-1, 1]
            mix = Calc.TanhFast(mix);
            // Write to all channels (mono → stereo replication)
            for (int c = 0; c < channels; c++) {
                buffer[f * channels + c] = mix;
            }
        }
    }

    int FindFreeVoice() {
        for (int i = 0; i < _max_voices; i++) {
            if (_voices[i].State == PlayState.Free) return i;
        }
        return -1;
    }

    int FindStealVictim(int requestingTrack, int requestingPriority) {
        // Find lowest priority non-protected voice
        // Protected tracks (Drum/Perc) can only be stolen by same track
        var req_config = TrackConfig.GetConfig(requestingTrack);
        int victim_idx = -1;
        int lowest_priority = int.MaxValue;
        for (int i = 0; i < _max_voices; i++) {
            ref var v = ref _voices[i];
            int v_track = v.ActiveNote.TrackId;
            var v_config = TrackConfig.GetConfig(v_track);
            // Don't steal protected voices from other tracks
            if (v_config.Protected && v_track != requestingTrack) continue;
            // Pick lowest priority
            if (v.ActiveNote.Priority < lowest_priority) {
                lowest_priority = v.ActiveNote.Priority;
                victim_idx = i;
            }
        }
        if (victim_idx >= 0) {
            _voices[victim_idx].StartQuickRelease(_sample_rate);
        }
        return victim_idx;
    }
}
