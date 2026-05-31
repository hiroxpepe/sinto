// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using NUnit.Framework;

namespace Signo.Tests.UI;

/// <summary>
/// New FX UI structure tests.
/// Layout: [MOD selector] [MOD pedal] [MOD note grid] [DELAY pedal] [DL note grid] [REVERB pedal]
/// All pedals same height. Note grids are OUTSIDE pedals as independent columns.
/// </summary>
[TestFixture]
public class FxUiRebuildTests
{
    static string Xaml => System.IO.File.ReadAllText(
        System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(typeof(FxUiRebuildTests).Assembly.Location)!,
            "..", "..", "..", "..", "..", "Audition~", "MainWindow.xaml"));

    // ── MOD selector ─────────────────────────────────────────────────
    [Test] public void Has_ModSelector_Chr() => Assert.That(Xaml, Does.Contain("BtnModChr"));
    [Test] public void Has_ModSelector_Flg() => Assert.That(Xaml, Does.Contain("BtnModFlg"));
    [Test] public void Has_ModSelector_Phs() => Assert.That(Xaml, Does.Contain("BtnModPhs"));
    [Test] public void Has_ModSelector_Trm() => Assert.That(Xaml, Does.Contain("BtnModTrm"));
    [Test] public void Has_ModSelector_Vib() => Assert.That(Xaml, Does.Contain("BtnModVib"));
    [Test] public void Has_ModSelector_Wah() => Assert.That(Xaml, Does.Contain("BtnModWah"));

    // ── MOD pedal ────────────────────────────────────────────────────
    [Test] public void Has_PedalMod()        => Assert.That(Xaml, Does.Contain("PedalChr"));
    [Test] public void Has_LblModType()      => Assert.That(Xaml, Does.Contain("LblChrSend"));
    [Test] public void Has_Knob_ModSend()    => Assert.That(Xaml, Does.Contain("Tag=\"ch:send\""));
    [Test] public void Has_Knob_ModRate()    => Assert.That(Xaml, Does.Contain("Tag=\"ch:rate\""));
    [Test] public void Has_Knob_ModDepth()   => Assert.That(Xaml, Does.Contain("Tag=\"ch:depth\""));
    [Test] public void Has_FootMod()         => Assert.That(Xaml, Does.Contain("FootChorus"));

    // ── MOD mode buttons (GATE / TRI+SQR / UP+DOWN) ─────────────────
    [Test] public void Has_BtnModGate()      => Assert.That(Xaml, Does.Contain("BtnChrGate"));
    [Test] public void Has_BtnModTri()       => Assert.That(Xaml, Does.Contain("BtnTrmTri"));
    [Test] public void Has_BtnModSqr()       => Assert.That(Xaml, Does.Contain("BtnTrmSqr"));
    [Test] public void Has_BtnModUp()        => Assert.That(Xaml, Does.Contain("BtnWahUp"));
    [Test] public void Has_BtnModDown()      => Assert.That(Xaml, Does.Contain("BtnWahDown"));

    // ── MOD note grid (outside pedal) ────────────────────────────────
    [Test] public void Has_PanelModNotes()   => Assert.That(Xaml, Does.Contain("PanelModNotes"));
    [Test] public void Has_ModNote_DQ()      => Assert.That(Xaml, Does.Contain("Tag=\"mod:dq\""));
    [Test] public void Has_ModNote_Q()       => Assert.That(Xaml, Does.Contain("Tag=\"mod:q\""));
    [Test] public void Has_ModNote_DE()      => Assert.That(Xaml, Does.Contain("Tag=\"mod:de\""));
    [Test] public void Has_ModNote_E()       => Assert.That(Xaml, Does.Contain("Tag=\"mod:e\""));
    [Test] public void Has_ModNote_SX()      => Assert.That(Xaml, Does.Contain("Tag=\"mod:sx\""));

    // ── DELAY pedal ──────────────────────────────────────────────────
    [Test] public void Has_PedalDelay()      => Assert.That(Xaml, Does.Contain("PedalDelay"));
    [Test] public void Has_Knob_DlSend()     => Assert.That(Xaml, Does.Contain("Tag=\"dl:send\""));
    [Test] public void Has_Knob_DlTime()     => Assert.That(Xaml, Does.Contain("Tag=\"dl:time\""));
    [Test] public void Has_Knob_DlFb()       => Assert.That(Xaml, Does.Contain("Tag=\"dl:fb\""));
    [Test] public void Has_FootDelay()       => Assert.That(Xaml, Does.Contain("FootDelay"));

    // ── DL note grid (outside pedal) ─────────────────────────────────
    [Test] public void Has_PanelDlNotes()    => Assert.That(Xaml, Does.Contain("PanelDlNotes"));
    [Test] public void Has_DlNote_DQ()       => Assert.That(Xaml, Does.Contain("Tag=\"dl:dq\""));
    [Test] public void Has_DlNote_Q()        => Assert.That(Xaml, Does.Contain("Tag=\"dl:q\""));
    [Test] public void Has_DlNote_DE()       => Assert.That(Xaml, Does.Contain("Tag=\"dl:de\""));
    [Test] public void Has_DlNote_E()        => Assert.That(Xaml, Does.Contain("Tag=\"dl:e\""));
    [Test] public void Has_DlNote_SX()       => Assert.That(Xaml, Does.Contain("Tag=\"dl:sx\""));

    // ── REVERB pedal ─────────────────────────────────────────────────
    [Test] public void Has_PedalReverb()     => Assert.That(Xaml, Does.Contain("PedalReverb"));
    [Test] public void Has_Knob_RvSend()     => Assert.That(Xaml, Does.Contain("Tag=\"rv:send\""));
    [Test] public void Has_Knob_RvSize()     => Assert.That(Xaml, Does.Contain("Tag=\"rv:size\""));
    [Test] public void Has_Knob_RvDamp()     => Assert.That(Xaml, Does.Contain("Tag=\"rv:damp\""));
    [Test] public void Has_FootReverb()      => Assert.That(Xaml, Does.Contain("FootReverb"));
}
