// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System.Runtime.CompilerServices;
using Sinto.Core.Synth;

namespace Sinto.Core.Synth;

public struct Voice {
    public Note           ActiveNote;
    public PlayState     State;
    public int            VoiceIndex;
    public Oscillator Osc1;
    public Oscillator Osc2;
    public Envelope  AmpEnvelope;
    public Envelope  FilterEnvelope;
    public Envelope  PitchEnvelope;
    public Filter    Filter;
    public Smoother SmoothedCutoff;
    public Smoother SmoothedResonance;
    public Smoother SmoothedAmpLevel;
    public Smoother SmoothedPitchMod;
    public Portamento Portamento;
    public int            QuickReleaseSamplesRemaining;
    // IsKeyHeld: true = physical key is still pressed.
    // Required for correct Sustain Pedal behavior:
    //   NoteOff while pedal down → IsKeyHeld = false, voice stays sustained.
    //   Pedal release → only voices with IsKeyHeld=false transition to Release.
    //   Without this flag, releasing pedal kills notes that are still physically held.
    public bool IsKeyHeld;
    // Preset parameters are shared across all voices via Engine._activePreset.
    // DO NOT embed LfoParams/OscParams/EnvParams per-voice.
    // 32 voices × (LfoParams + OscParams×2 + EnvParams×3) wastes L1 cache.
    // Each voice only needs a reference index into the shared preset.
    // Parameters are read from Engine._activePreset during Tick().
    // Exception: Envelope is per-voice (runtime state, not preset data).
    // Temporarily kept for Phase 1 stub — Phase 2 refactors to preset reference.
    public OscParams Osc1Params;   // TODO Phase 2: remove, read from preset
    public OscParams Osc2Params;   // TODO Phase 2: remove, read from preset
    public EnvParams AmpEnvParams;   // TODO Phase 2: remove, read from preset
    public EnvParams FilterEnvParams;// TODO Phase 2: remove, read from preset
    public EnvParams PitchEnvParams; // TODO Phase 2: remove, read from preset

    public float CurrentAmplitude => throw new System.NotImplementedException();

    public void NoteOn(in Note note, in OscParams osc1p, in OscParams osc2p,
        in EnvParams ampP, in EnvParams filterP, in EnvParams pitchP,
        float portamentoTime, int sampleRate)
        => throw new System.NotImplementedException();

    public void NoteOff()
        => throw new System.NotImplementedException();

    public void StartQuickRelease(int sampleRate)
        => throw new System.NotImplementedException();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Tick(float lfo1Output, float lfo2Output,
        float filterCutoffBase, float filterResonanceBase,
        in LfoParams lfo1Params, in LfoParams lfo2Params)
        => throw new System.NotImplementedException();
}
