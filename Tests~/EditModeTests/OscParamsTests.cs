// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using NUnit.Framework;
using Signo.Core.Synth;

namespace Signo.Tests.Synth;

[TestFixture]
public class OscParamsTests
{
    [Test] public void OscParams_IsValueType()
        => Assert.That(typeof(OscParams).IsValueType, Is.True);

    [Test] public void Constructor_DefaultValues_AreValid()
    {
        var p = new OscParams(WaveType.Sine);
        Assert.That(p.Wave,        Is.EqualTo(WaveType.Sine));
        Assert.That(p.Interp,      Is.EqualTo(Interpolation.Linear));
        Assert.That(p.DetuneCents, Is.EqualTo(0f).Within(1e-6f));
        Assert.That(p.PulseWidth,  Is.EqualTo(0.5f).Within(1e-6f));
        Assert.That(p.Level,       Is.EqualTo(1.0f).Within(1e-6f));
    }

    [TestCase(WaveType.Sine)]
    [TestCase(WaveType.Saw)]
    [TestCase(WaveType.Triangle)]
    [TestCase(WaveType.Square)]
    [TestCase(WaveType.Noise)]
    public void Constructor_AllWaveTypes_Stored(WaveType wave)
    {
        var p = new OscParams(wave);
        Assert.That(p.Wave, Is.EqualTo(wave));
    }

    [TestCase(Interpolation.Linear)]
    [TestCase(Interpolation.NearestNeighbor)]
    public void Constructor_AllInterpolations_Stored(Interpolation interp)
    {
        var p = new OscParams(WaveType.Sine, interp);
        Assert.That(p.Interp, Is.EqualTo(interp));
    }

    [TestCase(-100f, -100f)]
    [TestCase(0f,      0f)]
    [TestCase(100f,  100f)]
    public void Constructor_DetuneCents_Stored(float input, float expected)
    {
        var p = new OscParams(WaveType.Saw, detuneCents: input);
        Assert.That(p.DetuneCents, Is.EqualTo(expected).Within(1e-6f));
    }

    [TestCase(0.0f,  0.01f)]  // below min → clamped
    [TestCase(0.5f,  0.5f)]   // valid
    [TestCase(1.0f,  0.99f)]  // above max → clamped
    public void Constructor_PulseWidth_IsClamped(float input, float expected)
    {
        var p = new OscParams(WaveType.Square, pulseWidth: input);
        Assert.That(p.PulseWidth, Is.EqualTo(expected).Within(1e-4f));
    }

    [TestCase(-0.1f, 0.0f)]
    [TestCase(0.0f,  0.0f)]
    [TestCase(0.5f,  0.5f)]
    [TestCase(1.0f,  1.0f)]
    [TestCase(1.5f,  1.0f)]
    public void Constructor_Level_IsClamped(float input, float expected)
    {
        var p = new OscParams(WaveType.Sine, level: input);
        Assert.That(p.Level, Is.EqualTo(expected).Within(1e-4f));
    }
}
