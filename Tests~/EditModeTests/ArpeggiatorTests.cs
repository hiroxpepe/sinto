// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using System.Collections.Generic;
using NUnit.Framework;
using Signo.Core.Synth;

namespace Signo.Tests.Synth;

/// <summary>
/// Arpeggiator over held notes. Modes UP / DOWN / UP-DOWN / RANDOM, advancing
/// one step per RATE period. The arpeggiator emits the next MIDI note to play;
/// the engine layer turns that into NoteOn/Off. This covers the note-selection
/// logic deterministically (RANDOM uses a seedable RNG).
/// </summary>
[TestFixture]
public class ArpeggiatorTests
{
    static Arpeggiator Make(ArpMode mode, params int[] held)
    {
        var arp = new Arpeggiator();
        arp.SetMode(mode);
        foreach (var n in held) arp.NoteOn(n);
        return arp;
    }

    static List<int> Steps(Arpeggiator arp, int count)
    {
        var outp = new List<int>();
        for (int i = 0; i < count; i++) outp.Add(arp.NextStep());
        return outp;
    }

    [Test]
    public void Up_CyclesAscending()
    {
        var arp = Make(ArpMode.Up, 60, 64, 67); // C E G (added in order)
        Assert.That(Steps(arp, 6), Is.EqualTo(new[] { 60, 64, 67, 60, 64, 67 }));
    }

    [Test]
    public void Up_SortsHeldNotesAscending_RegardlessOfPressOrder()
    {
        var arp = Make(ArpMode.Up, 67, 60, 64); // pressed G C E
        Assert.That(Steps(arp, 3), Is.EqualTo(new[] { 60, 64, 67 }));
    }

    [Test]
    public void Down_CyclesDescending()
    {
        var arp = Make(ArpMode.Down, 60, 64, 67);
        Assert.That(Steps(arp, 4), Is.EqualTo(new[] { 67, 64, 60, 67 }));
    }

    [Test]
    public void UpDown_BouncesWithoutRepeatingEnds()
    {
        var arp = Make(ArpMode.UpDown, 60, 64, 67);
        // C E G E C E G E ... (ends not doubled)
        Assert.That(Steps(arp, 6), Is.EqualTo(new[] { 60, 64, 67, 64, 60, 64 }));
    }

    [Test]
    public void Random_StaysWithinHeldNotes()
    {
        var arp = new Arpeggiator();
        arp.SetMode(ArpMode.Random);
        arp.SetSeed(12345);
        arp.NoteOn(60); arp.NoteOn(64); arp.NoteOn(67);
        var held = new HashSet<int> { 60, 64, 67 };
        for (int i = 0; i < 50; i++)
            Assert.That(held, Does.Contain(arp.NextStep()));
    }

    [Test]
    public void Random_IsDeterministicWithSeed()
    {
        var a = new Arpeggiator(); a.SetMode(ArpMode.Random); a.SetSeed(99);
        var b = new Arpeggiator(); b.SetMode(ArpMode.Random); b.SetSeed(99);
        foreach (var n in new[] { 60, 64, 67 }) { a.NoteOn(n); b.NoteOn(n); }
        Assert.That(Steps(a, 10), Is.EqualTo(Steps(b, 10)));
    }

    [Test]
    public void NoteOff_RemovesFromPattern()
    {
        var arp = Make(ArpMode.Up, 60, 64, 67);
        arp.NoteOff(64);
        Assert.That(Steps(arp, 4), Is.EqualTo(new[] { 60, 67, 60, 67 }));
    }

    [Test]
    public void NoHeldNotes_NextStepReturnsMinusOne()
    {
        var arp = new Arpeggiator();
        arp.SetMode(ArpMode.Up);
        Assert.That(arp.NextStep(), Is.EqualTo(-1));
    }

    [Test]
    public void SingleNote_RepeatsItself()
    {
        var arp = Make(ArpMode.Up, 60);
        Assert.That(Steps(arp, 3), Is.EqualTo(new[] { 60, 60, 60 }));
    }
}
