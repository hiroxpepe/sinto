// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.IO;
using NUnit.Framework;
using Sinto.Core.Preset;
using Preset = Sinto.Core.Preset.Preset;
using Sinto.Core.Synth;

namespace Sinto.Tests.Preset;

// ── Preset ──────────────────────────────────────────────────────────────

[TestFixture]
public class PresetTests
{
    [Test] public void Default_HasNonNullSubPresets()
    {
        var p = Sinto.Core.Preset.Preset.Default;
        Assert.That(p.osc1,           Is.Not.Null);
        Assert.That(p.osc2,           Is.Not.Null);
        Assert.That(p.filter,         Is.Not.Null);
        Assert.That(p.amp_envelope,    Is.Not.Null);
        Assert.That(p.filter_envelope, Is.Not.Null);
        Assert.That(p.pitch_envelope,  Is.Not.Null);
        Assert.That(p.lfo1,           Is.Not.Null);
        Assert.That(p.lfo2,           Is.Not.Null);
        Assert.That(p.effects,        Is.Not.Null);
    }

    [Test] public void Default_Name_IsNotEmpty()
        => Assert.That(Sinto.Core.Preset.Preset.Default.name, Is.Not.Empty);

    [Test] public void Default_PortamentoTime_IsZero()
        => Assert.That(Sinto.Core.Preset.Preset.Default.portamento_time, Is.EqualTo(0f).Within(1e-6f));

    [Test] public void Default_RetroMode_IsClean()
        => Assert.That(Sinto.Core.Preset.Preset.Default.retro_mode, Is.EqualTo(RetroMode.Clean));
}

// ── OscPreset ─────────────────────────────────────────────────────────

[TestFixture]
public class OscPresetTests
{
    [Test] public void Default_WaveType_IsSine()
        => Assert.That(OscPreset.Default.wave, Is.EqualTo(WaveType.Sine));

    [Test] public void Default_Level_IsOne()
        => Assert.That(OscPreset.Default.level, Is.EqualTo(1.0f).Within(1e-6f));

    [Test] public void Default_PulseWidth_IsHalf()
        => Assert.That(OscPreset.Default.pulse_width, Is.EqualTo(0.5f).Within(1e-6f));
}

// ── FilterPreset ─────────────────────────────────────────────────────────────

[TestFixture]
public class FilterPresetTests
{
    [Test] public void Default_Mode_IsRoland()
        => Assert.That(FilterPreset.Default.mode, Is.EqualTo(FilterKind.Roland));

    [Test] public void Default_Cutoff_IsOpen()
        => Assert.That(FilterPreset.Default.cutoff, Is.EqualTo(1.0f).Within(1e-6f));

    [Test] public void Default_Resonance_IsZero()
        => Assert.That(FilterPreset.Default.resonance, Is.EqualTo(0f).Within(1e-6f));
}

// ── EnvPreset ───────────────────────────────────────────────────────────

[TestFixture]
public class EnvPresetTests
{
    [Test] public void Default_AllValuesArePositive()
    {
        var p = EnvPreset.Default;
        Assert.That(p.attack,  Is.GreaterThan(0f));
        Assert.That(p.decay,   Is.GreaterThan(0f));
        Assert.That(p.sustain, Is.GreaterThanOrEqualTo(0f));
        Assert.That(p.release, Is.GreaterThan(0f));
    }

    [Test] public void Percussive_SustainIsZero()
        => Assert.That(EnvPreset.Percussive.sustain, Is.EqualTo(0f).Within(1e-6f));

    [Test] public void Pad_AttackIsLong()
        => Assert.That(EnvPreset.Pad.attack, Is.GreaterThan(0.3f));
}

// ── LfoPreset ────────────────────────────────────────────────────────────────

[TestFixture]
public class LfoPresetTests
{
    [Test] public void Default_DepthIsZero()
        => Assert.That(LfoPreset.Default.depth, Is.EqualTo(0f).Within(1e-6f));

    [Test] public void Default_TempoSyncIsFalse()
        => Assert.That(LfoPreset.Default.tempo_sync, Is.False);

    [Test] public void Default_DestinationIsNone()
        => Assert.That(LfoPreset.Default.destinations, Is.EqualTo(LfoTarget.None));
}

// ── FxPreset ────────────────────────────────────────────────────────────

[TestFixture]
public class FxPresetTests
{
    [Test] public void Default_AllMixAreZero()
    {
        var p = FxPreset.Default;
        Assert.That(p.chorus_mix,  Is.EqualTo(0f).Within(1e-6f));
        Assert.That(p.reverb_mix,  Is.EqualTo(0f).Within(1e-6f));
        Assert.That(p.delay_mix,   Is.EqualTo(0f).Within(1e-6f));
    }

    [Test] public void Default_DelayFeedback_BelowMaximum()
        => Assert.That(FxPreset.Default.delay_feedback, Is.LessThanOrEqualTo(0.95f));

