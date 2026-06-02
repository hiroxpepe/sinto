// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Collections.Generic;
using Signo.Core.Effects;

namespace Signo.Core.Signal;

/// <summary>
/// Per-part channel processor. Owns insert FX chain and send amounts.
/// Signal flow: VAEngine → Channel → EffectBus → Master
/// </summary>
public sealed class Channel : ISignal
{
    public bool enabled { get; set; } = true;

    // Send amounts to EffectBus
    public float ReverbSend { get; set; } = 0f;
    public float DelaySend  { get; set; } = 0f;
    public float ChorusSend { get; set; } = 0f;

    readonly List<IInsertEffect> _inserts = new();
    readonly int _sr;

    public Channel(int sampleRate = 44100) { _sr = sampleRate; }

    public void AddInsert(IInsertEffect fx)    => _inserts.Add(fx);
    public void RemoveInsert(IInsertEffect fx) => _inserts.Remove(fx);
    public void ClearInserts()                 => _inserts.Clear();

    public void Process(Span<float> buffer, int channels)
    {
        if (!enabled) return;
        foreach (var fx in _inserts)
            if (fx.enabled) fx.Process(buffer, channels);
    }

    public void Reset()
    {
        foreach (var fx in _inserts) fx.Reset();
    }
}
