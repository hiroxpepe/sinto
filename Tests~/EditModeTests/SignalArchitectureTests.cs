// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Signal;
using Signo.Core.Effects;
using Signo.Core.Synth;

namespace Signo.Tests.Signal;

/// <summary>
/// TDD red: Signal architecture — Channel / EffectBus / Master
///
/// ARCHITECTURE.md Phase 2-3:
///   VAEngine (ISynth) → Channel (ISignal) → EffectBus (ISignal) → Master (ISignal)
///
/// VAEngine must NOT own FX.
/// Channel owns: ChannelEq, Compressor, Waveshaper[], InsertFX (Flanger/Phaser etc.)
/// EffectBus owns: Chorus, Delay, Reverb (shared send-return)
/// Master owns: MasterEq, Limiter
/// </summary>

// ════════════════════════════════════════════════════════════════════
// Channel
// ════════════════════════════════════════════════════════════════════
[TestFixture]
public class ChannelTests
{
    const int SR = 44100;

    // ── Interface ───────────────────────────────────────────────────
    [Test] public void Channel_Implements_ISignal()
        => Assert.That(new Channel(SR), Is.InstanceOf<ISignal>());

    // ── Insert FX chain ─────────────────────────────────────────────
    [Test] public void Channel_CanAdd_IInsertEffect()
    {
        var ch = new Channel(SR);
        var flanger = new Flanger(SR);
        Assert.DoesNotThrow(() => ch.AddInsert(flanger));
    }

    [Test] public void Channel_Insert_ProcessesInOrder()
    {
        var ch = new Channel(SR);
        var flanger = new Flanger(SR);
        flanger.enabled = true;
        ch.AddInsert(flanger);
        var buf = new float[512 * 2];
        for (int i = 0; i < buf.Length; i += 2) buf[i] = buf[i+1] = 0.5f;
        Assert.DoesNotThrow(() => ch.Process(buf, 2));
        Assert.That(buf, Has.Some.Not.EqualTo(0.5f),
            "Channel with insert FX must modify the signal.");
    }

    [Test] public void Channel_NoInserts_PassesThrough()
    {
        var ch = new Channel(SR);
        ch.enabled = true;
        var buf = new float[512 * 2];
        for (int i = 0; i < buf.Length; i++) buf[i] = 0.5f;
        ch.Process(buf, 2);
        Assert.That(buf[0], Is.EqualTo(0.5f).Within(0.001f),
            "Channel with no inserts must pass signal unchanged.");
    }

    // ── Send to EffectBus ────────────────────────────────────────────
    [Test] public void Channel_HasSendAmount_ForEffectBus()
    {
        var ch = new Channel(SR);
        ch.ReverbSend = 0.5f;
        ch.DelaySend  = 0.3f;
        ch.ChorusSend = 0.2f;
        Assert.That(ch.ReverbSend, Is.EqualTo(0.5f));
        Assert.That(ch.DelaySend,  Is.EqualTo(0.3f));
        Assert.That(ch.ChorusSend, Is.EqualTo(0.2f));
    }

    // ── Disabled passthrough ─────────────────────────────────────────
    [Test] public void Channel_Disabled_PassesThrough()
    {
        var ch = new Channel(SR);
        ch.enabled = false;
        ch.AddInsert(new Flanger(SR) { enabled = true });
        var buf = new float[512 * 2];
        for (int i = 0; i < buf.Length; i++) buf[i] = 0.5f;
        ch.Process(buf, 2);
        Assert.That(buf[0], Is.EqualTo(0.5f).Within(0.001f));
    }

    // ── No NaN/Inf ───────────────────────────────────────────────────
    [Test] public void Channel_NoNaNOrInf()
    {
        var ch = new Channel(SR);
        ch.AddInsert(new Flanger(SR) { enabled = true });
        var buf = new float[512 * 2];
        for (int i = 0; i < buf.Length; i += 2) buf[i] = buf[i+1] = 0.8f;
        ch.Process(buf, 2);
        foreach (var s in buf)
            Assert.That(float.IsFinite(s), Is.True);
    }
}

// ════════════════════════════════════════════════════════════════════
// EffectBus
// ════════════════════════════════════════════════════════════════════
[TestFixture]
public class EffectBusTests
{
    const int SR = 44100;

