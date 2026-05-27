// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using NUnit.Framework;
using Sinto.Core.Synth;

namespace Sinto.Tests.Synth;

[TestFixture]
public class LFOParamsTests
{
    [Test] public void LFOParams_IsValueType()
        => Assert.That(typeof(LFOParams).IsValueType, Is.True);

    [Test] public void Constructor_DefaultValues_AreValid()
    {
        var p = new LFOParams(LFOWave.Sine);
        Assert.That(p.Wave,      Is.EqualTo(LFOWave.Sine));
        Assert.That(p.RateOrSync, Is.EqualTo(1.0f).Within(1e-6f));
        Assert.That(p.Depth,     Is.EqualTo(0.5f).Within(1e-6f));
        Assert.That(p.TempoSync, Is.False);
        Assert.That(p.Destinations, Is.EqualTo(LFODestination.FilterCutoff));
    }

    [TestCase(LFOWave.Sine)]
    [TestCase(LFOWave.Triangle)]
    [TestCase(LFOWave.Square)]
    [TestCase(LFOWave.SH)]
    public void Constructor_AllWaves_Stored(LFOWave wave)
    {
        var p = new LFOParams(wave);
        Assert.That(p.Wave, Is.EqualTo(wave));
    }

    [Test] public void Constructor_TempoSync_True_Stored()
    {
        var p = new LFOParams(LFOWave.Sine, tempoSync: true);
        Assert.That(p.TempoSync, Is.True);
    }

    [Test] public void Constructor_RateOrSync_AsFrequency_Stored()
    {
        // Free mode: RateOrSync = Hz
        var p = new LFOParams(LFOWave.Sine, rateOrSync: 5.0f);
        Assert.That(p.RateOrSync, Is.EqualTo(5.0f).Within(1e-6f));
    }

    [Test] public void Constructor_RateOrSync_AsSyncNote_Stored()
    {
        // TempoSync mode: RateOrSync = note value (0.25 = 1/4 note)
        var p = new LFOParams(LFOWave.Sine, rateOrSync: 0.25f, tempoSync: true);
        Assert.That(p.RateOrSync, Is.EqualTo(0.25f).Within(1e-6f));
        Assert.That(p.TempoSync,  Is.True);
    }

    [Test] public void Destinations_Flags_CanBeCombined()
    {
        var dest = LFODestination.OSC1Pitch | LFODestination.FilterCutoff;
        var p = new LFOParams(LFOWave.Sine, destinations: dest);
        Assert.That(p.Destinations.HasFlag(LFODestination.OSC1Pitch),   Is.True);
        Assert.That(p.Destinations.HasFlag(LFODestination.FilterCutoff), Is.True);
        Assert.That(p.Destinations.HasFlag(LFODestination.Amp),          Is.False);
    }

    [TestCase(-0.1f, 0.0f)]
    [TestCase(0.0f,  0.0f)]
    [TestCase(0.5f,  0.5f)]
    [TestCase(1.0f,  1.0f)]
    [TestCase(1.5f,  1.0f)]
    public void Constructor_Depth_IsClamped(float input, float expected)
    {
        var p = new LFOParams(LFOWave.Sine, depth: input);
        Assert.That(p.Depth, Is.EqualTo(expected).Within(1e-4f));
    }
}
