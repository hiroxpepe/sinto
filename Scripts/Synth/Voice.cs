// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System.Runtime.CompilerServices;
using Sinto.Core.Filter;

namespace Sinto.Core.Synth;

public struct Voice {
    public Note           ActiveNote;
    public VoiceState     State;
    public int            VoiceIndex;
    public OscillatorState Osc1;
    public OscillatorState Osc2;
    public EnvelopeState  AmpEnvelope;
    public EnvelopeState  FilterEnvelope;
    public EnvelopeState  PitchEnvelope;
    public FilterState    Filter;
    public SmoothedParameter SmoothedCutoff;
    public SmoothedParameter SmoothedResonance;
    public SmoothedParameter SmoothedAmpLevel;
    public SmoothedParameter SmoothedPitchMod;
    public int            QuickReleaseSamplesRemaining;
    // Preset parameters are shared across all voices via SintoEngine._activePreset.
    // DO NOT embed LFOParams/OscillatorParams/EnvelopeParams per-voice.
    // 32 voices × (LFOParams + OscillatorParams×2 + EnvelopeParams×3) wastes L1 cache.
    // Each voice only needs a reference index into the shared preset.
    // Parameters are read from SintoEngine._activePreset during Tick().
    // Exception: EnvelopeState is per-voice (runtime state, not preset data).
    // Temporarily kept for Phase 1 stub — Phase 2 refactors to preset reference.
    public OscillatorParams Osc1Params;   // TODO Phase 2: remove, read from preset
    public OscillatorParams Osc2Params;   // TODO Phase 2: remove, read from preset
    public EnvelopeParams AmpEnvParams;   // TODO Phase 2: remove, read from preset
    public EnvelopeParams FilterEnvParams;// TODO Phase 2: remove, read from preset
    public EnvelopeParams PitchEnvParams; // TODO Phase 2: remove, read from preset

    public float CurrentAmplitude => throw new System.NotImplementedException();

    public void NoteOn(in Note note, in OscillatorParams osc1p, in OscillatorParams osc2p,
        in EnvelopeParams ampP, in EnvelopeParams filterP, in EnvelopeParams pitchP,
        float portamentoTime, int sampleRate)
        => throw new System.NotImplementedException();

    public void NoteOff()
        => throw new System.NotImplementedException();

    public void StartQuickRelease(int sampleRate)
        => throw new System.NotImplementedException();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Tick(float lfo1Output, float lfo2Output,
        float filterCutoffBase, float filterResonanceBase,
        in LFOParams lfo1Params, in LFOParams lfo2Params)
        => throw new System.NotImplementedException();
}
