// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;

namespace Sinto.Core.Synth;

/// <summary>Voice manager (polyphony). 32-voice pool with priority-based stealing.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public sealed class Voices {
#nullable enable
    private readonly Voice[] _voices;
    private int   _max_voices;
    private int   _sample_rate;
    private float _current_bpm;
    private float _filter_cutoff_base;
    private float _filter_resonance_base;
    private float _portamento_time;
    private FilterKind _filter_mode;
    private bool  _sustain_pedal_down;
    private float _osc1_level = 1.0f;
    private float _osc2_level = 0.5f;
    private float _detune_cents = 0f;
    private float _filter_env_amount;
    private EnvParams _filter_env_params = new EnvParams(0.01f, 0.3f, 0f, 0.2f);
    private LfoParams _lfo1_params;
    private LfoParams _lfo2_params;
    private Lfo _lfo1;
    private Lfo _lfo2;

    public int MaxVoices    => _max_voices;
    public int ActiveVoices {
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
            v.PitchEnvAmount    = 0f;
            v.FilterEnvAmount   = 0f;
            v.SmoothedCutoff    = new Smoother(_filter_cutoff_base, 20f, sampleRate);
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
        // Set filter base via voice's smoother
        v.SmoothedCutoff.SetTarget(_filter_cutoff_base);
        v.SmoothedResonance.SetTarget(_filter_resonance_base);
        v.NoteOn(note, osc1p, osc2p, ampP, _filter_env_params, pitchP, _portamento_time, _sample_rate);
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
        // 鳴っている全ボイスに即時反映
        for (int i = 0; i < _max_voices; i++) {
            _voices[i].Osc1MasterLevel = _osc1_level;
            _voices[i].Osc2MasterLevel = _osc2_level;
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
                return v.SmoothedCutoff.Current;
            }
        }
        return -1f;
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

    private int FindFreeVoice() {
        for (int i = 0; i < _max_voices; i++) {
            if (_voices[i].State == PlayState.Free) return i;
        }
        return -1;
    }

    private int FindStealVictim(int requestingTrack, int requestingPriority) {
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
