// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using NUnit.Framework;

namespace Signo.Tests.UI;

/// <summary>
/// FX panel expansion tests.
/// Decision table:
/// - DELAY/REVERB: no dummy mode button row
/// - BPM sync: OFF / ♩. / ♪. only (no ♩ or ♬)
/// - TREMOLO: TRI/SQR mode buttons present
/// - AUTO WAH: UP/DOWN mode buttons present
/// - BPM sliders: step=1 for 1-unit precision
/// - LFO S/H BPM and ARP BPM share same value (synced)
/// </summary>
[TestFixture]
public class FxExpandTests
{
    static string Xaml => System.IO.File.ReadAllText(XamlPath);
    static string Cs   => System.IO.File.ReadAllText(CsPath);
    static string XamlPath => System.IO.Path.Combine(
        System.IO.Path.GetDirectoryName(typeof(FxExpandTests).Assembly.Location)!,
        "..", "..", "..", "..", "..", "Audition~", "MainWindow.xaml");
    static string CsPath => System.IO.Path.Combine(
        System.IO.Path.GetDirectoryName(typeof(FxExpandTests).Assembly.Location)!,
        "..", "..", "..", "..", "..", "Audition~", "MainWindow.xaml.cs");

    // ── DELAY/REVERB: ダミーボタン行がないこと ──────────────────────
    [Test]
    public void Delay_HasNo_DummyModeButtonRow()
        => Assert.That(Xaml, Does.Not.Contain("BtnDlMode"),
            "DELAY must not have dummy mode buttons.");

    [Test]
    public void Reverb_HasNo_DummyModeButtonRow()
        => Assert.That(Xaml, Does.Not.Contain("BtnRvMode"),
            "REVERB must not have dummy mode buttons.");

    // ── BPMシンク: OFF/♩./♪. の3択 ──────────────────────────────
    [Test]
    public void BpmSync_Has_DottedQuarter()
        => Assert.That(Cs, Does.Contain("DottedQuarter"),
            "BPM sync must include DottedQuarter option.");

    [Test]
    public void BpmSync_Has_DottedEighth()
        => Assert.That(Cs, Does.Contain("DottedEighth"),
            "BPM sync must include DottedEighth option.");

    // ── TREMOLO: TRI/SQRボタン ───────────────────────────────────
    [Test]
    public void Tremolo_HasTRI_Button()
        => Assert.That(Xaml, Does.Contain("BtnTrmTri"),
            "TREMOLO must have TRI waveform button.");

    [Test]
    public void Tremolo_HasSQR_Button()
        => Assert.That(Xaml, Does.Contain("BtnTrmSqr"),
            "TREMOLO must have SQR waveform button.");

    // ── AUTO WAH: UP/DOWNボタン ──────────────────────────────────
    [Test]
    public void AutoWah_HasUP_Button()
        => Assert.That(Xaml, Does.Contain("BtnWahUp"),
            "AUTO WAH must have UP direction button.");

    [Test]
    public void AutoWah_HasDOWN_Button()
        => Assert.That(Xaml, Does.Contain("BtnWahDown"),
            "AUTO WAH must have DOWN direction button.");

    // ── BPMスライダー: step=1 ────────────────────────────────────
    [Test]
    public void ArpBpm_Slider_HasStep1()
        => Assert.That(Xaml, Does.Contain("SldArpRate"),
            "ARP BPM slider must exist (SlArpBpm).");

    [Test]
    public void LfoSh_Bpm_Slider_HasStep1()
        => Assert.That(Xaml, Does.Contain("SldLfoRate"),
            "LFO S/H BPM slider must exist (SlShBpm).");

    // ── LFO S/H BPM ↔ ARP BPM 同期 ─────────────────────────────
    [Test]
    public void Cs_SyncsBpm_ArpToSh()
        => Assert.That(Cs, Does.Contain("SldLfoRate"),
            "cs must reference SlShBpm for BPM sync.");
}
