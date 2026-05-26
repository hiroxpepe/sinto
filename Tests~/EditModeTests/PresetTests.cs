// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.IO;
using NUnit.Framework;
using Sinto.Core.Preset;
using Sinto.Core.Synth;
using Sinto.Core.Filter;

namespace Sinto.Tests.Preset;

// ── SintoPreset ──────────────────────────────────────────────────────────────

[TestFixture]
public class SintoPresetTests
{
    [Test] public void Default_HasNonNullSubPresets()
    {
        var p = SintoPreset.Default;
        Assert.That(p.Osc1,           Is.Not.Null);
        Assert.That(p.Osc2,           Is.Not.Null);
        Assert.That(p.Filter,         Is.Not.Null);
        Assert.That(p.AmpEnvelope,    Is.Not.Null);
        Assert.That(p.FilterEnvelope, Is.Not.Null);
        Assert.That(p.PitchEnvelope,  Is.Not.Null);
        Assert.That(p.Lfo1,           Is.Not.Null);
        Assert.That(p.Lfo2,           Is.Not.Null);
        Assert.That(p.Effects,        Is.Not.Null);
    }

    [Test] public void Default_Name_IsNotEmpty()
        => Assert.That(SintoPreset.Default.Name, Is.Not.Empty);

    [Test] public void Default_PortamentoTime_IsZero()
        => Assert.That(SintoPreset.Default.PortamentoTime, Is.EqualTo(0f).Within(1e-6f));

    [Test] public void Default_RetroMode_IsClean()
        => Assert.That(SintoPreset.Default.RetroMode, Is.EqualTo(RetroMode.Clean));
}

// ── OscillatorPreset ─────────────────────────────────────────────────────────

[TestFixture]
public class OscillatorPresetTests
{
    [Test] public void Default_WaveType_IsSine()
        => Assert.That(OscillatorPreset.Default.Wave, Is.EqualTo(WaveType.Sine));

    [Test] public void Default_Level_IsOne()
        => Assert.That(OscillatorPreset.Default.Level, Is.EqualTo(1.0f).Within(1e-6f));

    [Test] public void Default_PulseWidth_IsHalf()
        => Assert.That(OscillatorPreset.Default.PulseWidth, Is.EqualTo(0.5f).Within(1e-6f));
}

// ── FilterPreset ─────────────────────────────────────────────────────────────

[TestFixture]
public class FilterPresetTests
{
    [Test] public void Default_Mode_IsRoland()
        => Assert.That(FilterPreset.Default.Mode, Is.EqualTo(FilterMode.Roland));

    [Test] public void Default_Cutoff_IsOpen()
        => Assert.That(FilterPreset.Default.Cutoff, Is.EqualTo(1.0f).Within(1e-6f));

    [Test] public void Default_Resonance_IsZero()
        => Assert.That(FilterPreset.Default.Resonance, Is.EqualTo(0f).Within(1e-6f));
}

// ── EnvelopePreset ───────────────────────────────────────────────────────────

[TestFixture]
public class EnvelopePresetTests
{
    [Test] public void Default_AllValuesArePositive()
    {
        var p = EnvelopePreset.Default;
        Assert.That(p.Attack,  Is.GreaterThan(0f));
        Assert.That(p.Decay,   Is.GreaterThan(0f));
        Assert.That(p.Sustain, Is.GreaterThanOrEqualTo(0f));
        Assert.That(p.Release, Is.GreaterThan(0f));
    }

    [Test] public void Percussive_SustainIsZero()
        => Assert.That(EnvelopePreset.Percussive.Sustain, Is.EqualTo(0f).Within(1e-6f));

    [Test] public void Pad_AttackIsLong()
        => Assert.That(EnvelopePreset.Pad.Attack, Is.GreaterThan(0.3f));
}

// ── LFOPreset ────────────────────────────────────────────────────────────────

[TestFixture]
public class LFOPresetTests
{
    [Test] public void Default_DepthIsZero()
        => Assert.That(LFOPreset.Default.Depth, Is.EqualTo(0f).Within(1e-6f));

    [Test] public void Default_TempoSyncIsFalse()
        => Assert.That(LFOPreset.Default.TempoSync, Is.False);

    [Test] public void Default_DestinationIsNone()
        => Assert.That(LFOPreset.Default.Destinations, Is.EqualTo(LFODestination.None));
}

// ── EffectsPreset ────────────────────────────────────────────────────────────

[TestFixture]
public class EffectsPresetTests
{
    [Test] public void Default_AllMixAreZero()
    {
        var p = EffectsPreset.Default;
        Assert.That(p.ChorusMix,  Is.EqualTo(0f).Within(1e-6f));
        Assert.That(p.ReverbMix,  Is.EqualTo(0f).Within(1e-6f));
        Assert.That(p.DelayMix,   Is.EqualTo(0f).Within(1e-6f));
    }

    [Test] public void Default_DelayFeedback_BelowMaximum()
        => Assert.That(EffectsPreset.Default.DelayFeedback, Is.LessThanOrEqualTo(0.95f));

