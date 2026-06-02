// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Synth;
using Signo.Core.Effects;
using Signo.Core.Signal;

namespace Signo.Tests.Signal;

/// <summary>
/// TDD red: SignoProvider must wire VAEngine → Channel → EffectBus correctly.
/// The provider replaces the old pattern of calling FX methods on VAEngine directly.
/// After wiring:
///   - Notes play through VAEngine
///   - Insert FX (Flanger/Phaser etc.) live in Channel
///   - Send-return (Chorus/Delay/Reverb) live in EffectBus
///   - ProcessAudioCallback produces audible output
/// </summary>
[TestFixture]
public class SignoProviderWiringTests
{
    const int SR = 44100;

    static float Rms(float[] b) {
        double s = 0; foreach (var v in b) s += v*(double)v;
        return (float)Math.Sqrt(s/b.Length);
    }

    // ── SignoProvider exists and owns Channel + EffectBus ────────────
    [Test] public void SignoProvider_HasChannel()
        => Assert.That(new SignoProvider(new VAEngine(SR,2,8,512)).Channel, Is.Not.Null);

    [Test] public void SignoProvider_HasEffectBus()
        => Assert.That(new SignoProvider(new VAEngine(SR,2,8,512)).EffectBus, Is.Not.Null);

    // ── Audio flows: note → provider → audible output ────────────────
    [Test]
    public void SignoProvider_NoteOn_ProducesAudio()
    {
        var engine   = new VAEngine(SR, 2, 8, 512);
        var provider = new SignoProvider(engine);
        provider.NoteOn(60, 0.9f, 1, 1, 0);
        var buf = new float[1024];
        provider.Process(buf, 0, 512, 2);
        Assert.That(Rms(buf), Is.GreaterThan(0.001f),
            "Provider must produce audio after NoteOn.");
    }

    // ── FX set via Channel (not VAEngine directly) ───────────────────
    [Test]
    public void SignoProvider_SetFlanger_Via_Channel()
    {
        var provider = new SignoProvider(new VAEngine(SR, 2, 8, 512));
        Assert.DoesNotThrow(() => provider.SetModFx(ChorusType.Flanger, rate:2f, depth:0.8f, param3:0.3f, send:1f));
        Assert.That(provider.Channel.HasInsert<Flanger>(), Is.True,
            "Flanger must be in Channel inserts.");
    }

    [Test]
    public void SignoProvider_SetChorus_Via_EffectBus()
    {
        var provider = new SignoProvider(new VAEngine(SR, 2, 8, 512));
        Assert.DoesNotThrow(() => provider.SetChorusSend(0.5f));
        Assert.That(provider.Channel.ChorusSend, Is.EqualTo(0.5f));
    }

    [Test]
    public void SignoProvider_SetDelaySend_Via_Channel()
    {
        var provider = new SignoProvider(new VAEngine(SR, 2, 8, 512));
        provider.SetDelaySend(0.7f);
        Assert.That(provider.Channel.DelaySend, Is.EqualTo(0.7f));
    }

    [Test]
    public void SignoProvider_SetReverbSend_Via_Channel()
    {
        var provider = new SignoProvider(new VAEngine(SR, 2, 8, 512));
        provider.SetReverbSend(0.6f);
        Assert.That(provider.Channel.ReverbSend, Is.EqualTo(0.6f));
    }

    // ── Process applies channel + effectbus ──────────────────────────
    [Test]
    public void SignoProvider_ReverbSend_ProducesTail()
    {
        var engine   = new VAEngine(SR, 2, 8, 512);
        var provider = new SignoProvider(engine);
        provider.SetReverbSend(1f);
        provider.NoteOn(60, 0.9f, 1, 1, 0);
        var buf = new float[1024];
        provider.Process(buf, 0, 512, 2);
        provider.NoteOff(60, 1, 0);
        for (int i = 0; i < 30; i++) { provider.Process(buf, 0, 512, 2); }
        Assert.That(Rms(buf), Is.GreaterThan(0.0005f),
            "Reverb send must produce tail after note off.");
    }
}
