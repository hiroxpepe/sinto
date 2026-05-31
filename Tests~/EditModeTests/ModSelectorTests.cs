// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Synth;
using Signo.Core.Effects;

namespace Signo.Tests.UI;

/// <summary>
/// Decision table for MOD selector:
/// Each mod type must map to a unique ChorusType and produce correct pedal colour.
///
/// | _modType | ChorusType     | pedal bg         | selector active |
/// |----------|----------------|------------------|-----------------|
/// | chr      | Chorus         | #09212d (H200)   | CHR filled      |
/// | flg      | Flanger        | #2d091e (H325)   | FLG filled      |
/// | phs      | Phaser         | #152d09 (H100)   | PHS filled      |
/// | trm      | Tremolo        | #092d2d (H180)   | TRM filled      |
/// | vib      | Vibrato        | #090f2d (H230)   | VIB filled      |
/// | wah      | AutoWah        | #2d0c09 (H5)     | WAH filled      |
/// </summary>
[TestFixture]
public class ModSelectorDecisionTests
{
    [Test]
    public void ChorusType_Has_Tremolo()
        => Assert.That(Enum.IsDefined(typeof(ChorusType), "Tremolo"), Is.True,
            "ChorusType must have Tremolo member.");

    [Test]
    public void ChorusType_Has_Vibrato()
        => Assert.That(Enum.IsDefined(typeof(ChorusType), "Vibrato"), Is.True,
            "ChorusType must have Vibrato member.");

    [Test]
    public void ChorusType_Has_AutoWah()
        => Assert.That(Enum.IsDefined(typeof(ChorusType), "AutoWah"), Is.True,
            "ChorusType must have AutoWah member.");

    [Test]
    public void Engine_SetChorusType_Tremolo_DoesNotThrow()
    {
        var e = new Engine(44100, 2, 8, 512);
        Assert.DoesNotThrow(() => e.SetChorusType(ChorusType.Tremolo));
    }

    [Test]
    public void Engine_SetChorusType_Vibrato_DoesNotThrow()
    {
        var e = new Engine(44100, 2, 8, 512);
        Assert.DoesNotThrow(() => e.SetChorusType(ChorusType.Vibrato));
    }

    [Test]
    public void Engine_SetChorusType_AutoWah_DoesNotThrow()
    {
        var e = new Engine(44100, 2, 8, 512);
        Assert.DoesNotThrow(() => e.SetChorusType(ChorusType.AutoWah));
    }

    [Test]
    public void Engine_SetTremoloParams_DoesNotThrow()
    {
        var e = new Engine(44100, 2, 8, 512);
        Assert.DoesNotThrow(() => e.SetTremoloParams(4f, 0.8f));
    }

    [Test]
    public void Engine_SetVibratoParams_DoesNotThrow()
    {
        var e = new Engine(44100, 2, 8, 512);
        Assert.DoesNotThrow(() => e.SetVibratoParams(4f, 0.8f));
    }

    [Test]
    public void Engine_SetAutoWahParams_DoesNotThrow()
    {
        var e = new Engine(44100, 2, 8, 512);
        Assert.DoesNotThrow(() => e.SetAutoWahParams(0.7f, 0.5f, 0.7f));
    }
}

[TestFixture]
public class ModSelectorXamlTests
{
    static string Xaml => System.IO.File.ReadAllText(
        System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(typeof(ModSelectorXamlTests).Assembly.Location)!,
            "..", "..", "..", "..", "..", "Audition~", "MainWindow.xaml"));

    [Test] public void Xaml_HasModSelector_Chr() => Assert.That(Xaml, Does.Contain("BtnModChr"));
    [Test] public void Xaml_HasModSelector_Flg() => Assert.That(Xaml, Does.Contain("BtnModFlg"));
    [Test] public void Xaml_HasModSelector_Phs() => Assert.That(Xaml, Does.Contain("BtnModPhs"));
    [Test] public void Xaml_HasModSelector_Trm() => Assert.That(Xaml, Does.Contain("BtnModTrm"));
    [Test] public void Xaml_HasModSelector_Vib() => Assert.That(Xaml, Does.Contain("BtnModVib"));
    [Test] public void Xaml_HasModSelector_Wah() => Assert.That(Xaml, Does.Contain("BtnModWah"));
    [Test] public void Xaml_HasLabel_Trm() => Assert.That(Xaml, Does.Contain("Text=\"TRM\""));
    [Test] public void Xaml_HasLabel_Vib() => Assert.That(Xaml, Does.Contain("Text=\"VIB\""));
    [Test] public void Xaml_HasLabel_Wah() => Assert.That(Xaml, Does.Contain("Text=\"WAH\""));
}

[TestFixture]
public class PedalColourDecisionTests
{
    static string Cs => System.IO.File.ReadAllText(
        System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(typeof(PedalColourDecisionTests).Assembly.Location)!,
            "..", "..", "..", "..", "..", "Audition~", "MainWindow.xaml.cs"));

    // Each mod type must have its unique colour in UpdateChrPedalUI (cs file)
    [Test] public void Cs_HasTremoloColour_Bg()     => Assert.That(Cs, Does.Contain("#092d2d"));
    [Test] public void Cs_HasVibratoColour_Bg()      => Assert.That(Cs, Does.Contain("#090f2d"));
    [Test] public void Cs_HasAutoWahColour_Bg()      => Assert.That(Cs, Does.Contain("#2d0c09"));
    [Test] public void Cs_HasTremoloColour_Border()  => Assert.That(Cs, Does.Contain("#2db7b7"));
    [Test] public void Cs_HasVibratoColour_Border()  => Assert.That(Cs, Does.Contain("#2d44b7"));
    [Test] public void Cs_HasAutoWahColour_Border()  => Assert.That(Cs, Does.Contain("#b7392d"));
}

[TestFixture]
public class OldModeButtonsRemovedTests
{
    static string Xaml => System.IO.File.ReadAllText(
        System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(typeof(OldModeButtonsRemovedTests).Assembly.Location)!,
            "..", "..", "..", "..", "..", "Audition~", "MainWindow.xaml"));

    [Test]
    public void Xaml_NoBtnChr_OldModeButton()
        => Assert.That(Xaml, Does.Not.Contain("x:Name=\"BtnChr\""),
            "Old BtnChr mode button inside pedal must be removed.");

    [Test]
    public void Xaml_NoBtnFlg_OldModeButton()
        => Assert.That(Xaml, Does.Not.Contain("x:Name=\"BtnFlg\""),
            "Old BtnFlg mode button inside pedal must be removed.");

    [Test]
    public void Xaml_NoBtnPhs_OldModeButton()
        => Assert.That(Xaml, Does.Not.Contain("x:Name=\"BtnPhs\""),
            "Old BtnPhs mode button inside pedal must be removed.");
}
