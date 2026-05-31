// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;

namespace Signo.Tests.UI;

/// <summary>
/// Correct FX pedal structure (from reference design):
/// - Note grid is INSIDE the pedal (between pedal start and its closing).
/// - 3 knobs same size, horizontal in one row.
/// - PanelModNotes must be inside PedalChr (before PedalDelay).
/// - PanelDlNotes must be inside PedalDelay (before PedalReverb).
/// </summary>
[TestFixture]
public class FxPedalStructureTests
{
    static string Xaml => System.IO.File.ReadAllText(
        System.IO.Path.Combine(
            System.IO.Path.GetDirectoryName(typeof(FxPedalStructureTests).Assembly.Location)!,
            "..", "..", "..", "..", "..", "Audition~", "MainWindow.xaml"));

    // ── Note grid INSIDE pedal ───────────────────────────────────────
    // PanelModNotes must be inside PedalChr's Border block.
    // We verify the pedal's closing </Border> comes AFTER PanelModNotes.

    [Test]
    public void PanelModNotes_IsInside_PedalChr()
    {
        string x = Xaml;
        int pedalStart = x.IndexOf("x:Name=\"PedalChr\"");
        int notes      = x.IndexOf("x:Name=\"PanelModNotes\"");
        int footChorus = x.IndexOf("x:Name=\"FootChorus\"");
        Assert.That(pedalStart, Is.GreaterThan(-1));
        Assert.That(notes, Is.GreaterThan(pedalStart),
            "PanelModNotes must come after PedalChr start.");
        Assert.That(notes, Is.LessThan(footChorus),
            "PanelModNotes must be inside the pedal (before FootChorus).");
    }

    [Test]
    public void PanelDlNotes_IsInside_PedalDelay()
    {
        string x = Xaml;
        int pedalStart = x.IndexOf("x:Name=\"PedalDelay\"");
        int notes      = x.IndexOf("x:Name=\"PanelDlNotes\"");
        int footDelay  = x.IndexOf("x:Name=\"FootDelay\"");
        Assert.That(pedalStart, Is.GreaterThan(-1));
        Assert.That(notes, Is.GreaterThan(pedalStart),
            "PanelDlNotes must come after PedalDelay start.");
        Assert.That(notes, Is.LessThan(footDelay),
            "PanelDlNotes must be inside the pedal (before FootDelay).");
    }

    // ── 3 knobs same size ────────────────────────────────────────────
    // No knob should be Width="24" (the old large SEND knob). All 20.

    [Test]
    public void ModPedal_NoLargeKnob()
    {
        string x = Xaml;
        int kStart = x.IndexOf("x:Name=\"KnobChrLevel\"");
        int kEnd   = x.IndexOf("x:Name=\"KnobChrDepth\"") + 60;
        string seg = x.Substring(kStart, kEnd - kStart);
        Assert.That(seg, Does.Not.Contain("Width=\"24\""),
            "MOD knobs must all be same size (no Width=24 large knob).");
    }

    [Test]
    public void DelayPedal_NoLargeKnob()
    {
        string x = Xaml;
        int kStart = x.IndexOf("x:Name=\"KnobDlSend\"");
        int kEnd   = x.IndexOf("x:Name=\"KnobDlFb\"") + 60;
        string seg = x.Substring(kStart, kEnd - kStart);
        Assert.That(seg, Does.Not.Contain("Width=\"24\""),
            "DELAY knobs must all be same size (no Width=24 large knob).");
    }

    [Test]
    public void ReverbPedal_NoLargeKnob()
    {
        string x = Xaml;
        int kStart = x.IndexOf("x:Name=\"KnobRvSend\"");
        int kEnd   = x.IndexOf("x:Name=\"KnobRvDamp\"") + 60;
        if (kStart < 0) Assert.Inconclusive("REVERB knobs not named yet.");
        string seg = x.Substring(kStart, kEnd - kStart);
        Assert.That(seg, Does.Not.Contain("Width=\"24\""),
            "REVERB knobs must all be same size (no Width=24 large knob).");
    }

    // ── Note grid NOT outside pedal ──────────────────────────────────
    // There must be no standalone note column between pedals.
    // After PedalChr closes and before PedalDelay opens, no PanelModNotes.

    [Test]
    public void NoStandaloneNoteColumn_BetweenChrAndDelay()
    {
        // PanelModNotes must NOT appear after FootChorus and before PedalDelay
        string x = Xaml;
        int footChorus = x.IndexOf("x:Name=\"FootChorus\"");
        int pedalDelay = x.IndexOf("x:Name=\"PedalDelay\"");
        string between = x.Substring(footChorus, pedalDelay - footChorus);
        Assert.That(between, Does.Not.Contain("PanelModNotes"),
            "No standalone MOD note column between pedals — it must be inside the pedal.");
    }
}
