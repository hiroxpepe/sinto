// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System.Runtime.CompilerServices;

namespace Sinto.Core.Audio;

/// <summary>IIR subnormal protection. No static state — thread-safe.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public static class Denormal {
#nullable enable
    private const float MAGNITUDE    = 1e-15f;
    private const long  CYCLE_LENGTH = 256L;
    private const float SUBNORMAL_THRESHOLD = 1.175494e-38f; // float.Epsilon * 100

    /// <summary>Inject alternating DC offset (~172Hz at 44100Hz) to prevent IIR subnormal trap.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Protect(float x, long sampleIndex)
        => x + ((sampleIndex / CYCLE_LENGTH & 1L) == 0L ? MAGNITUDE : -MAGNITUDE);

    /// <summary>True if value is in subnormal range (causes 100x CPU spike on ARM).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsDenormal(float x) {
        float abs = x < 0f ? -x : x;
        return abs > 0f && abs < SUBNORMAL_THRESHOLD;
    }
}
