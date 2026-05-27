// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Sinto.Core.Audio;

namespace Sinto.Tests.Audio;

[TestFixture]
public class EventTests
{
    // ── Constructor ──────────────────────────────────────────────

    [Test]
    public void Constructor_Default_KindIsNoteOn()
    {
        var ev = new Event(EventKind.NoteOn);
        Assert.That(ev.Kind, Is.EqualTo(EventKind.NoteOn));
    }

    [Test]
    public void Constructor_AllFields_AreStored()
    {
        var ev = new Event(EventKind.Pause, 512, 60, 0.8f, 3, 7);
        Assert.That(ev.Kind,         Is.EqualTo(EventKind.Pause));
        Assert.That(ev.OffsetFrames, Is.EqualTo((ushort)512));
        Assert.That(ev.IntParam,     Is.EqualTo(60));
        Assert.That(ev.FloatParam,   Is.EqualTo(0.8f).Within(1e-6f));
        Assert.That(ev.TrackId,      Is.EqualTo(3));
        Assert.That(ev.Priority,     Is.EqualTo(7));
    }

    [Test]
    public void Constructor_DefaultOptionals_AreZero()
    {
        var ev = new Event(EventKind.Resume);
        Assert.That(ev.OffsetFrames, Is.EqualTo((ushort)0));
        Assert.That(ev.IntParam,     Is.EqualTo(0));
        Assert.That(ev.FloatParam,   Is.EqualTo(0f).Within(1e-6f));
        Assert.That(ev.TrackId,      Is.EqualTo(0));
        Assert.That(ev.Priority,     Is.EqualTo(0));
    }

    // ── EventKind ─────────────────────────────────────────

    [TestCase(EventKind.None)]
    [TestCase(EventKind.NoteOn)]
    [TestCase(EventKind.NoteOff)]
    [TestCase(EventKind.Pause)]
    [TestCase(EventKind.Resume)]
    [TestCase(EventKind.SetVoiceLimit)]
    [TestCase(EventKind.SwapPreset)]
    [TestCase(EventKind.SetBPM)]
    public void EventKind_AllRequiredValues_Exist(EventKind kind)
        => Assert.That(Enum.IsDefined(typeof(EventKind), kind), Is.True);

    [Test]
    public void EventKind_None_IsZero()
    {
        // default(Event).Kind must be None (0), NOT NoteOn.
        // If NoteOn=0, every uninitialized Event would fire a phantom note.
        Assert.That((int)EventKind.None, Is.EqualTo(0));
        Assert.That(default(Event).Kind, Is.EqualTo(EventKind.None));
    }

    [Test]
    public void EventKind_LoadPreset_DoesNotExist()
        => Assert.That(Enum.IsDefined(typeof(EventKind), "LoadPreset"), Is.False);

    // ── IsValueType ──────────────────────────────────────────────

    [Test]
    public void Event_IsReadonlyStruct()
        => Assert.That(typeof(Event).IsValueType, Is.True);

    // ── IEquatable ───────────────────────────────────────────────

    [Test]
    public void Equals_SameValues_ReturnsTrue()
    {
        var a = new Event(EventKind.NoteOn, 0, 60, 0.8f, 1, 5);
        var b = new Event(EventKind.NoteOn, 0, 60, 0.8f, 1, 5);
        Assert.That(a.Equals(b), Is.True);
    }

    [Test]
    public void Equals_DifferentKind_ReturnsFalse()
    {
        var a = new Event(EventKind.NoteOn);
        var b = new Event(EventKind.NoteOff);
        Assert.That(a.Equals(b), Is.False);
    }

    [Test]
    public void OperatorEquals_SameValues_ReturnsTrue()
    {
        var a = new Event(EventKind.Pause, 10);
        var b = new Event(EventKind.Pause, 10);
        Assert.That(a == b, Is.True);
    }

    [Test]
    public void OperatorNotEquals_DifferentOffset_ReturnsTrue()
    {
        var a = new Event(EventKind.NoteOn, 0);
        var b = new Event(EventKind.NoteOn, 512);
        Assert.That(a != b, Is.True);
    }

    // ── Edge Cases ───────────────────────────────────────────────

    [Test]
    public void OffsetFrames_MaxUshort_IsStoredCorrectly()
    {
        var ev = new Event(EventKind.NoteOn, ushort.MaxValue);
        Assert.That(ev.OffsetFrames, Is.EqualTo(ushort.MaxValue));
    }

    [Test]
    public void SetBPM_FloatParam_StoresBpm()
    {
        var ev = new Event(EventKind.SetBPM, 0, 0, 120.5f);
        Assert.That(ev.FloatParam, Is.EqualTo(120.5f).Within(1e-4f));
    }
}
