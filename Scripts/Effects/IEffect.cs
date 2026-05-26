// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;

namespace Sinto.Core.Effects;

public interface IEffect
{
    void Process(Span<float> buffer, int channels);
    void Reset();
    bool Enabled { get; set; }
}
