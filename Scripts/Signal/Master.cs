// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;

namespace Signo.Core.Signal;

/// <summary>
/// Final stage master processor.
/// Placeholder for MasterEq and Limiter (Phase 3+).
/// Currently passes signal through.
/// </summary>
public sealed class Master : ISignal
{
    public bool enabled { get; set; } = true;
    readonly int _sr;

    public Master(int sampleRate = 44100) { _sr = sampleRate; }

    public void Process(Span<float> buffer, int channels)
    {
        if (!enabled) return;
        // MasterEq and Limiter will be inserted here in Phase 3
    }

    public void Reset() { }
}
