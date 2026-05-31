// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using NUnit.Framework;

namespace Signo.Tests.UI;

/// <summary>
/// New FX panel layout:
/// [MOD selector] [MOD pedal] [MOD note col] [DELAY pedal] [DL note col] [REVERB pedal]
/// - MOD/DELAY note columns are OUTSIDE pedals (independent vertical strips)
/// - All pedals same height regardless of BPM/mode buttons
/// - Note buttons: 2 rows × 3 cols (♩./♩/♪. top, ♪/♬/- bottom)
/// - Colour system: S=75% V=72% per pedal type
/// </summary>
[TestFixture]
public class FxPanelRebuildTests
{
    static string Xaml => System.IO.File.ReadAllText(System.IO.Path.Combine(
        System.IO.Path.GetDirectoryName(typeof(FxPanelRebuildTests).Assembly.Location)!,
        "..", "..", "..", "..", "..", "Audition~", "MainWindow.xaml"));

    // ── MOD selector ──────────────────────────────────────────────────
    [Test] public void Has_ModSelector_Chr() => Assert.That(Xaml, Does.Contain("BtnModChr"));
    [Test] public void Has_ModSelector_Flg() => Assert.That(Xaml, Does.Contain("BtnModFlg"));
    [Test] public void Has_ModSelector_Phs() => Assert.That(Xaml, Does.Contain("BtnModPhs"));
    [Test] public void Has_ModSelector_Trm() => Assert.That(Xaml, Does.Contain("BtnModTrm"));
    [Test] public void Has_ModSelector_Vib() => Assert.That(Xaml, Does.Contain("BtnModVib"));
    [Test] public void Has_ModSelector_Wah() => Assert.That(Xaml, Does.Contain("BtnModWah"));

    // ── MOD pedal ─────────────────────────────────────────────────────
    [Test] public void Has_PedalMod()        => Assert.That(Xaml, Does.Contain("PedalChr"));
    [Test] public void Has_ModKnob_Level()   => Assert.That(Xaml, Does.Contain("KnobChrLevel"));
    [Test] public void Has_ModKnob_Rate()    => Assert.That(Xaml, Does.Contain("KnobChrRate"));
    [Test] public void Has_ModKnob_Depth()   => Assert.That(Xaml, Does.Contain("KnobChrDepth"));
    [Test] public void Has_Mod_GateBtn()     => Assert.That(Xaml, Does.Contain("BtnChrGate"));
    [Test] public void Has_Mod_TrmWave()     => Assert.That(Xaml, Does.Contain("BtnTrmTri"));
    [Test] public void Has_Mod_WahDir()      => Assert.That(Xaml, Does.Contain("BtnWahUp"));
    [Test] public void Has_Mod_FootSwitch()  => Assert.That(Xaml, Does.Contain("FootChorus"));

    // ── MOD note column (OUTSIDE pedal) ───────────────────────────────
    [Test] public void Has_ModNoteCol()      => Assert.That(Xaml, Does.Contain("PanelModNotes"));
    [Test] public void Has_ModNote_DQ()      => Assert.That(Xaml, Does.Contain("BtnModNDQ"));
    [Test] public void Has_ModNote_Q()       => Assert.That(Xaml, Does.Contain("BtnModNQ"));
    [Test] public void Has_ModNote_DE()      => Assert.That(Xaml, Does.Contain("BtnModNDE"));
    [Test] public void Has_ModNote_E()       => Assert.That(Xaml, Does.Contain("BtnModNE"));
    [Test] public void Has_ModNote_SX()      => Assert.That(Xaml, Does.Contain("BtnModNSX"));

    // ── DELAY pedal ───────────────────────────────────────────────────
    [Test] public void Has_PedalDelay()      => Assert.That(Xaml, Does.Contain("PedalDelay"));
    [Test] public void Has_DelayKnob_Send()  => Assert.That(Xaml, Does.Contain("KnobDlSend"));
    [Test] public void Has_DelayKnob_Time()  => Assert.That(Xaml, Does.Contain("KnobDlTime"));
    [Test] public void Has_DelayKnob_Fb()    => Assert.That(Xaml, Does.Contain("KnobDlFb"));
    [Test] public void Has_Delay_FootSwitch()=> Assert.That(Xaml, Does.Contain("FootDelay"));

    // ── DELAY note column (OUTSIDE pedal) ─────────────────────────────
    [Test] public void Has_DlNoteCol()       => Assert.That(Xaml, Does.Contain("PanelDlNotes"));
    [Test] public void Has_DlNote_DQ()       => Assert.That(Xaml, Does.Contain("BtnDlNDQ"));
    [Test] public void Has_DlNote_Q()        => Assert.That(Xaml, Does.Contain("BtnDlNQ"));
    [Test] public void Has_DlNote_DE()       => Assert.That(Xaml, Does.Contain("BtnDlNDE"));
    [Test] public void Has_DlNote_E()        => Assert.That(Xaml, Does.Contain("BtnDlNE"));
    [Test] public void Has_DlNote_SX()       => Assert.That(Xaml, Does.Contain("BtnDlNSX"));

    // ── REVERB pedal ──────────────────────────────────────────────────
    [Test] public void Has_PedalReverb()     => Assert.That(Xaml, Does.Contain("PedalReverb"));
    [Test] public void Has_ReverbKnob_Send() => Assert.That(Xaml, Does.Contain("KnobRvSend"));
    [Test] public void Has_ReverbKnob_Size() => Assert.That(Xaml, Does.Contain("KnobRvSize"));
    [Test] public void Has_ReverbKnob_Damp() => Assert.That(Xaml, Does.Contain("KnobRvDamp"));
    [Test] public void Has_Reverb_FootSwitch()=>Assert.That(Xaml, Does.Contain("FootReverb"));
}
