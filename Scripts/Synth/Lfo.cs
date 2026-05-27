// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System.Runtime.CompilerServices;

namespace Sinto.Core.Synth;

public struct Lfo
{
    private double _phase;
    private double _phaseInc;
    private float  _shValue;       // Current S&H held value
    private float  _shPrevPhase;   // For S&H trigger detection
    private float  _currentBpm;

    public void Initialize(in LfoParams p, int sampleRate, float bpm = 120f)
        => throw new System.NotImplementedException();

    /// <summary>Update BPM. Recalculate _phaseInc if TempoSync is on.</summary>
    public void SetBPM(float bpm, in LfoParams p, int sampleRate)
        => throw new System.NotImplementedException();

    /// <summary>
    /// Advance one sample. Returns modulation value [-1, +1].
    /// S&H: random step on each phase wrap.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Tick(in LfoParams p)
        => throw new System.NotImplementedException();
}
