// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System.Runtime.CompilerServices;

namespace Sinto.Core.Synth;

public static class SintoMath {
    public const int LutSize = 4096;
    public static bool IsLutMode { get; private set; }

    public static void Initialize()
        => throw new System.NotImplementedException();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float SinFast(double phase)
        => throw new System.NotImplementedException();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float TanhFast(float x)
        => throw new System.NotImplementedException();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float PitchRatioFast(float semitones)
        => throw new System.NotImplementedException();
}
