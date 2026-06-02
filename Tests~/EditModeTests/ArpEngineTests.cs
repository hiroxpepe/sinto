// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Signo.Core.Synth;

namespace Signo.Tests.Synth;

/// <summary>
/// Arpeggiator integrated into the VAEngine. When ARP is on, held notes are
/// collected and the engine plays one at a time at the arp rate (audio-thread
/// driven). When off, notes play normally (polyphonic).
/// </summary>
[TestFixture]
public class ArpEngineTests
{
    const int SR = 44100;

    static int ActiveAfter(VAEngine e, int renderCalls, int bufFrames = 256)
    {
        var buf = new float[bufFrames * 2];
        for (int i = 0; i < renderCalls; i++) e.ProcessAudioCallback(buf.AsSpan());
        return e.activeVoices;
    }

    [Test]
    public void ArpOff_ChordPlaysPolyphonically()
    {
        var e = new VAEngine(SR, 2, 32, 256);
        e.SetArpEnabled(false);
        e.SendNoteOn(60, 0.8f, 2, 5, 0);
        e.SendNoteOn(64, 0.8f, 2, 5, 0);
        e.SendNoteOn(67, 0.8f, 2, 5, 0);
        int active = ActiveAfter(e, 1);
        Assert.That(active, Is.EqualTo(3), "With ARP off, a 3-note chord sounds all voices.");
    }

    [Test]
    public void ArpOn_PlaysOneNoteAtATime()
    {
        var e = new VAEngine(SR, 2, 32, 256);
        e.SetArpEnabled(true);
        e.SetArpRate(120f); // 120 BPM
        e.SetArpMode(ArpMode.Up);
        e.SendNoteOn(60, 0.8f, 2, 5, 0);
        e.SendNoteOn(64, 0.8f, 2, 5, 0);
        e.SendNoteOn(67, 0.8f, 2, 5, 0);
        // After a little rendering, only one arp voice should sound at once.
        int active = ActiveAfter(e, 2);
        Assert.That(active, Is.LessThanOrEqualTo(1),
            "With ARP on, only one held note sounds at a time.");
    }

    [Test]
    public void ArpOn_AdvancesOverTime()
    {
        var e = new VAEngine(SR, 2, 32, 256);
        e.SetArpEnabled(true);
        e.SetArpRate(240f); // fast so steps occur within the render window
        e.SetArpMode(ArpMode.Up);
        e.SendNoteOn(60, 0.8f, 2, 5, 0);
        e.SendNoteOn(64, 0.8f, 2, 5, 0);
        // Collect which notes become active across many render slices.
        var seen = new HashSet<int>();
        var buf = new float[128 * 2];
        for (int i = 0; i < 200; i++) {
            e.ProcessAudioCallback(buf.AsSpan());
            int n = e.currentArpNote;
            if (n >= 0) seen.Add(n);
        }
        Assert.That(seen, Does.Contain(60));
        Assert.That(seen, Does.Contain(64));
    }

    [Test]
    public void ArpOn_NoHeldNotes_NothingSounds()
    {
        var e = new VAEngine(SR, 2, 32, 256);
        e.SetArpEnabled(true);
        e.SetArpRate(120f);
        int active = ActiveAfter(e, 4);
        Assert.That(active, Is.EqualTo(0));
    }

    [Test]
    public void ArpDisabledMidPlay_StopsArpVoices()
    {
        var e = new VAEngine(SR, 2, 32, 256);
        e.SetArpEnabled(true);
        e.SetArpRate(120f);
        e.SetArpMode(ArpMode.Up);
        e.SendNoteOn(60, 0.8f, 2, 5, 0);
        e.SendNoteOn(64, 0.8f, 2, 5, 0);
        ActiveAfter(e, 2);
        e.SetArpEnabled(false); // turn arp off
        var buf = new float[256 * 2];
        for (int i = 0; i < 4; i++) e.ProcessAudioCallback(buf.AsSpan());
        // Not asserting an exact count (release tails); just that it doesn't throw
        // and the arp note resets.
        Assert.That(e.currentArpNote, Is.EqualTo(-1));
    }
}
