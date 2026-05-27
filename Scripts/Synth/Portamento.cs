// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System.Runtime.CompilerServices;

namespace Sinto.Core.Synth;

public struct Portamento
{
    private float _currentFreq;
    private float _targetFreq;
    private float _rate; // frequency change per sample

    public float CurrentFrequency { get; }
    public bool  IsGliding        { get; }

    /// <summary>Set glide target. time=0 → SnapToTarget.</summary>
    public void SetTarget(float targetFreqHz, float timeSeconds, int sampleRate)
        => throw new System.NotImplementedException();

    /// <summary>Instant jump — no glide. Call on NoteOn when portamento=0.</summary>
    public void SnapToTarget()
        => throw new System.NotImplementedException();

    /// <summary>Advance one sample. Returns current frequency.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Tick()
        => throw new System.NotImplementedException();
}