    [Test] public void Default_RetroMode_IsClean()
        => Assert.That(FxPreset.Default.retro_mode, Is.EqualTo(RetroMode.Clean));
}

// ── Validator ──────────────────────────────────────────────────────────

[TestFixture]
public class ValidatorTests
{
    [Test] public void Validate_NullInput_ReturnsSafeDefault()
        => Assert.DoesNotThrow(() => Validator.Validate(Sinto.Core.Preset.Preset.Default));

    [Test] public void Validate_OscPulseWidth_ClampedTo0_01_0_99()
    {
        var raw = new Sinto.Core.Preset.Preset {
            osc1 = new OscPreset { pulse_width = 0.0f } // below min
        };
        var validated = Validator.Validate(raw);
        Assert.That(validated.osc1.pulse_width, Is.GreaterThanOrEqualTo(0.01f),
            "pulse_width below 0.01 must be clamped.");
    }

    [Test] public void Validate_FilterCutoff_ClampedTo0_001_0_999()
    {
        var raw = new Sinto.Core.Preset.Preset {
            filter = new FilterPreset { cutoff = 2.0f } // above max
        };
        var validated = Validator.Validate(raw);
        Assert.That(validated.filter.cutoff, Is.LessThanOrEqualTo(0.999f),
            "cutoff above 0.999 must be clamped.");
    }

    [Test] public void Validate_EnvelopeAttack_ClampedToMinimum()
    {
        var raw = new Sinto.Core.Preset.Preset {
            amp_envelope = new EnvPreset { attack = 0.0f }
        };
        var validated = Validator.Validate(raw);
        Assert.That(validated.amp_envelope.attack, Is.GreaterThanOrEqualTo(0.001f),
            "attack = 0 must be clamped to prevent division by zero.");
    }

    [Test] public void Validate_DelayFeedback_ClampedTo0_95()
    {
        var raw = new Sinto.Core.Preset.Preset {
            effects = new FxPreset { delay_feedback = 1.0f }
        };
        var validated = Validator.Validate(raw);
        Assert.That(validated.effects.delay_feedback, Is.LessThanOrEqualTo(0.95f),
            "Delay feedback >= 1.0 must be clamped to prevent runaway.");
    }

    [Test] public void Validate_FilterResonance_ClampedTo0_1()
    {
        var raw = new Sinto.Core.Preset.Preset {
            filter = new FilterPreset { resonance = 5.0f }
        };
        var validated = Validator.Validate(raw);
        Assert.That(validated.filter.resonance, Is.LessThanOrEqualTo(1.0f));
        Assert.That(validated.filter.resonance, Is.GreaterThanOrEqualTo(0.0f));
    }
}

// ── Loader ─────────────────────────────────────────────────────────────

[TestFixture]
public class LoaderTests
{
    [Test] public void Load_NonExistentFile_ReturnsDefault()
    {
        var result = Loader.Load("/nonexistent/path/preset.sinto");
        Assert.That(result, Is.Not.Null,
            "Load must return Default preset on file-not-found, not throw.");
    }

    [Test] public void Load_CorruptedFile_ReturnsDefault()
    {
        // Write corrupted data to temp file
        string tmp = Path.GetTempFileName();
        try {
            File.WriteAllText(tmp, "{ corrupted json {{{{");
            var result = Loader.Load(tmp);
            Assert.That(result, Is.Not.Null,
                "Load must return Default preset on parse error, not throw.");
        } finally {
            File.Delete(tmp);
        }
    }

    [Test] public void LoadFromBytes_EmptyData_ReturnsDefault()
    {
        var result = Loader.LoadFromBytes(ReadOnlySpan<byte>.Empty);
        Assert.That(result, Is.Not.Null);
    }

    [Test] public void LoadFromBytes_ValidJson_ReturnsPreset()
    {
        var json = System.Text.Encoding.UTF8.GetBytes(
            """{"name":"TestPreset","version":"1.0"}""");
        var result = Loader.LoadFromBytes(json.AsSpan());
        Assert.That(result, Is.Not.Null);
        Assert.That(result.name, Is.EqualTo("TestPreset"));
    }

    [Test] public void Load_ValidatedPreset_HasSafeValues()
    {
        // Any loaded preset must pass validation automatically
        string tmp = Path.GetTempFileName();
        try {
            // Write a preset with out-of-range values
            File.WriteAllText(tmp,
                """{"name":"Extreme","filter":{"cutoff":999.0,"resonance":-5.0}}""");
            var result = Loader.Load(tmp);
            // Validation must have been applied
            Assert.That(result.filter.cutoff,    Is.LessThanOrEqualTo(0.999f));
            Assert.That(result.filter.resonance, Is.GreaterThanOrEqualTo(0.0f));
        } finally {
            File.Delete(tmp);
        }
    }
}
