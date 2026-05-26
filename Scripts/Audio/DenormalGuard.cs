// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System.Runtime.CompilerServices;

namespace Sinto.Core.Audio;

public static class DenormalGuard {
    private const float Magnitude = 1e-15f;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Protect(float x, long sampleIndex)
        => throw new System.NotImplementedException();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDenormal(float x)
        => throw new System.NotImplementedException();
}
