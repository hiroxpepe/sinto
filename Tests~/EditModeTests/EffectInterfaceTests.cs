// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Effects;

namespace Signo.Tests.Effects;

/// <summary>
/// IAudioEffect / ISendEffect / IInsertEffect interface contract tests.
/// </summary>
[TestFixture]
public class EffectInterfaceTests
{
    const int SR = 44100;

    // ── IAudioEffect ─────────────────────────────────────────────────────

    [Test]
    public void Chorus_Implements_ISendEffect()
        => Assert.That(new Chorus(SR), Is.InstanceOf<ISendEffect>());

    [Test]
    public void Delay_Implements_ISendEffect()
        => Assert.That(new Delay(SR), Is.InstanceOf<ISendEffect>());

    [Test]
    public void Reverb_Implements_ISendEffect()
        => Assert.That(new Reverb(), Is.InstanceOf<ISendEffect>());

    [Test]
    public void Flanger_Implements_IInsertEffect()
        => Assert.That(new Flanger(SR), Is.InstanceOf<IInsertEffect>());

    [Test]
    public void Phaser_Implements_IInsertEffect()
        => Assert.That(new Phaser(SR), Is.InstanceOf<IInsertEffect>());

    // ── ISendEffect: full-wet output contract ────────────────────────────

    [Test]
    public void SendEffect_Chorus_FullWet_OutputNotEqualToDry()
    {
        ISendEffect fx = new Chorus(SR);
        ((Chorus)fx).rate  = 1.0f;
        ((Chorus)fx).depth = 0.8f;
        ((Chorus)fx).mix   = 1.0f;
        var buf = MakeSine(440f, 4096);
        var dry = MakeSine(440f, 4096);
        fx.Process(buf.AsSpan(), 2);
        float diff = Diff(buf, dry);
        Assert.That(diff, Is.GreaterThan(0.1f));
    }

    // ── IInsertEffect: dry+wet output contract ───────────────────────────

    [Test]
    public void InsertEffect_Flanger_DrySurvives()
    {
        // With send=0, dry must pass through unchanged.
        IInsertEffect fx = new Flanger(SR);
        fx.Send = 0f;
        var buf = MakeSine(440f, 1024);
        var dry = MakeSine(440f, 1024);
        fx.Process(buf.AsSpan(), 2);
        float diff = Diff(buf, dry);
        Assert.That(diff, Is.LessThan(0.001f),
            "IInsertEffect with Send=0 must pass dry unchanged.");
    }

    [Test]
    public void InsertEffect_Phaser_DrySurvives()
    {
        IInsertEffect fx = new Phaser(SR);
        fx.Send = 0f;
        var buf = MakeSine(440f, 1024);
        var dry = MakeSine(440f, 1024);
        fx.Process(buf.AsSpan(), 2);
        float diff = Diff(buf, dry);
        Assert.That(diff, Is.LessThan(0.001f),
            "IInsertEffect with Send=0 must pass dry unchanged.");
    }

    [Test]
    public void InsertEffect_Flanger_WetAddsWithSend()
    {
        // With send=1, output must differ from dry.
        IInsertEffect fx = new Flanger(SR);
        fx.Send = 1f;
        var buf = MakeSine(440f, 4096);
        var dry = MakeSine(440f, 4096);
        fx.Process(buf.AsSpan(), 2);
        Assert.That(Diff(buf, dry), Is.GreaterThan(0.1f));
    }

    [Test]
    public void InsertEffect_Phaser_WetAddsWithSend()
    {
        IInsertEffect fx = new Phaser(SR);
        fx.Send = 1f;
        var buf = MakeSine(440f, 4096);
        var dry = MakeSine(440f, 4096);
        fx.Process(buf.AsSpan(), 2);
        Assert.That(Diff(buf, dry), Is.GreaterThan(0.1f));
    }

    // ── InsertFxChain ────────────────────────────────────────────────────

    [Test]
    public void InsertFxChain_Empty_PassesDryThrough()
    {
        var chain = new InsertFxChain();
        var buf = MakeSine(440f, 1024);
        var dry = MakeSine(440f, 1024);
        chain.Process(buf.AsSpan(), 2);
        Assert.That(Diff(buf, dry), Is.LessThan(0.001f));
    }

    [Test]
    public void InsertFxChain_WithFlanger_ModifiesSignal()
    {
        var chain = new InsertFxChain();
        var fx = new Flanger(SR);
        fx.Send = 1f;
        chain.Add(fx);
        var buf = MakeSine(440f, 4096);
        var dry = MakeSine(440f, 4096);
        chain.Process(buf.AsSpan(), 2);
        Assert.That(Diff(buf, dry), Is.GreaterThan(0.1f));
    }

    [Test]
    public void InsertFxChain_Remove_StopsEffect()
    {
        var chain = new InsertFxChain();
        var fx = new Flanger(SR);
        fx.Send = 1f;
        chain.Add(fx);
        chain.Remove(fx);
        var buf = MakeSine(440f, 1024);
        var dry = MakeSine(440f, 1024);
        chain.Process(buf.AsSpan(), 2);
        Assert.That(Diff(buf, dry), Is.LessThan(0.001f));
    }

    // ── helpers ──────────────────────────────────────────────────────────

    static float[] MakeSine(float freq, int frames)
    {
        var buf = new float[frames * 2];
        for (int i = 0; i < frames; i++) {
            float s = 0.5f * MathF.Sin(2f * MathF.PI * freq * i / SR);
            buf[i * 2] = buf[i * 2 + 1] = s;
        }
        return buf;
    }

    static float Diff(float[] a, float[] b)
    {
        float d = 0f;
        for (int i = 0; i < a.Length; i++) d += MathF.Abs(a[i] - b[i]);
        return d;
    }
}
