// Copyright (c) STUDIO MeowToon. All rights reserved.
// Licensed under the MIT License.
#nullable enable
using System;
using NUnit.Framework;
using Sinto.Core.Audio;

namespace Sinto.Tests.Audio;

[TestFixture]
public class ControlEventTests
{
    // ── Constructor ──────────────────────────────────────────────

    [Test]
    public void Constructor_Default_KindIsNoteOn()
    {
        var ev = new ControlEvent(ControlEventKind.NoteOn);
        Assert.That(ev.Kind, Is.EqualTo(ControlEventKind.NoteOn));
    }

    [Test]
    public void Constructor_AllFields_AreStored()
    {
        var ev = new ControlEvent(ControlEventKind.Pause, 512, 60, 0.8f, 3, 7);
        Assert.That(ev.Kind,         Is.EqualTo(ControlEventKind.Pause));
        Assert.That(ev.OffsetFrames, Is.EqualTo((ushort)512));
        Assert.That(ev.IntParam,     Is.EqualTo(60));
        Assert.That(ev.FloatParam,   Is.EqualTo(0.8f).Within(1e-6f));
        Assert.That(ev.TrackId,      Is.EqualTo(3));
        Assert.That(ev.Priority,     Is.EqualTo(7));
    }

    [Test]
    public void Constructor_DefaultOptionals_AreZero()
    {
        var ev = new ControlEvent(ControlEventKind.Resume);
        Assert.That(ev.OffsetFrames, Is.EqualTo((ushort)0));
        Assert.That(ev.IntParam,     Is.EqualTo(0));
        Assert.That(ev.FloatParam,   Is.EqualTo(0f).Within(1e-6f));
        Assert.That(ev.TrackId,      Is.EqualTo(0));
        Assert.That(ev.Priority,     Is.EqualTo(0));
    }

    // ── ControlEventKind ─────────────────────────────────────────

    [TestCase(ControlEventKind.None)]
    [TestCase(ControlEventKind.NoteOn)]
    [TestCase(ControlEventKind.NoteOff)]
    [TestCase(ControlEventKind.Pause)]
    [TestCase(ControlEventKind.Resume)]
    [TestCase(ControlEventKind.SetVoiceLimit)]
    [TestCase(ControlEventKind.SwapPreset)]
    [TestCase(ControlEventKind.SetBPM)]
    public void ControlEventKind_AllRequiredValues_Exist(ControlEventKind kind)
        => Assert.That(Enum.IsDefined(typeof(ControlEventKind), kind), Is.True);

    [Test]
    public void ControlEventKind_None_IsZero()
    {
        // default(ControlEvent).Kind must be None (0), NOT NoteOn.
        // If NoteOn=0, every uninitialized ControlEvent would fire a phantom note.
        Assert.That((int)ControlEventKind.None, Is.EqualTo(0));
        Assert.That(default(ControlEvent).Kind, Is.EqualTo(ControlEventKind.None));
    }

    [Test]
    public void ControlEventKind_LoadPreset_DoesNotExist()
        => Assert.That(Enum.IsDefined(typeof(ControlEventKind), "LoadPreset"), Is.False);

    // ── IsValueType ──────────────────────────────────────────────

    [Test]
    public void ControlEvent_IsReadonlyStruct()
        => Assert.That(typeof(ControlEvent).IsValueType, Is.True);

    // ── IEquatable ───────────────────────────────────────────────

    [Test]
    public void Equals_SameValues_ReturnsTrue()
    {
        var a = new ControlEvent(ControlEventKind.NoteOn, 0, 60, 0.8f, 1, 5);
        var b = new ControlEvent(ControlEventKind.NoteOn, 0, 60, 0.8f, 1, 5);
        Assert.That(a.Equals(b), Is.True);
    }

    [Test]
    public void Equals_DifferentKind_ReturnsFalse()
    {
        var a = new ControlEvent(ControlEventKind.NoteOn);
        var b = new ControlEvent(ControlEventKind.NoteOff);
        Assert.That(a.Equals(b), Is.False);
    }

    [Test]
    public void OperatorEquals_SameValues_ReturnsTrue()
    {
        var a = new ControlEvent(ControlEventKind.Pause, 10);
        var b = new ControlEvent(ControlEventKind.Pause, 10);
        Assert.That(a == b, Is.True);
    }

    [Test]
    public void OperatorNotEquals_DifferentOffset_ReturnsTrue()
    {
        var a = new ControlEvent(ControlEventKind.NoteOn, 0);
        var b = new ControlEvent(ControlEventKind.NoteOn, 512);
        Assert.That(a != b, Is.True);
    }

    // ── Edge Cases ───────────────────────────────────────────────

    [Test]
    public void OffsetFrames_MaxUshort_IsStoredCorrectly()
    {
        var ev = new ControlEvent(ControlEventKind.NoteOn, ushort.MaxValue);
        Assert.That(ev.OffsetFrames, Is.EqualTo(ushort.MaxValue));
    }

    [Test]
    public void SetBPM_FloatParam_StoresBpm()
    {
        var ev = new ControlEvent(ControlEventKind.SetBPM, 0, 0, 120.5f);
        Assert.That(ev.FloatParam, Is.EqualTo(120.5f).Within(1e-4f));
    }
}
