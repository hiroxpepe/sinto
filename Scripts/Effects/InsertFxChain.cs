// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Collections.Generic;

namespace Signo.Core.Effects;

/// <summary>
/// Ordered chain of insert effects applied in-place to a buffer.
/// Each effect receives the output of the previous one.
/// </summary>
public sealed class InsertFxChain
{
    readonly List<IInsertEffect> _chain = new();

    public void Add(IInsertEffect fx)    => _chain.Add(fx);
    public void Remove(IInsertEffect fx) => _chain.Remove(fx);

    public void Process(Span<float> buffer, int channels)
    {
        foreach (var fx in _chain)
            if (fx.enabled) fx.Process(buffer, channels);
    }

    public void Reset()
    {
        foreach (var fx in _chain) fx.Reset();
    }
}
