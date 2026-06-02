// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;

namespace Signo.Core.Signal;

/// <summary>
/// Base interface for all nodes in the Signo audio pipeline.
/// Implemented by sound sources (ISynth), processors (IEffect), and
/// aggregators (Channel, EffectBus, Master).
/// </summary>
public interface ISignal
{
    void Process(Span<float> buffer, int channels);
    void Reset();
    bool enabled { get; set; }
}