    [Test] public void EffectBus_Implements_ISignal()
        => Assert.That(new EffectBus(SR), Is.InstanceOf<ISignal>());

    // ── Owns Chorus/Delay/Reverb ─────────────────────────────────────
    [Test] public void EffectBus_HasChorus()
        => Assert.That(new EffectBus(SR).Chorus, Is.Not.Null);

    [Test] public void EffectBus_HasDelay()
        => Assert.That(new EffectBus(SR).Delay, Is.Not.Null);

    [Test] public void EffectBus_HasReverb()
        => Assert.That(new EffectBus(SR).Reverb, Is.Not.Null);

    // ── Accepts send from Channel ────────────────────────────────────
    [Test] public void EffectBus_AcceptsSend_AndMixesReturn()
    {
        var bus = new EffectBus(SR);
        bus.Reverb.enabled = true;
        var dry = new float[512 * 2];
        for (int i = 0; i < dry.Length; i += 2) dry[i] = dry[i+1] = 0.5f;
        var send = (float[])dry.Clone();
        // send × amount → bus processes → return mixed into dry
        Assert.DoesNotThrow(() => bus.ProcessSend(dry, send, reverbSend: 0.5f, delaySend: 0f, chorusSend: 0f));
    }

    // ── No NaN/Inf ───────────────────────────────────────────────────
    [Test] public void EffectBus_NoNaNOrInf()
    {
        var bus = new EffectBus(SR);
        bus.Reverb.enabled = true;
        var dry = new float[512 * 2];
        for (int i = 0; i < dry.Length; i += 2) dry[i] = dry[i+1] = 0.8f;
        var send = (float[])dry.Clone();
        bus.ProcessSend(dry, send, 0.5f, 0.3f, 0.2f);
        foreach (var s in dry) Assert.That(float.IsFinite(s), Is.True);
    }
}

// ════════════════════════════════════════════════════════════════════
// Master
// ════════════════════════════════════════════════════════════════════
[TestFixture]
public class MasterTests
{
    const int SR = 44100;

    [Test] public void Master_Implements_ISignal()
        => Assert.That(new Master(SR), Is.InstanceOf<ISignal>());

    // ── Final stage: passthrough when flat ───────────────────────────
    [Test] public void Master_Flat_PassesThrough()
    {
        var master = new Master(SR);
        master.enabled = true;
        var buf = new float[512 * 2];
        for (int i = 0; i < buf.Length; i++) buf[i] = 0.5f;
        master.Process(buf, 2);
        Assert.That(buf[0], Is.EqualTo(0.5f).Within(0.05f),
            "Master with flat settings must pass signal unchanged.");
    }

    // ── No NaN/Inf ───────────────────────────────────────────────────
    [Test] public void Master_NoNaNOrInf()
    {
        var master = new Master(SR);
        master.enabled = true;
        var buf = new float[512 * 2];
        for (int i = 0; i < buf.Length; i += 2) buf[i] = buf[i+1] = 0.9f;
        master.Process(buf, 2);
        foreach (var s in buf) Assert.That(float.IsFinite(s), Is.True);
    }
}

// ════════════════════════════════════════════════════════════════════
// VAEngine must NOT own FX
// ════════════════════════════════════════════════════════════════════
[TestFixture]
public class VAEngineDecoupledTests
{
    // VAEngine must not have Flanger/Phaser/Chorus/Delay/Reverb fields
    [Test] public void VAEngine_HasNo_FlangerField()
    {
        var type = typeof(VAEngine);
        var fields = type.GetFields(
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Instance);
        var fxFields = System.Array.FindAll(fields, f =>
            f.FieldType == typeof(Flanger) ||
            f.FieldType == typeof(Phaser)  ||
            f.FieldType == typeof(Chorus)  ||
            f.FieldType == typeof(Delay)   ||
            f.FieldType == typeof(Reverb)  ||
            f.FieldType == typeof(Tremolo) ||
            f.FieldType == typeof(Vibrato) ||
            f.FieldType == typeof(AutoWah));
        Assert.That(fxFields.Length, Is.EqualTo(0),
            $"VAEngine must not own FX. Found: {string.Join(", ", System.Array.ConvertAll(fxFields, f => f.Name))}");
    }
}
