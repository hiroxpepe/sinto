// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using Signo.Core.Effects;

namespace Signo.Core.Signal;

/// <summary>
/// Shared send-return effect bus. All channels send to one EffectBus.
/// Owns Chorus, Delay, Reverb. CPU-efficient: one Reverb for all parts.
/// </summary>
public sealed class EffectBus : ISignal
{
    public bool enabled { get; set; } = true;

    public Chorus Chorus { get; }
    public Delay  Delay  { get; }
    public Reverb Reverb { get; }

    readonly int _sr;
    readonly float[] _scratch;

    public EffectBus(int sampleRate = 44100, int maxBufferFrames = 1024)
    {
        _sr     = sampleRate;
        Chorus  = new Chorus(sampleRate);
        Delay   = new Delay(sampleRate);
        Reverb  = new Reverb();
        Reverb.mix = 1f; Reverb.enabled = true;
        Chorus.mix = 1f;
        Delay.mix  = 1f;
        _scratch = new float[maxBufferFrames * 2];
    }

    /// <summary>
    /// Apply send-return: copy dry × send into scratch, process full-wet,
    /// mix return back into dry buffer.
    /// </summary>
    public void ProcessSend(Span<float> dry, float[] send,
        float reverbSend, float delaySend, float chorusSend)
    {
        if (!enabled) return;
        int n = dry.Length;
        SendBus(dry, send, Reverb, reverbSend);
        SendBus(dry, send, Delay,  delaySend);
        if (Chorus.enabled)
            SendBus(dry, send, Chorus, chorusSend);
    }

    void SendBus(Span<float> dry, float[] send, IEffect fx, float amount)
    {
        if (amount <= 0f || !fx.enabled) return;
        int n = Math.Min(dry.Length, send.Length);
        var scratch = _scratch.AsSpan(0, n);
        for (int i = 0; i < n; i++) scratch[i] = send[i] * amount;
        fx.Process(scratch, 2);
        for (int i = 0; i < n; i++) dry[i] += scratch[i];
    }

    // ISignal: full-buffer process (used when EffectBus is in a pipeline)
    public void Process(Span<float> buffer, int channels) { }
    public void Reset()
    {
        Chorus.Reset(); Delay.Reset(); Reverb.Reset();
    }
}
