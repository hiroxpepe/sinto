// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Threading;
using Signo.Core.Audio;

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
    float    _osc1_level     = 1.0f;
    float    _osc2_level     = 0.5f;
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

    /// <summary>Set the waveform used for new NoteOn events (audition / live tweaking).</summary>
    public void SetWave(WaveType wave) => _current_wave = wave;

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
        _voices.RenderSamples(sub, _channels);
        Interlocked.Add(ref _dsp_time_samples, sub.Length);
    }

    void ApplyEvent(in Event ev) {
        switch (ev.Kind) {
            case EventKind.None:
                return; // explicit no-op, prevent phantom NoteOn
            case EventKind.NoteOn:
                _voices.NoteOn(
                    new Note(ev.IntParam, ev.FloatParam, ev.TrackId, ev.Priority),
                    new OscParams(_current_wave, level: _osc1_level),
                    new OscParams(_current_wave, detuneCents: _detune_cents, level: _osc2_level),
                    _current_amp_env, EnvParams.Default, EnvParams.Default);
                break;
            case EventKind.NoteOff:
                _voices.NoteOff(ev.IntParam, ev.TrackId);
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
