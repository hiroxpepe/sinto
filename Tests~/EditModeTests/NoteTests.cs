#nullable enable
using NUnit.Framework;
using Signo.Core.Synth;

namespace Signo.Tests.Synth;

[TestFixture]
public class NoteTests
{
    [Test] public void Note_IsValueType()
        => Assert.That(typeof(Note).IsValueType, Is.True);

    [TestCase(60, 0.8f, 1, 5)]
    [TestCase(0,  0.0f, 0, 0)]
    [TestCase(127, 1.0f, 7, 255)]
    public void Constructor_ValidValues_Stored(int midi, float vel, int track, int prio)
    {
        var n = new Note(midi, vel, track, prio);
        Assert.That(n.MidiNote,  Is.EqualTo(midi));
        Assert.That(n.Velocity,  Is.EqualTo(vel).Within(1e-6f));
        Assert.That(n.TrackId,   Is.EqualTo(track));
        Assert.That(n.Priority,  Is.EqualTo(prio));
    }

    [TestCase(-1, 0)]       // midi below min → clamped to 0
    [TestCase(128, 127)]    // midi above max → clamped to 127
    public void Constructor_MidiNote_IsClamped(int input, int expected)
    {
        var n = new Note(input, 0.5f, 0, 0);
        Assert.That(n.MidiNote, Is.EqualTo(expected));
    }

    [TestCase(-0.1f, 0.0f)]
    [TestCase(1.1f,  1.0f)]
    public void Constructor_Velocity_IsClamped(float input, float expected)
    {
        var n = new Note(60, input, 0, 0);
        Assert.That(n.Velocity, Is.EqualTo(expected).Within(1e-6f));
    }

    [Test] public void FrequencyHz_MiddleC_Is261Hz()
    {
        // Within(1f) allows ~4 cents error — audible as chorus/beating in chords.
        // Within(0.01f) = < 0.04 cents error — imperceptible even in close harmony.
        var n = new Note(60, 1f, 0, 0); // MIDI 60 = C4 = 261.63 Hz
        Assert.That(n.FrequencyHz, Is.EqualTo(261.63f).Within(0.01f),
            "C4 frequency must be within 0.01 Hz of 261.63 Hz.");
    }

    [Test] public void FrequencyHz_A4_Is440Hz()
    {
        var n = new Note(69, 1f, 0, 0); // MIDI 69 = A4 = 440 Hz
        Assert.That(n.FrequencyHz, Is.EqualTo(440.0f).Within(0.01f),
            "A4 must be exactly 440.00 Hz within 0.01 Hz tolerance.");
    }

    [Test] public void FrequencyHz_OctaveAbove_IsDoubled()
    {
        var c4  = new Note(60, 1f, 0, 0);
        var c5  = new Note(72, 1f, 0, 0); // one octave up
        Assert.That(c5.FrequencyHz, Is.EqualTo(c4.FrequencyHz * 2f).Within(0.01f),
            "One octave up must double the frequency.");
    }

    [Test] public void FrequencyHz_AllMidiNotes_AreFiniteAndPositive()
    {
        for (int midi = 0; midi <= 127; midi++) {
            var n = new Note(midi, 1f, 0, 0);
            Assert.That(float.IsNaN(n.FrequencyHz),      Is.False, $"MIDI {midi} FrequencyHz is NaN.");
            Assert.That(float.IsInfinity(n.FrequencyHz), Is.False, $"MIDI {midi} FrequencyHz is Inf.");
            Assert.That(n.FrequencyHz, Is.GreaterThan(0f), $"MIDI {midi} FrequencyHz must be > 0.");
        }
    }

    [Test] public void Equals_SameValues_ReturnsTrue()
    {
        var a = new Note(60, 0.8f, 1, 5);
        var b = new Note(60, 0.8f, 1, 5);
        Assert.That(a.Equals(b), Is.True);
    }

    [Test] public void Equals_DifferentMidi_ReturnsFalse()
    {
        var a = new Note(60, 0.8f, 1, 5);
        var b = new Note(61, 0.8f, 1, 5);
        Assert.That(a.Equals(b), Is.False);
    }
}
