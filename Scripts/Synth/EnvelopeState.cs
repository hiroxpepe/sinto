// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System.Runtime.CompilerServices;

namespace Sinto.Core.Synth;

public struct EnvelopeState {
    private float      _level;
    private VoiceState _phase;
    private float      _attackRate;
    private float      _decayRate;
    private float      _releaseRate;
    private float      _sustainLevel;
    private float      _quickReleaseRate;

    public float      Level  => _level;
    public VoiceState Phase  => _phase;
    public bool       IsDone => throw new System.NotImplementedException();

    // Clamp here as last line of defense against default(EnvelopeParams) {0,0,0,0}.
    // default(EnvelopeParams) bypasses the constructor, setting all fields to 0.0f.
    // Without this clamp, rate = 1.0 / (0.0 * sampleRate) = NaN → silent audio thread.
    public void NoteOn(in EnvelopeParams p, int sampleRate)
        => throw new System.NotImplementedException();

    public void NoteOff()
        => throw new System.NotImplementedException();

    public void StartQuickRelease(int sampleRate)
        => throw new System.NotImplementedException();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Tick()
        => throw new System.NotImplementedException();
}
