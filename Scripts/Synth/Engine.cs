// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Threading;
using Signo.Core.Audio;
using Signo.Core.Effects;

namespace Signo.Core.Synth;

/// <summary>Main synthesizer engine. Lock-free SPSC event queue + voice manager.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public sealed class Engine : IDisposable {
    readonly RingBuffer<Event> _event_queue;
    readonly Voices            _voices;
    readonly Scaler            _scaler;
    readonly int               _sample_rate;
    readonly int               _channels;
    int   _paused;
    float _current_bpm;
    long  _dsp_time_samples;
    WaveType _current_wave  = WaveType.Saw;
    WaveType _osc1_wave     = WaveType.Saw;
    WaveType _osc2_wave     = WaveType.Saw;
    // ── Arpeggiator (audio-thread driven) ───────────────────────────────
    readonly Arpeggiator _arp = new Arpeggiator();
    bool  _arp_enabled;
    float _arp_rate_hz = 120f; // BPM
    int   _arp_step_samples = 44100 * 60 / (120 * 4);
    int   _arp_counter;
    int   _current_arp_note = -1;
    int   _arp_track_id;
    bool  _arp_started;
    // ── Send/return FX (QY70-style shared busses) ───────────────────────
    readonly Reverb _reverb = new Reverb();
    readonly Chorus _chorus;
    readonly Delay  _delay;
    float _reverb_send;
    float _chorus_send;
    float _delay_send;
    float[] _fx_scratch = new float[0];
    float    _osc1_level     = 1.0f;
    float    _osc2_level     = 0.5f;
    float    _osc1_pw        = 0.5f;
    float    _osc2_pw        = 0.5f;
    float    _osc1_shape     = 0.5f;
    float    _osc2_shape     = 0.5f;
    float    _detune_cents   = 0f;
    EnvParams _current_amp_env = EnvParams.Default;

    public int   activeVoices     => _voices.activeVoices;
    public int   currentMaxVoices => _scaler.currentMaxVoices;
    public bool  isPaused         => Volatile.Read(ref _paused) != 0;
    public float currentBpm       => _current_bpm;
    public long  dspTimeSamples   => Volatile.Read(ref _dsp_time_samples);

    public Engine(int sampleRate = 44100, int channels = 2,
        int maxVoices = 32, int bufferSize = 1024) {
        if (sampleRate <= 0) sampleRate = 44100;
        if (channels <= 0)   channels = 2;
        if (maxVoices <= 0)  maxVoices = 32;
        if (bufferSize <= 0) bufferSize = 1024;
        // Round bufferSize to next power of 2
        int cap = 16;
        while (cap < bufferSize) cap <<= 1;
        _event_queue   = new RingBuffer<Event>(cap);
        _voices        = new Voices(maxVoices, sampleRate);
        _scaler        = new Scaler(_voices);
        _sample_rate   = sampleRate;
        _channels      = channels;
        _paused        = 0;
        _current_bpm   = 120f;
        _dsp_time_samples = 0L;
        _arp_step_samples = (int)(_sample_rate * (60f / _arp_rate_hz) / 4f);
        _chorus = new Chorus(_sample_rate);
        _delay  = new Delay(_sample_rate);
        // Send busses run full-wet; the send/return amount is applied by Engine.
        _reverb.mix = 1f; _reverb.enabled = true;
        _chorus.mix = 1f; _chorus.enabled = true;
        _delay.mix  = 1f; _delay.enabled  = true;
        Calc.Initialize();
    }

    public bool SendNoteOn(int midiNote, float velocity, int trackId,
        int priority, ushort offsetFrames) {
        return _event_queue.TryEnqueue(new Event(
            EventKind.NoteOn, offsetFrames, midiNote, velocity, trackId, priority));
    }

    public bool SendNoteOff(int midiNote, int trackId, ushort offsetFrames) {
        return _event_queue.TryEnqueue(new Event(
            EventKind.NoteOff, offsetFrames, midiNote, 0f, trackId, 0));
    }

    /// <summary>Set the waveform for both oscillators (audition / live tweaking).</summary>
    public void SetWave(WaveType wave) {
        _current_wave = wave;
        _osc1_wave = wave;
        _osc2_wave = wave;
    }

    /// <summary>Set per-oscillator waveforms (each may be a [Flags] stack of up to two).</summary>
    public void SetOscWaves(WaveType osc1Wave, WaveType osc2Wave) {
        if (osc1Wave == WaveType.None) osc1Wave = WaveType.Saw;
        if (osc2Wave == WaveType.None) osc2Wave = WaveType.Saw;
        _osc1_wave = osc1Wave;
        _osc2_wave = osc2Wave;
        _current_wave = osc1Wave;
    }

    /// <summary>Square-wave pulse width per oscillator (0.01..0.99, 0.5 = square).</summary>
    public void SetPulseWidth(float osc1Pw, float osc2Pw) {
        _osc1_pw = Clamp01(osc1Pw);
        _osc2_pw = Clamp01(osc2Pw);
        if (_osc1_pw < 0.01f) _osc1_pw = 0.01f; else if (_osc1_pw > 0.99f) _osc1_pw = 0.99f;
        if (_osc2_pw < 0.01f) _osc2_pw = 0.01f; else if (_osc2_pw > 0.99f) _osc2_pw = 0.99f;
        _voices.SetPulseWidth(_osc1_pw, _osc2_pw);
    }

    /// <summary>Wave shape per oscillator (0..1, 0.5 = neutral). SAW/TRI/SIN only.</summary>
    public void SetShape(float osc1Shape, float osc2Shape) {
        _osc1_shape = Clamp01(osc1Shape);
        _osc2_shape = Clamp01(osc2Shape);
        _voices.SetShape(_osc1_shape, _osc2_shape);
    }

    /// <summary>Portamento glide time in seconds (applied per-voice on NoteOn).</summary>
    public void SetPortamentoTime(float seconds) => _voices.SetPortamentoTime(seconds);

    /// <summary>Test/observation: current portamento frequency of an active voice.</summary>
    public float GetVoiceCurrentFrequency(int midiNote, int trackId)
        => _voices.GetVoiceCurrentFrequency(midiNote, trackId);

    /// <summary>Test/observation: whether an active voice is currently gliding.</summary>
    public bool GetVoiceIsGliding(int midiNote, int trackId)
        => _voices.GetVoiceIsGliding(midiNote, trackId);

    /// <summary>Set OSC levels and OSC2 detune.</summary>
    public void SetOscParams(float osc1Level, float osc2Level, float detuneCents) {
        _osc1_level   = osc1Level;
        _osc2_level   = osc2Level;
        _detune_cents = detuneCents;
        // Apply immediately to currently playing voices
        _voices.SetOscLevels(osc1Level, osc2Level, detuneCents);
    }

    /// <summary>Per-oscillator range (octave): -1 = 16', 0 = 8', +1 = 4'.</summary>
    public void SetOscRange(int osc1Octave, int osc2Octave)
        => _voices.SetOscRange(osc1Octave, osc2Octave);

    /// <summary>High-pass filter amount 0..100 (0 = off). Maps to ~20..3000 Hz.</summary>
    public void SetHpf(float amount0to100) {
        if (amount0to100 <= 0f) { _voices.SetHpfCutoff(0f); return; }
        float t = amount0to100 / 100f;
        // Log map 20 Hz .. 3000 Hz.
        float hz = 20f * MathF.Pow(3000f / 20f, t);
        _voices.SetHpfCutoff(hz);
    }

    /// <summary>Route the LFO to filter cutoff (depth 0..1, rate Hz).</summary>
    public void SetLfoToCutoff(float depth, float rateHz) => _voices.SetLfoToCutoff(depth, rateHz);

    /// <summary>Route the LFO to amplitude (depth 0..1, rate Hz).</summary>
    public void SetLfoToAmp(float depth, float rateHz) => _voices.SetLfoToAmp(depth, rateHz);

    /// <summary>LFO waveform shape (sine/triangle/square/S&amp;H).</summary>
    public void SetLfoWave(LfoWave wave) => _voices.SetLfoWave(wave);

    /// <summary>DCA master output level (0..1).</summary>
    public void SetDcaLevel(float level) => _voices.SetDcaLevel(level);

    /// <summary>Enable/disable the arpeggiator. Held notes drive it when on.</summary>
    public void SetArpEnabled(bool enabled) {
        if (_arp_enabled == enabled) return;
        _arp_enabled = enabled;
        // Stop any sounding arp note and reset the step counter on a mode change.
        if (_current_arp_note >= 0) {
            _voices.NoteOff(_current_arp_note, _arp_track_id);
            _current_arp_note = -1;
        }
        _arp_counter = 0;
        _arp_started = false;
    }

    /// <summary>Arpeggiator pattern mode.</summary>
    public void SetArpMode(ArpMode mode) => _arp.SetMode(mode);

    /// <summary>Arpeggiator tempo in BPM. One step = a sixteenth note.</summary>
    public void SetArpRate(float bpm) {
        if (bpm < 20f) bpm = 20f;
        _arp_rate_hz = bpm; // stored as BPM
        // step = sixteenth note = (60 / bpm) / 4 seconds
        float stepSeconds = (60f / bpm) / 4f;
        _arp_step_samples = (int)(_sample_rate * stepSeconds);
        if (_arp_step_samples < 1) _arp_step_samples = 1;
    }

    /// <summary>Test/observation: the MIDI note the arpeggiator is currently sounding (-1 none).</summary>
    public int currentArpNote => _current_arp_note;

    // ── Send/return FX API ──────────────────────────────────────────────
    /// <summary>Reverb send amount (0..1).</summary>
    public void SetReverbSend(float amount) => _reverb_send = Clamp01(amount);
    /// <summary>Chorus send amount (0..1).</summary>
    public void SetChorusSend(float amount) => _chorus_send = Clamp01(amount);
    /// <summary>Delay send amount (0..1).</summary>
    public void SetDelaySend(float amount) => _delay_send = Clamp01(amount);

    /// <summary>Reverb main params: room size and damping (0..1).</summary>
    public void SetReverbParams(float roomSize, float damping) {
        _reverb.roomSize = Clamp01(roomSize);
        _reverb.damping  = Clamp01(damping);
    }
    /// <summary>Chorus main params: rate (Hz) and depth (0..1).</summary>
    public void SetChorusParams(float rateHz, float depth) {
        _chorus.rate  = rateHz < 0.01f ? 0.01f : rateHz;
        _chorus.depth = Clamp01(depth);
    }
    /// <summary>Delay main params: time (seconds) and feedback (0..1).</summary>
    public void SetDelayParams(float timeSec, float feedback) {
        _delay.time     = timeSec < 0f ? 0f : timeSec;
        _delay.feedback = Clamp01(feedback);
    }

    static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);

    /// <summary>Test/observation: effective oscillator frequency (oscIndex 0 or 1).</summary>
    public float GetVoiceOscFrequency(int midiNote, int trackId, int oscIndex)
        => _voices.GetVoiceOscFrequency(midiNote, trackId, oscIndex);

    /// <summary>Test/observation: current (smoothed) filter cutoff of an active voice.</summary>
    public float GetVoiceCurrentCutoff(int midiNote, int trackId)
        => _voices.GetVoiceCurrentCutoff(midiNote, trackId);

    /// <summary>Test/observation: cutoff after env + LFO modulation.</summary>
    public float GetVoiceEffectiveCutoff(int midiNote, int trackId)
        => _voices.GetVoiceEffectiveCutoff(midiNote, trackId);

    /// <summary>Set filter cutoff/resonance for all voices.</summary>
    public void SetFilterParams(float cutoff, float resonance, FilterKind mode)
        => _voices.SetFilterParams(cutoff, resonance, mode);

    /// <summary>Set filter envelope amount (how much the filter envelope modulates cutoff).</summary>
    /// <summary>Set VCF dedicated filter envelope ADSR.</summary>
    public void SetFilterEnv(float attack, float decay, float sustain, float release)
        => _voices.SetFilterEnv(attack, decay, sustain, release);

    public void SetFilterEnvAmount(float amount) => _voices.SetFilterEnvAmount(amount);

    /// <summary>Set amp envelope ADSR for new NoteOn events.</summary>
    public void SetAmpEnv(float attack, float decay, float sustain, float release)
        => _current_amp_env = new EnvParams(attack, decay, sustain, release);

    public void Pause() {
        // Set _paused immediately for isPaused check
        Volatile.Write(ref _paused, 1);
    }
    public void Resume() {
        Volatile.Write(ref _paused, 0);
    }

    public void SetBPM(float bpm) {
        _current_bpm = bpm;
        _event_queue.TryEnqueue(new Event(EventKind.SetBPM, 0, 0, bpm, 0, 0));
    }

    public void RequestPresetSwap(object newPreset) {
        // Hot-swap preset reference via ring buffer event
        // (real impl would store via Interlocked.Exchange to _pending_preset)
        _event_queue.TryEnqueue(new Event(EventKind.SwapPreset));
    }

    public void ProcessAudioCallback(Span<float> buffer) {
        _scaler.OnCallbackBegin();
        int frames = buffer.Length / _channels;
        // Drain all events for this buffer
        int posFrames = 0;
        while (_event_queue.TryDequeue(out Event ev)) {
            int offsetFrames = ev.OffsetFrames;
            if (offsetFrames < posFrames) offsetFrames = posFrames;
            if (offsetFrames > frames)     offsetFrames = frames;
            int renderFrames = offsetFrames - posFrames;
            if (renderFrames > 0) {
                RenderRange(buffer.Slice(posFrames * _channels, renderFrames * _channels));
            }
            ApplyEvent(in ev);
            posFrames = offsetFrames;
        }
        int remaining = frames - posFrames;
        if (remaining > 0) {
            RenderRange(buffer.Slice(posFrames * _channels, remaining * _channels));
        }
        _scaler.OnCallbackEnd(frames, _sample_rate);
    }

    void RenderRange(Span<float> sub) {
        if (Volatile.Read(ref _paused) != 0) {
            sub.Clear();
            // DspTime does NOT advance while paused
            return;
        }
        if (_arp_enabled) AdvanceArp(sub.Length / _channels);
        _voices.RenderSamples(sub, _channels);
        ApplySendFx(sub);
        Interlocked.Add(ref _dsp_time_samples, sub.Length);
    }

    // QY70-style send/return: dry stays intact; a scaled copy is fed to each FX
    // bus (full-wet) and the wet result is summed back by the send amount.
    void ApplySendFx(Span<float> dry) {
        if (_reverb_send <= 0f && _chorus_send <= 0f && _delay_send <= 0f) return;
        int n = dry.Length;
        if (_fx_scratch.Length < n) _fx_scratch = new float[n];
        var scratch = _fx_scratch.AsSpan(0, n);

        SendBus(dry, scratch, _reverb, _reverb_send);
        SendBus(dry, scratch, _chorus, _chorus_send);
        SendBus(dry, scratch, _delay,  _delay_send);
    }

    void SendBus(Span<float> dry, Span<float> scratch, IEffect fx, float send) {
        if (send <= 0f) return;
        // copy dry * send into scratch, process full-wet, add the return to dry
        for (int i = 0; i < dry.Length; i++) scratch[i] = dry[i] * send;
        fx.Process(scratch, _channels);
        for (int i = 0; i < dry.Length; i++) dry[i] += scratch[i];
    }

    // Advance the arpeggiator by `frames`; on each step boundary stop the
    // previous note and start the next held note. Audio-thread only (SPSC safe).
    void AdvanceArp(int frames) {
        if (_arp.HeldCount == 0) {
            if (_current_arp_note >= 0) {
                _voices.NoteOff(_current_arp_note, _arp_track_id);
                _current_arp_note = -1;
            }
            _arp_counter = 0;
            return;
        }
        _arp_counter += frames;
        // Gate off at ~60% of the step so notes are articulated, not glued together.
        int gate = (_arp_step_samples * 3) / 5;
        if (_current_arp_note >= 0 && _arp_counter >= gate && _arp_counter < _arp_step_samples) {
            _voices.NoteOff(_current_arp_note, _arp_track_id);
            _current_arp_note = -1; // sounding handled; wait for next step to retrigger
        }
        // Step boundary: trigger the next held note.
        if (!_arp_started || _arp_counter >= _arp_step_samples) {
            _arp_counter = 0;
            _arp_started = true;
            if (_current_arp_note >= 0)
                _voices.NoteOff(_current_arp_note, _arp_track_id);
            int next = _arp.NextStep();
            if (next >= 0) {
                _voices.NoteOn(
                    new Note(next, 0.8f, _arp_track_id, 5),
                    new OscParams(_osc1_wave, pulseWidth: _osc1_pw, shape: _osc1_shape, level: _osc1_level),
                    new OscParams(_osc2_wave, detuneCents: _detune_cents, pulseWidth: _osc2_pw, shape: _osc2_shape, level: _osc2_level),
                    _current_amp_env, EnvParams.Default, EnvParams.Default);
                _current_arp_note = next;
            } else {
                _current_arp_note = -1;
            }
        }
    }

    void ApplyEvent(in Event ev) {
        switch (ev.Kind) {
            case EventKind.None:
                return; // explicit no-op, prevent phantom NoteOn
            case EventKind.NoteOn:
                if (_arp_enabled) {
                    _arp.NoteOn(ev.IntParam);
                    _arp_track_id = ev.TrackId;
                } else {
                    _voices.NoteOn(
                        new Note(ev.IntParam, ev.FloatParam, ev.TrackId, ev.Priority),
                        new OscParams(_osc1_wave, pulseWidth: _osc1_pw, shape: _osc1_shape, level: _osc1_level),
                        new OscParams(_osc2_wave, detuneCents: _detune_cents, pulseWidth: _osc2_pw, shape: _osc2_shape, level: _osc2_level),
                        _current_amp_env, EnvParams.Default, EnvParams.Default);
                }
                break;
            case EventKind.NoteOff:
                if (_arp_enabled) {
                    _arp.NoteOff(ev.IntParam);
                } else {
                    _voices.NoteOff(ev.IntParam, ev.TrackId);
                }
                break;
            case EventKind.Pause:
                Volatile.Write(ref _paused, 1);
                break;
            case EventKind.Resume:
                Volatile.Write(ref _paused, 0);
                break;
            case EventKind.SetVoiceLimit:
                _scaler.ForceSetTier(ev.IntParam);
                break;
            case EventKind.SwapPreset:
                // Preset swap handled by Engine state owner via Interlocked.Exchange
                break;
            case EventKind.SetBPM:
                _current_bpm = ev.FloatParam;
                _voices.SetBPM(ev.FloatParam);
                break;
            case EventKind.SustainPedal:
                _voices.SetSustainPedal(ev.FloatParam >= 0.5f);
                break;
        }
    }

    public void Dispose() {
        // Drain remaining events
        while (_event_queue.TryDequeue(out _)) { }
        _voices.AllNotesOff();
    }
}