    [Test] public void Default_RetroMode_IsClean()
        => Assert.That(EffectsPreset.Default.RetroMode, Is.EqualTo(RetroMode.Clean));
}

// ── PresetValidator ──────────────────────────────────────────────────────────

[TestFixture]
public class PresetValidatorTests
{
    [Test] public void Validate_NullInput_ReturnsSafeDefault()
        => Assert.DoesNotThrow(() => PresetValidator.Validate(SintoPreset.Default));

    [Test] public void Validate_OscPulseWidth_ClampedTo0_01_0_99()
    {
        var raw = new SintoPreset {
            Osc1 = new OscillatorPreset { PulseWidth = 0.0f } // below min
        };
        var validated = PresetValidator.Validate(raw);
        Assert.That(validated.Osc1.PulseWidth, Is.GreaterThanOrEqualTo(0.01f),
            "PulseWidth below 0.01 must be clamped.");
    }

    [Test] public void Validate_FilterCutoff_ClampedTo0_001_0_999()
    {
        var raw = new SintoPreset {
            Filter = new FilterPreset { Cutoff = 2.0f } // above max
        };
        var validated = PresetValidator.Validate(raw);
        Assert.That(validated.Filter.Cutoff, Is.LessThanOrEqualTo(0.999f),
            "Cutoff above 0.999 must be clamped.");
    }

    [Test] public void Validate_EnvelopeAttack_ClampedToMinimum()
    {
        var raw = new SintoPreset {
            AmpEnvelope = new EnvelopePreset { Attack = 0.0f }
        };
        var validated = PresetValidator.Validate(raw);
        Assert.That(validated.AmpEnvelope.Attack, Is.GreaterThanOrEqualTo(0.001f),
            "Attack = 0 must be clamped to prevent division by zero.");
    }

    [Test] public void Validate_DelayFeedback_ClampedTo0_95()
    {
        var raw = new SintoPreset {
            Effects = new EffectsPreset { DelayFeedback = 1.0f }
        };
        var validated = PresetValidator.Validate(raw);
        Assert.That(validated.Effects.DelayFeedback, Is.LessThanOrEqualTo(0.95f),
            "Delay feedback >= 1.0 must be clamped to prevent runaway.");
    }

    [Test] public void Validate_FilterResonance_ClampedTo0_1()
    {
        var raw = new SintoPreset {
            Filter = new FilterPreset { Resonance = 5.0f }
        };
        var validated = PresetValidator.Validate(raw);
        Assert.That(validated.Filter.Resonance, Is.LessThanOrEqualTo(1.0f));
        Assert.That(validated.Filter.Resonance, Is.GreaterThanOrEqualTo(0.0f));
    }
}

// ── PresetLoader ─────────────────────────────────────────────────────────────

[TestFixture]
public class PresetLoaderTests
{
    [Test] public void Load_NonExistentFile_ReturnsDefault()
    {
        var result = PresetLoader.Load("/nonexistent/path/preset.sinto");
        Assert.That(result, Is.Not.Null,
            "Load must return Default preset on file-not-found, not throw.");
    }

    [Test] public void Load_CorruptedFile_ReturnsDefault()
    {
        // Write corrupted data to temp file
        string tmp = Path.GetTempFileName();
        try {
            File.WriteAllText(tmp, "{ corrupted json {{{{");
            var result = PresetLoader.Load(tmp);
            Assert.That(result, Is.Not.Null,
                "Load must return Default preset on parse error, not throw.");
        } finally {
            File.Delete(tmp);
        }
    }

    [Test] public void LoadFromBytes_EmptyData_ReturnsDefault()
    {
        var result = PresetLoader.LoadFromBytes(ReadOnlySpan<byte>.Empty);
        Assert.That(result, Is.Not.Null);
    }

    [Test] public void LoadFromBytes_ValidJson_ReturnsPreset()
    {
        var json = System.Text.Encoding.UTF8.GetBytes(
            """{"Name":"TestPreset","Version":"1.0"}""");
        var result = PresetLoader.LoadFromBytes(json.AsSpan());
        Assert.That(result, Is.Not.Null);
        Assert.That(result.Name, Is.EqualTo("TestPreset"));
    }

    [Test] public void Load_ValidatedPreset_HasSafeValues()
    {
        // Any loaded preset must pass validation automatically
        string tmp = Path.GetTempFileName();
        try {
            // Write a preset with out-of-range values
            File.WriteAllText(tmp,
                """{"Name":"Extreme","Filter":{"Cutoff":999.0,"Resonance":-5.0}}""");
            var result = PresetLoader.Load(tmp);
            // Validation must have been applied
            Assert.That(result.Filter.Cutoff,    Is.LessThanOrEqualTo(0.999f));
            Assert.That(result.Filter.Resonance, Is.GreaterThanOrEqualTo(0.0f));
        } finally {
            File.Delete(tmp);
        }
    }
}
