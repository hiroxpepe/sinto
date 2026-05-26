// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System.Runtime.CompilerServices;

namespace Sinto.Core.Synth;

public struct OscillatorState {
    private double _phase;
    private double _phaseInc;
    private uint   _noiseSeed;
    private long   _sampleCount;

    public void SetFrequency(float frequencyHz, int sampleRate)
        => throw new System.NotImplementedException();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Tick(in OscillatorParams p)
        => throw new System.NotImplementedException();
}
