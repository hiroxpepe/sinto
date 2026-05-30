// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using NUnit.Framework;
using Signo.Core.Synth;

namespace Signo.Tests.Synth;

[TestFixture]
public class LfoParamsTests
{
    [Test] public void LfoParams_IsValueType()
        => Assert.That(typeof(LfoParams).IsValueType, Is.True);

    [Test] public void Constructor_DefaultValues_AreValid()
    {
        var p = new LfoParams(LfoWave.Sine);
        Assert.That(p.Wave,      Is.EqualTo(LfoWave.Sine));
        Assert.That(p.RateOrSync, Is.EqualTo(1.0f).Within(1e-6f));
        Assert.That(p.Depth,     Is.EqualTo(0.5f).Within(1e-6f));
        Assert.That(p.TempoSync, Is.False);
        Assert.That(p.Destinations, Is.EqualTo(LfoTarget.FilterCutoff));
    }

    [TestCase(LfoWave.Sine)]
    [TestCase(LfoWave.Triangle)]
    [TestCase(LfoWave.Square)]
    [TestCase(LfoWave.SH)]
    public void Constructor_AllWaves_Stored(LfoWave wave)
    {
        var p = new LfoParams(wave);
        Assert.That(p.Wave, Is.EqualTo(wave));
    }

    [Test] public void Constructor_TempoSync_True_Stored()
    {
        var p = new LfoParams(LfoWave.Sine, tempoSync: true);
        Assert.That(p.TempoSync, Is.True);
    }

    [Test] public void Constructor_RateOrSync_AsFrequency_Stored()
    {
        // Free mode: RateOrSync = Hz
        var p = new LfoParams(LfoWave.Sine, rateOrSync: 5.0f);
        Assert.That(p.RateOrSync, Is.EqualTo(5.0f).Within(1e-6f));
    }

    [Test] public void Constructor_RateOrSync_AsSyncNote_Stored()
    {
        // TempoSync mode: RateOrSync = note value (0.25 = 1/4 note)
        var p = new LfoParams(LfoWave.Sine, rateOrSync: 0.25f, tempoSync: true);
        Assert.That(p.RateOrSync, Is.EqualTo(0.25f).Within(1e-6f));
        Assert.That(p.TempoSync,  Is.True);
    }

    [Test] public void Destinations_Flags_CanBeCombined()
    {
        var dest = LfoTarget.OSC1Pitch | LfoTarget.FilterCutoff;
        var p = new LfoParams(LfoWave.Sine, destinations: dest);
        Assert.That(p.Destinations.HasFlag(LfoTarget.OSC1Pitch),   Is.True);
        Assert.That(p.Destinations.HasFlag(LfoTarget.FilterCutoff), Is.True);
        Assert.That(p.Destinations.HasFlag(LfoTarget.Amp),          Is.False);
    }

    [TestCase(-0.1f, 0.0f)]
    [TestCase(0.0f,  0.0f)]
    [TestCase(0.5f,  0.5f)]
    [TestCase(1.0f,  1.0f)]
    [TestCase(1.5f,  1.0f)]
    public void Constructor_Depth_IsClamped(float input, float expected)
    {
        var p = new LfoParams(LfoWave.Sine, depth: input);
        Assert.That(p.Depth, Is.EqualTo(expected).Within(1e-4f));
    }
}
