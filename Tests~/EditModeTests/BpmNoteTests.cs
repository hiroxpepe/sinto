// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Signo.Core.Effects;

namespace Signo.Tests.Effects;

/// <summary>
/// NoteValue: 5 values (♩./♩/♪./♪/♬) for MOD and DELAY independently.
/// Each note toggles BPM sync ON (press again = OFF).
/// MOD and DELAY have separate NoteValue states.
/// </summary>
[TestFixture]
public class BpmNoteTests
{
    // ── NoteValue enum has 5 values ──────────────────────────────────────

    [Test]
    public void NoteValue_Has_DottedQuarter()
        => Assert.That(Enum.IsDefined(typeof(NoteValue), "DottedQuarter"), Is.True);

    [Test]
    public void NoteValue_Has_Quarter()
        => Assert.That(Enum.IsDefined(typeof(NoteValue), "Quarter"), Is.True);

    [Test]
    public void NoteValue_Has_DottedEighth()
        => Assert.That(Enum.IsDefined(typeof(NoteValue), "DottedEighth"), Is.True);

    [Test]
    public void NoteValue_Has_Eighth()
        => Assert.That(Enum.IsDefined(typeof(NoteValue), "Eighth"), Is.True);

    [Test]
    public void NoteValue_Has_Sixteenth()
        => Assert.That(Enum.IsDefined(typeof(NoteValue), "Sixteenth"), Is.True);

    // ── Beat ratios ───────────────────────────────────────────────────────

    [Test]
    public void DottedQuarter_Is_1p5_Beats()
        => Assert.That(NoteValue.DottedQuarter.ToMs(120f), Is.EqualTo(750f).Within(1f));

    [Test]
    public void Quarter_Is_1_Beat()
        => Assert.That(NoteValue.Quarter.ToMs(120f), Is.EqualTo(500f).Within(1f));

    [Test]
    public void DottedEighth_Is_0p75_Beats()
        => Assert.That(NoteValue.DottedEighth.ToMs(120f), Is.EqualTo(375f).Within(1f));

    [Test]
    public void Eighth_Is_0p5_Beat()
        => Assert.That(NoteValue.Eighth.ToMs(120f), Is.EqualTo(250f).Within(1f));

    [Test]
    public void Sixteenth_Is_0p25_Beat()
        => Assert.That(NoteValue.Sixteenth.ToMs(120f), Is.EqualTo(125f).Within(1f));

    // ── MOD and DELAY have independent NoteValue ─────────────────────────

    [Test]
    public void Engine_SetFlangerBpmSync_Quarter_DoesNotThrow()
    {
        var e = new Signo.Core.Synth.Engine(44100, 2, 8, 512);
        Assert.DoesNotThrow(() => e.SetFlangerBpmSync(120f, NoteValue.Quarter));
    }

    [Test]
    public void Engine_SetDelayBpmSync_Eighth_DoesNotThrow()
    {
        var e = new Signo.Core.Synth.Engine(44100, 2, 8, 512);
        Assert.DoesNotThrow(() => e.SetDelayBpmSync(120f, NoteValue.Eighth));
    }

    [Test]
    public void Engine_SetDelayBpmSync_Sixteenth_DoesNotThrow()
    {
        var e = new Signo.Core.Synth.Engine(44100, 2, 8, 512);
        Assert.DoesNotThrow(() => e.SetDelayBpmSync(120f, NoteValue.Sixteenth));
    }

    // ── XAML has 5-note grid for MOD and DELAY ───────────────────────────

    [Test]
    public void Xaml_ModPedal_Has_NoteButtons_2rows()
    {
        string cs = System.IO.File.ReadAllText(System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(typeof(BpmNoteTests).Assembly.Location)!,
            "..", "..", "..", "..", "..", "Audition~", "MainWindow.xaml.cs"));
        Assert.That(cs, Does.Contain("NoteValue.Quarter"),
            "cs must reference NoteValue.Quarter for MOD note buttons.");
        Assert.That(cs, Does.Contain("NoteValue.Eighth"),
            "cs must reference NoteValue.Eighth for note buttons.");
        Assert.That(cs, Does.Contain("NoteValue.Sixteenth"),
            "cs must reference NoteValue.Sixteenth for note buttons.");
    }
}

[TestFixture]
public class BpmNoteXamlTests
{
    static string Xaml => System.IO.File.ReadAllText(System.IO.Path.Combine(
        System.IO.Path.GetDirectoryName(typeof(BpmNoteXamlTests).Assembly.Location)!,
        "..", "..", "..", "..", "..", "Audition~", "MainWindow.xaml"));

    // MOD note buttons in pedal
    [Test] public void Xaml_ModNote_DottedQuarter() => Assert.That(Xaml, Does.Contain("mod:dq"));
    [Test] public void Xaml_ModNote_Quarter()       => Assert.That(Xaml, Does.Contain("mod:q"));
    [Test] public void Xaml_ModNote_DottedEighth()  => Assert.That(Xaml, Does.Contain("mod:de"));
    [Test] public void Xaml_ModNote_Eighth()        => Assert.That(Xaml, Does.Contain("mod:e"));
    [Test] public void Xaml_ModNote_Sixteenth()     => Assert.That(Xaml, Does.Contain("mod:sx"));

    // DELAY note buttons in pedal
    [Test] public void Xaml_DlNote_DottedQuarter()  => Assert.That(Xaml, Does.Contain("dl:dq"));
    [Test] public void Xaml_DlNote_Quarter()         => Assert.That(Xaml, Does.Contain("dl:q"));
    [Test] public void Xaml_DlNote_DottedEighth()    => Assert.That(Xaml, Does.Contain("dl:de"));
    [Test] public void Xaml_DlNote_Eighth()          => Assert.That(Xaml, Does.Contain("dl:e"));
    [Test] public void Xaml_DlNote_Sixteenth()       => Assert.That(Xaml, Does.Contain("dl:sx"));
}
