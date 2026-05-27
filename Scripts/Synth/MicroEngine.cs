// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using Sinto.Core.Audio;
using Sinto.Core.Effects;

namespace Sinto.Core.Synth;

/// <summary>Lightweight single-voice SFX processor. Mono → stereo duplicate.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public sealed class MicroEngine {
#nullable enable
    // Minimal SPSC (4 slots) — prevents main/audio thread data race on Voice struct.
    // Main thread: NoteOn/NoteOff → enqueue event.
    // Audio thread: RenderMono → dequeue and apply events.
    private readonly RingBuffer<Event> _events;
    private Voice  _voice;
    private readonly Chorus _chorus;
    private readonly Retro  _retro;
    private readonly int    _sample_rate;
    private OscParams _osc1_params;
    private OscParams _osc2_params;
    private EnvParams _amp_params;
    private readonly LfoParams _lfo_silent;  // no modulation for SFX

    public bool IsActive => _voice.State != PlayState.Free;

    public MicroEngine(int sample_rate = 44100) {
        if (sample_rate <= 0) sample_rate = 44100;
        _events      = new RingBuffer<Event>(4);
        _chorus      = new Chorus(sample_rate);
        _retro       = new Retro();
        _sample_rate = sample_rate;
        _lfo_silent  = new LfoParams(LfoWave.Sine, 1f, 0f, false, LfoTarget.None);
        _osc1_params = new OscParams(WaveType.Sine);
        _osc2_params = new OscParams(WaveType.Sine);
        _amp_params  = EnvParams.Default;
        _voice.SmoothedCutoff    = new Smoother(0.7f, 20f, sample_rate);
        _voice.SmoothedResonance = new Smoother(0.0f, 20f, sample_rate);
        _voice.SmoothedAmpLevel  = new Smoother(1.0f, 20f, sample_rate);
        _voice.SmoothedPitchMod  = new Smoother(0.0f, 20f, sample_rate);
        _voice.FilterMode        = FilterKind.Roland;
        Calc.Initialize();
    }

    /// <summary>Main thread: enqueue NoteOn event (SPSC-safe).</summary>
    public void NoteOn(int midiNote, float velocity, in OscParams osc1,
        in OscParams osc2, in EnvParams amp) {
        _osc1_params = osc1;
        _osc2_params = osc2;
        _amp_params  = amp;
        _events.TryEnqueue(new Event(EventKind.NoteOn, 0, midiNote, velocity));
    }

    /// <summary>Main thread: enqueue NoteOff event (SPSC-safe).</summary>
    public void NoteOff() {
        _events.TryEnqueue(new Event(EventKind.NoteOff));
    }

    /// <summary>
    /// Audio thread: drain events then render.
    /// Mono output duplicated to all channels.
    /// </summary>
    public void RenderMono(Span<float> buffer, int channels) {
        if (channels < 1) channels = 1;
        // Drain SPSC queue — apply all pending events before rendering
        while (_events.TryDequeue(out Event ev)) {
            switch (ev.Kind) {
                case EventKind.NoteOn:
                    var note = new Note(ev.IntParam, ev.FloatParam, 0, 5);
                    _voice.NoteOn(note, _osc1_params, _osc2_params,
                        _amp_params, EnvParams.Default, EnvParams.Default,
                        0f, _sample_rate);
                    break;
                case EventKind.NoteOff:
                    _voice.NoteOff();
                    break;
            }
        }
        int frames = buffer.Length / channels;
        for (int f = 0; f < frames; f++) {
            float sample = 0f;
            if (_voice.State != PlayState.Free) {
                sample = _voice.Tick(0f, 0f, 0.7f, 0.0f, _lfo_silent, _lfo_silent);
                sample *= 0.5f;
                sample = Calc.TanhFast(sample);
            }
            // Duplicate mono sample to all channels
            for (int c = 0; c < channels; c++) {
                buffer[f * channels + c] = sample;
            }
        }
    }
}
