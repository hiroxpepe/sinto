// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable

namespace Sinto.Core.Synth;

/// <summary>
/// Abstraction over Stopwatch.GetTimestamp() for testability.
/// Production: SystemTimer (real Stopwatch).
/// Test: FakeTimeProvider (manually controlled ticks).
/// </summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public interface ITimer {
    long GetTimestamp();
    long frequency { get; }
}

/// <summary>System stopwatch-backed timer implementation.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public sealed class SystemTimer : ITimer {
    public static readonly SystemTimer Instance = new();
    public long GetTimestamp() => System.Diagnostics.Stopwatch.GetTimestamp();
    public long frequency     => System.Diagnostics.Stopwatch.Frequency;
}
