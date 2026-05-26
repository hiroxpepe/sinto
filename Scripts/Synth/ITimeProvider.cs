// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable

namespace Sinto.Core.Synth;

/// <summary>
/// Abstraction over Stopwatch.GetTimestamp() for testability.
/// Production: SystemTimeProvider (real Stopwatch).
/// Test: FakeTimeProvider (manually controlled ticks).
/// </summary>
public interface ITimeProvider {
    long GetTimestamp();
    long Frequency { get; }
}

public sealed class SystemTimeProvider : ITimeProvider {
    public static readonly SystemTimeProvider Instance = new();
    public long GetTimestamp() => System.Diagnostics.Stopwatch.GetTimestamp();
    public long Frequency     => System.Diagnostics.Stopwatch.Frequency;
}
