// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Effects;

namespace Signo.Tests.Effects;

[TestFixture]
public class StepFlangerTests
{
    const int SR = 44100;

    [Test]
    public void Flanger_StepMode_Off_IsContinuous()
    {
        var fx = new Flanger(SR);
        fx.SetParams(1.0f, 0.9f, 0.0f, 1f);
        fx.SetStepMode(FlangerStepMode.Off);
        var buf = MakeSine(440f, 4096);
        fx.Process(buf.AsSpan(), 2);
        Assert.That(Peak(buf), Is.GreaterThan(0.01f));
    }

    [Test]
    public void Flanger_StepMode_Step_DiffersFromOff()
    {
        var fxOff  = Make(FlangerStepMode.Off);
        var fxStep = Make(FlangerStepMode.Step);
        fxStep.SetStepRate(10);
        Assert.That(Diff(fxOff, fxStep, 4096), Is.GreaterThan(0.5f));
    }


    [Test]
    public void Flanger_StepMode_Gate2_LRDiffers()
    {
        var fx = Make(FlangerStepMode.Gate2);
        var buf = MakeSine(440f, SR / 2);
        fx.Process(buf.AsSpan(), 2);
        Assert.That(LRDiff(buf), Is.GreaterThan(0.01f));
    }

    [Test]
    public void Flanger_StepMode_Gate3_LRDiffers()
    {
        var fx = Make(FlangerStepMode.Gate3);
        var buf = MakeSine(440f, SR / 2);
        fx.Process(buf.AsSpan(), 2);
        Assert.That(LRDiff(buf), Is.GreaterThan(0.01f));
    }

    static Flanger Make(FlangerStepMode m) {
        var fx = new Flanger(SR);
        fx.SetParams(2.0f, 0.9f, 0.0f, 1f);
        fx.SetStepMode(m);
        return fx;
    }
    static float[] MakeSine(float freq, int frames) {
        var buf = new float[frames * 2];
        for (int i = 0; i < frames; i++) { float s = 0.5f * MathF.Sin(2f * MathF.PI * freq * i / SR); buf[i*2]=buf[i*2+1]=s; }
        return buf;
    }
    static float Peak(float[] buf) { float p=0f; foreach(var s in buf){float a=MathF.Abs(s);if(a>p)p=a;} return p; }
    static float LRDiff(float[] buf) { float d=0f; for(int i=0;i<buf.Length/2;i++) d+=MathF.Abs(buf[i*2]-buf[i*2+1]); return d/(buf.Length/2); }
    static float Diff(Flanger a, Flanger b, int frames) {
        var bA=MakeSine(440f,frames); var bB=MakeSine(440f,frames);
        a.Process(bA.AsSpan(),2); b.Process(bB.AsSpan(),2);
        float d=0f; for(int i=0;i<bA.Length;i++) d+=MathF.Abs(bA[i]-bB[i]); return d;
    }
}
