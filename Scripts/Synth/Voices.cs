// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;

namespace Sinto.Core.Synth;

public sealed class Voices {
    private readonly Voice[]       _voices;
    private readonly TrackConfig[] _trackConfigs;
    private int   _maxVoices;
    private int   _sampleRate;
    private float _currentBpm;
    private float _filterCutoffBase;
    private float _filterResonanceBase;
    private float _portamentoTime;
    private FilterKind _filterMode;
    private bool  _sustainPedalDown;  // CC64 state

    public int MaxVoices    => _maxVoices;
    public int ActiveVoices => throw new NotImplementedException();

    public Voices(int maxVoices = 32, int sampleRate = 44100)
        => throw new NotImplementedException();

    // CRITICAL: _voices is a struct array. Always use `ref var v = ref _voices[i]`
    // NEVER `var v = _voices[i]` — that creates a copy; changes are silently discarded.
    // Bug scenario: StealVoice sets v.State = Free on a COPY → voice never released → pool exhausted.
    public void NoteOn(in Note note, in OscParams osc1p, in OscParams osc2p,
        in EnvParams ampP, in EnvParams filterP, in EnvParams pitchP)
        => throw new NotImplementedException();

    // Must release ALL voices matching (midiNote, trackId), not just the first one.
    // If the same note was re-triggered during a long release, multiple voices
    // may be playing the same pitch. Releasing only the first causes Hanging Notes.
    public void NoteOff(int midiNote, int trackId)
        => throw new NotImplementedException();

    public void AllNotesOff()
        => throw new NotImplementedException();

    public void SetMaxVoices(int newMax)
        => throw new NotImplementedException();

    public void SetBPM(float bpm)
        => throw new NotImplementedException();

    public void SetFilterParams(float cutoff, float resonance, FilterKind mode)
        => throw new NotImplementedException();

    public void SetPortamentoTime(float seconds)
        => throw new NotImplementedException();

    /// <summary>
    /// CC64 Sustain Pedal. When down (value >= 0.5), NoteOff transitions are deferred.
    /// When released (value < 0.5), all deferred NoteOffs fire immediately.
    /// </summary>
    public void SetSustainPedal(bool down)
        => throw new NotImplementedException();

    /// <summary>
    /// Returns the current SmoothedCutoff.Current for the active voice playing midiNote on trackId.
    /// Returns -1 if no matching voice found. For test verification of SnapToTarget wiring only.
    /// </summary>
    public float GetVoiceCurrentCutoff(int midiNote, int trackId)
        => throw new NotImplementedException();

    /// <summary>Returns true if the given midiNote on trackId is still active (not Free).</summary>
    public bool IsNoteActive(int midiNote, int trackId)
        => throw new NotImplementedException();

    // CLIPPING PREVENTION: sum of 32 voices × 1.0 = 32.0 — far exceeds [-1, 1].
    // RenderSamples MUST apply one of:
    //   (a) Master volume scaling: output *= (1.0f / activeVoices) or fixed factor
    //   (b) Soft clipper at output: output = TanhFast(output) maps ℝ → (-1, 1)
    // Without this, playing a chord causes catastrophic digital clipping.
    public void RenderSamples(Span<float> buffer, int channels)
        => throw new NotImplementedException();
}
