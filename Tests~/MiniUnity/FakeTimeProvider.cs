// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable

namespace Sinto.Core.Synth;

/// <summary>
/// Deterministic time provider for unit tests.
/// Never use Thread.Sleep to simulate elapsed time — use Advance() instead.
/// </summary>
public sealed class FakeTimeProvider : ITimer {
    long _current = 0;

    // Simulate Stopwatch.frequency (ticks per second)
    public long frequency => 10_000_000L; // 100ns ticks (same as .NET Stopwatch on Windows)

    public long GetTimestamp() => _current;

    /// <summary>Advance time by the given number of milliseconds.</summary>
    public void AdvanceMs(double ms) => _current += (long)(ms * frequency / 1000.0);

    /// <summary>Advance time by the given number of ticks.</summary>
    public void AdvanceTicks(long ticks) => _current += ticks;
}
