#nullable enable
using NUnit.Framework;
using Sinto.Core.Synth;

namespace Sinto.Tests.Synth;

[TestFixture]
public class PortamentoTests
{
    const int SR = 44100;

    [Test] public void Initial_IsGliding_IsFalse()
    {
        var p = new Portamento();
        Assert.That(p.isGliding, Is.False);
    }

    [Test] public void SnapToTarget_InstantFrequencyChange()
    {
        var p = new Portamento();
        p.SetTarget(440f, 0f, SR);
        p.SnapToTarget();
        Assert.That(p.currentFrequency, Is.EqualTo(440f).Within(0.01f));
        Assert.That(p.isGliding, Is.False);
    }

    [Test] public void SetTarget_ZeroTime_SnapImmediate()
    {
        var p = new Portamento();
        p.SetTarget(440f, 0f, SR);
        float freq = p.Tick();
        Assert.That(freq, Is.EqualTo(440f).Within(0.01f));
    }

    [Test] public void SetTarget_NonZeroTime_GlidesGradually()
    {
        var p = new Portamento();
        p.SetTarget(220f, 0f, SR);
        p.SnapToTarget(); // start at 220Hz
        p.SetTarget(440f, 1.0f, SR); // glide to 440Hz over 1 second

        Assert.That(p.isGliding, Is.True);

        // After first tick, frequency must be between 220 and 440
        float first = p.Tick();
        Assert.That(first, Is.GreaterThan(220f).And.LessThan(440f),
            "Portamento must glide gradually, not jump instantly.");
    }

    [Test] public void Tick_ReachesTargetFrequency()
    {
        var p = new Portamento();
        p.SetTarget(220f, 0f, SR);
        p.SnapToTarget();
        p.SetTarget(440f, 0.1f, SR); // short glide

        float last = 0f;
        for (int i = 0; i < SR; i++) last = p.Tick();

        Assert.That(last, Is.EqualTo(440f).Within(1.0f),
            "Portamento must reach target frequency after glide time.");
        Assert.That(p.isGliding, Is.False,
            "isGliding must be false after reaching target.");
    }

    [Test] public void Tick_OutputIsAlwaysPositive()
    {
        // Frequency must never be zero or negative
        var p = new Portamento();
        p.SetTarget(110f, 0f, SR);
        p.SnapToTarget();
        p.SetTarget(880f, 0.5f, SR);
        for (int i = 0; i < SR / 2; i++) {
            float freq = p.Tick();
            Assert.That(freq, Is.GreaterThan(0f),
                $"Portamento frequency must always be positive. Got {freq} at sample {i}.");
        }
    }
}
