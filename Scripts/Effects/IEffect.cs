// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;

namespace Signo.Core.Effects;

/// <summary>Shared processing contract for audio effects.</summary>
/// <author>h.adachi (STUDIO MeowToon)</author>
public interface IEffect
{
    void Process(Span<float> buffer, int channels);
    void Reset();
    bool enabled { get; set; }
}
